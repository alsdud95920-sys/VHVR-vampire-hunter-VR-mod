using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEngine;

namespace VampireHuntersVR.Interaction;

[StructLayout(LayoutKind.Sequential)]
public struct NativeHandPose
{
    public int Active;
    public float X, Y, Z, Qx, Qy, Qz, Qw, Trigger;
    public int Face;
    // D-091: locomotion/menu inputs (must match HandPose in Host.h).
    public float StickX, StickY;
    public int StickClick, Lower, Menu;
    // D-206 (BUG-204): the grip (squeeze) value 0..1, appended to match the native HandPose field order exactly.
    public float Squeeze;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeHandsStatus
{
    public int Available, Seq, Frames, Reserved;
    public NativeHandPose Left, Right;
}

internal static class HandAimNative
{
    private const string Dll = "VhvrNativeHost.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int EnableHands(int enabled);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int ReadHands(out NativeHandsStatus status);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ReadHandsError();
    // D-103: queue one short vibration on one controller (0 left, 1 right). Applied by the native render thread after the next sync.
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int RequestHaptic(int hand, float amplitude, int durationMs);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ReadHapticError();
}

// Soft-gated per-hand aim driver (Stage 6B). Each hand moves ONLY its own weapon-slot container.
// There is no link between the hands. Opt-in FireTick starts/releases each native slot; the stock
// Weapon.update owns sustained cadence. Attack acceptance requires game/headset observations.
internal sealed partial class HandAimDriver
{
    private sealed class HandSlot
    {
        public string Name = "";
        public EWeaponSlot Slot;
        public Transform? Container;
        public bool SharedOrAmbiguous;
        public bool RestCaptured;
        // D-202 (BUG-202): true when RestLocalRotation was captured with the tracked pose that was live in that frame
        // (the normal case). False means no valid pose existed at the capture instant and only the frame was recorded.
        public bool RestCapturedWithPose;
        public float[] RestLocalRotation = { 0, 0, 0, 1 };
        public Vector3 RestLocalPosition;
        public Quaternion RestLocalUnityRotation = Quaternion.identity;
        public Vector3 LastWrittenPosition;
        public Quaternion LastWrittenRotation = Quaternion.identity;
        public bool HasWritten;
        public int GameOverwrites, DrivenFrames, WeaponCount;
        public int LastSeq = int.MinValue, LastSeqFrame;
        public float Trigger;
        public bool Face;
        public bool SampleActive;
        public bool HasAimRay;
        public Vector3 AimOrigin, AimForward;
        public Quaternion AimRotation = Quaternion.identity;
        public HandAimState State = HandAimState.WaitingForWeapons;
        public float MuzzleToHandAngle = float.NaN;
        // D-153 (TOOL-073): how far the weapon's own up axis sits from the controller's up around the muzzle axis, and
        // the dial value that was applied this frame. Both are reported; neither is a claim about the visible mesh.
        public float LastTwistResidualDeg = float.NaN, MaxTwistResidualAbsDeg = float.NaN, LastAppliedTwistDeg;
        public string? MuzzlePath;
        public int FireCalls, ReleaseCalls;
        public int Presses;        // D-101: trigger press edges (ammo-drain measurement without counting by eye)
        public float HeldSeconds;  // D-101: seconds the trigger latch was held in a fireable world
        public bool FireHeld;
        public GameObject? Reticle;
        public float ReticleDistance;
        public bool ReticleHit, ReticleVisible;
        public GameObject? UiRay, UiDot;
    }

    private readonly HandSlot left = new() { Name = "left", Slot = EWeaponSlot.LEFT_HAND };
    private readonly HandSlot right = new() { Name = "right", Slot = EWeaponSlot.RIGHT_HAND };
    public ShotAim? Shot;
    // D-153 (TOOL-073): the per-hand weapon rest-twist dial, in degrees about the muzzle axis, applied AFTER the forward
    // alignment above so it can never change where a weapon points, where its shots leave from, or the aim ray. 0 is the
    // measured default (no correction): the left weapon's roll follows the captured authored rest pose, and D-153 found
    // that this leaves a fixed hand-relative tilt that is not the same for both hands. The dial exists so one sitting can
    // move only that tilt per hand, and the receipt records both the residual it measured and the value it applied.
    public float LeftWeaponTwistDeg, RightWeaponTwistDeg;
    private readonly string receiptPath;
    private readonly JsonSerializerOptions json;
    private DateTime nextScan, nextReceipt;
    private string? lastError;
    private int nativeFailures;

    public HandAimDriver(string outputDirectory, JsonSerializerOptions options)
    {
        receiptPath = Path.Combine(outputDirectory, "hand-aim.json");
        json = options;
    }

    public void Tick(Camera source, Vector3 origin, Quaternion poseBaseRotation, string processedScene,
        PlayerController? player = null, bool fireEnabled = false)
    {
        int frame = Time.frameCount;
        int read = HandAimNative.ReadHands(out NativeHandsStatus status);
        ObserveHandRead(frame, read, status); // D-136/D-138 (restored, BUG-205): read BEFORE the busy return below, so a busy
                                              // native frame is still visible in the probe. Read-only; no gameplay effect.
        if (read == 6) { ReapplyLastHandPose(); PinFlameEmitters(FlameEmitterStage.Busy); return; } // native render callback busy: skip this frame, never block
        if (read == 2) { Fail("ReadHands rejected"); ReapplyLastHandPose(); PinFlameEmitters(FlameEmitterStage.Busy); return; }
        bool available = read == 0 && status.Seq > 0;
        if (read == 1 && status.Seq == -1 && lastError == null)
            lastError = Marshal.PtrToStringAnsi(HandAimNative.ReadHandsError());

        ApplyAutonomousGaze(source);
        if (DateTime.UtcNow >= nextScan) { nextScan = DateTime.UtcNow.AddSeconds(1); Scan(source); }

        int leftDrivenBefore = left.DrivenFrames, rightDrivenBefore = right.DrivenFrames;
        Update(left, available ? status.Left : default, available, status.Seq, frame, origin, poseBaseRotation);
        Update(right, available ? status.Right : default, available, status.Seq, frame, origin, poseBaseRotation);
        NoteHandBody(player, leftDrivenBefore, rightDrivenBefore); // D-143: body position for the pre-particle hand hold
        PublishShotFrames(source);
        Shot?.ObserveFlames();
        TracePose(source); // D-127 (TOOL-071): read-only rotation trace, no behavior depends on it
        UpdateReticles(source);
        UiTick(source);
        MarkersTick(source); // D-181: restored - dropped by the D-178 source recovery, so potion/chest arrows vanished
        HapticTick(available);
        InputTick(status, available, source, player);
        TurnTick(status, available, Time.deltaTime);
        if (fireEnabled && player != null) FireTick(player, source);
        ObserveHandApplied(); // D-178 recovery: this diagnostics call was dropped with the recovered source (the RenderTiming
                              // fixture "applied-only-after-fire-tick" pins it). Timing probe only; no gameplay effect.
        lastCompletedTick = frame;
        PinFlameEmitters(FlameEmitterStage.Tick); // D-141: last writer of the emitter rotation this frame, after every parent change
        if (DateTime.UtcNow >= nextReceipt)
        {
            nextReceipt = DateTime.UtcNow.AddSeconds(1);
            // D-099: a receipt failure (ha9: JSON infinity) must never disable aim/fire/locomotion; log once and keep driving.
            try { WriteReceipt(processedScene, available, status); }
            catch (System.Exception e) { receiptError ??= e.GetType().Name + ": " + e.Message; }
        }
    }

    public string? receiptError;
    private static float? FiniteOrNull(float v) => float.IsFinite(v) ? v : (float?)null;

    // Slot ownership comes from the weapon's own current_weapon_slot, never from an object name alone.
    private void Scan(Camera source)
    {
        var leftContainers = new HashSet<Transform>();
        var rightContainers = new HashSet<Transform>();
        int leftWeapons = 0, rightWeapons = 0;
        foreach (var weapon in Resources.FindObjectsOfTypeAll<Weapon>())
        {
            if (weapon == null || !weapon.gameObject.activeInHierarchy || !weapon.transform.IsChildOf(source.transform)) continue;
            Transform? container = weapon.transform;
            while (container != null && container.parent != source.transform) container = container.parent;
            if (container == null) continue;
            var slot = weapon.current_weapon_slot;
            if (slot == EWeaponSlot.LEFT_HAND) { leftContainers.Add(container); leftWeapons++; }
            else if (slot == EWeaponSlot.RIGHT_HAND) { rightContainers.Add(container); rightWeapons++; }
        }
        Assign(left, leftContainers, rightContainers, leftWeapons);
        Assign(right, rightContainers, leftContainers, rightWeapons);
    }

    private static void Assign(HandSlot slot, HashSet<Transform> own, HashSet<Transform> other, int weaponCount)
    {
        slot.WeaponCount = weaponCount;
        slot.SharedOrAmbiguous = own.Count > 1 || own.Overlaps(other);
        Transform? chosen = own.Count == 1 ? own.First() : null;
        // Unity object identity, not managed IL2CPP wrapper identity across scans.
        if (chosen != slot.Container) { slot.Container = chosen; slot.RestCaptured = false; slot.RestCapturedWithPose = false; slot.HasWritten = false; }
    }

    // D-202 (BUG-202): poseBaseRotation is the frame the caller composes the tracked pose in - identity for the eye
    // frame (the default) or the game camera rotation for the legacy A/B mode. The camera object is no longer read
    // here: the rest capture takes its frame from this one value, so the captured frame and the driven frame can
    // never disagree (that disagreement was the 90 deg hand/aim offset in 0.9.4).
    private void Update(HandSlot slot, NativeHandPose pose, bool available, int seq, int frame,
        Vector3 origin, Quaternion poseBaseRotation)
    {
        slot.SampleActive = available && pose.Active == 1;
        slot.HasAimRay = false;
        if (slot.SampleActive)
        {
            HandAimMath.ToWorld(new[] { origin.x, origin.y, origin.z }, Rotation(poseBaseRotation),
                new HandSample(true, pose.X, pose.Y, pose.Z, pose.Qx, pose.Qy, pose.Qz, pose.Qw), out float[] rayPos, out float[] rayRot);
            float[] fwd = HandAimMath.Forward(rayRot);
            slot.AimOrigin = new Vector3(rayPos[0], rayPos[1], rayPos[2]);
            slot.AimForward = new Vector3(fwd[0], fwd[1], fwd[2]);
            slot.AimRotation = new Quaternion(rayRot[0], rayRot[1], rayRot[2], rayRot[3]);
            slot.HasAimRay = true;
        }
        slot.Trigger = available ? pose.Trigger : 0f;
        slot.Face = available && pose.Face == 1;
        if (available && seq != slot.LastSeq) { slot.LastSeq = seq; slot.LastSeqFrame = frame; }
        bool fresh = slot.SampleActive && frame - slot.LastSeqFrame <= HandAimPolicy.FreshTicks;
        bool hasContainer = slot.Container != null;
        Vector3 candidateRest = hasContainer ? slot.Container!.localPosition : default;
        if (hasContainer && HandAimPolicy.ShouldCaptureRest(slot.RestCaptured, slot.SharedOrAmbiguous,
            candidateRest.x, candidateRest.y, candidateRest.z))
        {
            slot.RestLocalRotation = HandAimMath.Relative(RestCaptureFrame(poseBaseRotation, pose, slot.SampleActive),
                Rotation(slot.Container!.rotation));
            slot.RestCapturedWithPose = slot.SampleActive;
            slot.RestLocalPosition = candidateRest;
            slot.RestLocalUnityRotation = slot.Container.localRotation;
            slot.RestCaptured = true;
            slot.LastSeqFrame = frame;
        }
        // The live local pose changes when gaze preserves a driven root's WORLD pose.
        // Keep the unchanged 10-unit guard on captured authored rest, not our own driven output.
        bool restPlausible = !hasContainer || (slot.RestCaptured && HandAimMath.RestPlausible(slot.RestLocalPosition.x,
            slot.RestLocalPosition.y, slot.RestLocalPosition.z, HandAimPolicy.MaxRestLocalDistance));
        slot.State = HandAimPolicy.Next(hasContainer, slot.SharedOrAmbiguous, restPlausible, fresh, frame - slot.LastSeqFrame);
        if (slot.State != HandAimState.Driving && slot.State != HandAimState.HoldingLastPose) return;
        var container = slot.Container!;
        if (slot.HasWritten && ((container.position - slot.LastWrittenPosition).sqrMagnitude > 1e-6f ||
            Quaternion.Angle(container.rotation, slot.LastWrittenRotation) > 0.05f)) slot.GameOverwrites++;
        if (slot.State == HandAimState.HoldingLastPose)
        {
            if (slot.HasWritten) container.SetPositionAndRotation(slot.LastWrittenPosition, slot.LastWrittenRotation);
            return;
        }
        HandAimMath.ToWorld(new[] { origin.x, origin.y, origin.z }, Rotation(poseBaseRotation),
            new HandSample(true, pose.X, pose.Y, pose.Z, pose.Qx, pose.Qy, pose.Qz, pose.Qw), out float[] position, out float[] handRotation);
        float[] drive = HandAimMath.DriveRotation(handRotation, slot.RestLocalRotation);
        container.SetPositionAndRotation(new Vector3(position[0], position[1], position[2]),
            new Quaternion(drive[0], drive[1], drive[2], drive[3]));
        // D-095: make the muzzle (what the reticle shows and what the fire call aims) point exactly along the controller.
        var alignMuzzle = FirstMuzzle(container);
        if (alignMuzzle != null)
        {
            float[] hf = HandAimMath.Forward(handRotation);
            var want = new Vector3(hf[0], hf[1], hf[2]);
            if (want.sqrMagnitude > 0.5f)
            {
                container.rotation = Quaternion.FromToRotation(alignMuzzle.forward, want) * container.rotation;
                // D-153 (TOOL-073): FromToRotation is a minimal-arc swing, so it pins the muzzle direction and leaves the
                // weapon's roll about that direction to the captured rest pose - which is what the left-hand weapon angle
                // report is about, and why the two hands can differ while both muzzles are exactly on the controller.
                // Measure that residual (weapon up vs controller up about the muzzle axis), report it, then apply the
                // per-hand dial about the same axis. The axis is the muzzle's world forward, so the dial cannot move it.
                float[] hu = HandAimMath.Up(handRotation);
                slot.LastTwistResidualDeg = HandAimMath.TwistDegrees(HandAimMath.Up(Rotation(container.rotation)), hu, hf);
                KeepAbs(slot.LastTwistResidualDeg, ref slot.MaxTwistResidualAbsDeg);
                float twist = HandAimPolicy.ClampWeaponTwist(slot == left ? LeftWeaponTwistDeg : RightWeaponTwistDeg);
                slot.LastAppliedTwistDeg = twist;
                if (twist != 0f)
                {
                    float[] twisted = HandAimMath.TwistAbout(Rotation(container.rotation), hf, twist);
                    container.rotation = new Quaternion(twisted[0], twisted[1], twisted[2], twisted[3]);
                }
            }
        }
        slot.LastWrittenPosition = container.position;
        slot.LastWrittenRotation = container.rotation;
        slot.HasWritten = true;
        slot.DrivenFrames++;
        var muzzle = FirstMuzzle(container);
        if (muzzle != null)
        {
            var forward = muzzle.forward;
            slot.MuzzlePath = muzzle.name;
            slot.MuzzleToHandAngle = HandAimMath.AngleDegrees(new[] { forward.x, forward.y, forward.z }, HandAimMath.Forward(handRotation));
        }
    }

    private static Transform? FirstMuzzle(Transform container)
    {
        foreach (var weapon in container.GetComponentsInChildren<Weapon>(false))
            if (weapon != null && weapon.muzzle_flash_spawn != null) return weapon.muzzle_flash_spawn;
        return null;
    }

    private static float[] Rotation(Quaternion q) => new[] { q.x, q.y, q.z, q.w };

    // D-202 (BUG-202): the rotation the container keeps, captured in the frame the pose is driven in AND with the pose
    // that is live at this instant. Relative to the game camera alone (pre-D-202) the container kept a rotation the
    // view does not have - the reported 90 deg hand - and the hand's absolute orientation was never divided out, so a
    // weapon also jumped the moment the driver took it over. `active` false means no valid tracked pose existed in that
    // frame: the frame is then used without a pose (never a zero quaternion).
    private static float[] RestCaptureFrame(Quaternion poseBaseRotation, NativeHandPose pose, bool active)
    {
        float[] frame = Rotation(poseBaseRotation);
        return active
            ? HandAimMath.PoseRotation(frame, new HandSample(true, pose.X, pose.Y, pose.Z, pose.Qx, pose.Qy, pose.Qz, pose.Qw))
            : frame;
    }

    // D-153: running worst case of |value|; a non-finite sample is ignored and a NaN maximum is replaced, never kept.
    private static void KeepAbs(float value, ref float max)
    {
        if (!float.IsFinite(value)) return;
        float abs = MathF.Abs(value);
        if (!(abs <= max)) max = abs;
    }

    // Stop driving and hand each container back to the game at its captured rest pose.
    public void Release()
    {
        ReleaseFlameEmitterPin();
        ReleaseFlameEndpoint();
        ReleaseFireBestEffort();
        ReleaseWeaponRuntime();
        InputRelease();
        Shot?.Remove();
        ReleaseShotFrames();
        UiRelease();
        MarkersRelease(); // D-181: restored (see MarkersTick)
        ReleaseRenderTiming(); // D-178 recovery repair: the diagnostic's patches/writer were never torn down
        DestroyReticles();
        foreach (var slot in new[] { left, right })
        {
            if (slot.Container != null && slot.RestCaptured && slot.HasWritten)
                slot.Container.SetLocalPositionAndRotation(slot.RestLocalPosition, slot.RestLocalUnityRotation);
            slot.HasWritten = false;
            slot.State = HandAimState.ReleasedInactive;
        }
    }

    private void Fail(string message)
    {
        nativeFailures++;
        lastError = message;
    }

    private void WriteReceipt(string scene, bool available, NativeHandsStatus status)
    {
        object Describe(HandSlot s) => new
        {
            hand = s.Name, slot = s.Slot.ToString(), state = s.State.ToString(), weaponsInSlot = s.WeaponCount,
            container = s.Container == null ? null : s.Container.name, sharedOrAmbiguous = s.SharedOrAmbiguous,
            restCaptured = s.RestCaptured, restCapturedWithPose = s.RestCapturedWithPose, hasWritten = s.HasWritten,
            capturedRestLocal = new[] { FiniteOrNull(s.RestLocalPosition.x), FiniteOrNull(s.RestLocalPosition.y), FiniteOrNull(s.RestLocalPosition.z) },
            currentLocal = s.Container == null ? null : new[] { FiniteOrNull(s.Container.localPosition.x), FiniteOrNull(s.Container.localPosition.y), FiniteOrNull(s.Container.localPosition.z) },
            currentWorld = s.Container == null ? null : new[] { FiniteOrNull(s.Container.position.x), FiniteOrNull(s.Container.position.y), FiniteOrNull(s.Container.position.z) },
            samplesActive = s.SampleActive, trigger = s.Trigger, faceUpper = s.Face,
            fireCalls = s.FireCalls, releaseCalls = s.ReleaseCalls, triggerPresses = s.Presses, heldSeconds = s.HeldSeconds, fireHeld = s.FireHeld,
            reticleVisible = s.ReticleVisible, reticleDistance = s.ReticleDistance, reticleHitSurface = s.ReticleHit,
            drivenFrames = s.DrivenFrames, gameOverwrites = s.GameOverwrites, muzzle = s.MuzzlePath,
            muzzleToHandForwardDegrees = float.IsNaN(s.MuzzleToHandAngle) ? (float?)null : s.MuzzleToHandAngle,
            // D-153 (TOOL-073): the roll about the muzzle axis. `weaponTwistResidualDegrees` is the measured gap
            // between the weapon's up and the controller's up (null when the axis is degenerate, never a silent 0);
            // `weaponTwistAppliedDegrees` is the dial value this sitting ran with.
            weaponTwistResidualDegrees = FiniteOrNull(s.LastTwistResidualDeg),
            maxWeaponTwistResidualAbsDegrees = FiniteOrNull(s.MaxTwistResidualAbsDeg),
            weaponTwistAppliedDegrees = s.LastAppliedTwistDeg,
        };
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(new
        {
            verdict = "HAND_AIM_SOFT_GATE_NOT_HEADSET_VERIFIED", scene, updatedUtc = DateTime.UtcNow.ToString("o"),
            processId = Environment.ProcessId, controllersAvailable = available, nativeSeq = status.Seq,
            nativeFailures, lastError, left = Describe(left), right = Describe(right),
            flameEndpointVfx = FlameEndpointReceipt(),
            flameEmitterPin = FlameEmitterPinReceipt(),
            worldMarkers = MarkerReceipt(), // D-181: restored
            renderTiming = RenderTimingReceipt(), // D-136/D-138 (restored, BUG-205)
            fireCalls = left.FireCalls + right.FireCalls, fireFailed, fireError, camAimed = CamAimed, linkedAim = false,
            weaponRuntime = new { stockFireCadence = StockFireCadence, autonomousHeadAim = AutonomousHeadAim,
                gazeWrites = GazeWrites, nativeWeaponUpdates = NativeWeaponUpdates, heldWeaponUpdates = HeldWeaponUpdates, error = WeaponRuntimeError,
                acceptance = "NOT_HEADSET_VERIFIED" },
            ui = new { enabled = UiEnabled, hideHudBackdrop = HideHudBackdrop, gameBackdropMode = GameBackdropMode, verticalOffsetMeters = UiVerticalOffset, persistentBackdropCount = hudBackdropOriginal.Count, converted = UiConverted, clicks = UiClicks, hovers = UiHovers, lastClick = UiLastClick, error = UiError, canvases = uiCanvases.Count, dimmed = uiDimmed, crosshairHidden = CrosshairHidden, hudLocked = HudLockedCanvases, hudDarkHidden = HudDarkHidden, hudDarkCandidates = hudDarkLog, backdropHidden = BackdropHidden, backdropCandidates = uiBackdropLog, flattenFound = FlattenFound, flattenBillboards = FlattenBillboards, flattened = flatLog, levelPlacement = levelLog,
                renderPlacement = new { passes = UiRenderPasses, corrections = UiRenderCorrections, maxDriftBeforeMeters = FiniteOrNull(UiMaxDriftBefore), maxErrorAfterMeters = FiniteOrNull(UiMaxErrorAfter), error = UiRenderError } },
            haptic = new { enabled = HapticEnabled, amplitude = HapticAmplitude, milliseconds = HapticMilliseconds, pulsesLeft = HapticPulsesLeft, pulsesRight = HapticPulsesRight, rejected = HapticRejected, error = HapticError },
            shot = Shot == null ? null : new { overrideArg = Shot.OverrideArg, shootCalls = Shot.ShootCalls, leftShootCalls = Shot.LeftShootCalls, rightShootCalls = Shot.RightShootCalls, leftShotsWithoutHeld = Shot.LeftShotsWithoutHeld, rightShotsWithoutHeld = Shot.RightShotsWithoutHeld, argNull = Shot.ArgNull, argCamera = Shot.ArgCamera, argCameraChild = Shot.ArgCameraChild, argOther = Shot.ArgOther, overrides = Shot.Overrides, lastArg = Shot.LastArg, tracers = Shot.Tracers, tracerNearMuzzle = Shot.TracerNearMuzzle, tracerNearCamera = Shot.TracerNearCamera, lastTracerToMuzzleDeg = float.IsFinite(Shot.LastTracerToMuzzle) ? Shot.LastTracerToMuzzle : (float?)null, lastTracerToCameraDeg = float.IsFinite(Shot.LastTracerToCamera) ? Shot.LastTracerToCamera : (float?)null, tracerPatched = Shot.TracerPatched, cameraAimedShots = Shot.CameraAimedShots, lastCameraToMuzzleDeg = FiniteOrNull(Shot.LastCameraToMuzzleDeg), maxCameraToMuzzleDeg = Shot.MaxCameraToMuzzleDeg, maxTracerToMuzzleDeg = Shot.MaxTracerToMuzzle, ammo = new { weapons = Shot.AmmoWeapons, first = FiniteOrNull(Shot.FirstAmmo), last = FiniteOrNull(Shot.LastAmmo), maxAmmo = FiniteOrNull(Shot.WeaponMaxAmmo), fireRate = FiniteOrNull(Shot.WeaponFireRate), drops = Shot.AmmoDrops, dropTotal = Shot.AmmoDropTotal, maxDrop = Shot.MaxAmmoDrop, maxShootsPerFrame = Shot.MaxShootsPerFrame }, error = Shot.Error },
            shotScope = new { calls = ShotScopeCalls, depth = ShotScopeDepth, maxDepth = ShotScopeMaxDepth, failures = ShotScopeFailures, error = ShotScopeError,
                proxies = shotFrames.Count, missingFrames = Shot?.MissingFrames, alignmentSamples = Shot?.AlignmentSamples,
                maxPostMuzzleToAimDeg = Shot?.MaxPostMuzzleToAim, maxPostOriginDriftMeters = Shot?.MaxPostOriginDrift,
                flameSamples = Shot?.FlameSamples, flameError = Shot?.FlameError, attackDirectionVerified = false },
            flameFx = Shot == null ? null : new { frames = Shot.FlameFxFrames, vfxPlayToggles = Shot.VfxPlayToggles, vfxCountResets = Shot.VfxCountResets,
                minVfxParticles = Shot.FlameFxFrames == 0 ? (int?)null : Shot.MinVfxParticles, maxVfxParticles = Shot.MaxVfxParticles,
                lastVfxPlaying = Shot.LastVfxPlaying, flamesActive = Shot.FlamePoolActive, flamesWithVfx = Shot.FlamePoolVfxActive,
                projectileV2Active = Shot.FlameProjV2Active, instancesSeenInSamples = Shot.FlamePoolSeen, error = Shot.FlameFxError,
                note = "Read-only flame/ParticleSystem sampling for TOOL-070: no fire or effect entry point is called and no field, transform or ParticleSystem is written." },
            poseTrace = new { left = leftPose.Receipt(), right = rightPose.Receipt(),
                note = "Read-only rotation trace for TOOL-071 (left-weapon spin). It reads transform rotation/position/name and the last written container rotation only; no transform, object, field, ParticleSystem or weapon/effect entry point is written." },
            input = new { enabled = InputEnabled, moveCalls = MoveCalls, menuPresses = MenuPresses, jumpPresses = JumpPresses, menuBlockedByLevelUp = MenuBlockedByLevelUp, interactPresses = InteractPresses, reloadPresses = ReloadPresses, facePresses = FacePresses, minFacePressHeadDistance = FiniteOrNull(MinFacePressHeadDistance), lastFacePressHeadDistance = FiniteOrNull(LastFacePressHeadDistance), specialPresses = SpecialPresses, bloodPresses = BloodPresses, armLPresses = ArmLPresses, armRPresses = ArmRPresses, pad = pad != null, padSent = PadSent, padError = PadError, padNote = PadNote, scheme = InputScheme(), moveYawDelta = LastMoveYawDelta, error = InputError, snapDegrees = SnapDegrees, snapTurns = SnapTurns, snapTotalDegrees = SnapTotal, turnMode = TurnMode, smoothFrames = SmoothFrames, viewTurnedDegrees = ViewTurnedDegrees,
                turret = new { cycle = TurretCycle, modeFrames = TurretModeFrames, stepsBack = TurretStepsBack, stepsForward = TurretStepsForward, confirms = TurretConfirms, confirmHeld = turretConfirmWas, error = TurretModeError,
                    note = "D-206 (BUG-204): the engineer's build-mode grips. modeFrames counts frames the game itself reported is_turret_mode; a step is one grip released outside a chord; both grips (chord) hold the game's Fire action. Acceptance is a headset observation (docs\\playtests\\PLAYTEST-D204-TURRET-KO.md)." } },
            receiptError,
            note = "Containers are driven per hand independently; the trigger calls the game fire path only when HandFire is on. The reticle is the muzzle-forward ray, not a proof of the bullet path. No shot-direction, " +
                   "reload, 14-bank or aim-assist behavior is verified. gameOverwrites counts frames where the game moved a driven container.",
        }, json));
    }
}