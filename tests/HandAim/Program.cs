using VampireHuntersVR.Interaction;
using VampireHuntersVR.Render;

int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
    passed++;
}
void Throws(string name, Action action)
{
    try { action(); } catch (ArgumentException) { Console.WriteLine("PASS " + name); passed++; return; }
    throw new InvalidOperationException(name + " (expected rejection missing)");
}
bool Near(float a, float b, float tol = 1e-4f) => MathF.Abs(a - b) <= tol;
bool NearV(float[] a, float[] b, float tol = 1e-4f) => a.Length == b.Length && a.Zip(b).All(p => Near(p.First, p.Second, tol));
float[] Identity = { 0, 0, 0, 1 };
float[] YawQuat(float degrees) { float h = degrees * MathF.PI / 360f; return new[] { 0f, MathF.Sin(h), 0f, MathF.Cos(h) }; }
// OpenXR quaternion for a rotation about +Y (up) is unaffected by the handedness flip except sign of rotation.
HandSample Pose(float x, float y, float z, float qx = 0, float qy = 0, float qz = 0, float qw = 1) => new(true, x, y, z, qx, qy, qz, qw);

// --- coordinate transform: must equal the eye-camera mapping (OpenXR -Z forward -> Unity +Z forward)
HandAimMath.ToWorld(new float[] { 0, 0, 0 }, Identity, Pose(0.1f, 1.2f, -0.5f), out var p0, out var r0);
Check("position-flips-z", NearV(p0, new[] { 0.1f, 1.2f, 0.5f }));
Check("identity-aim-forward-is-unity-plus-z", NearV(HandAimMath.Forward(r0), new[] { 0f, 0f, 1f }));
HandAimMath.ToWorld(new float[] { 5, 1, -2 }, Identity, Pose(0, 0, -1), out var p1, out _);
Check("origin-offset-applied", NearV(p1, new[] { 5f, 1f, -1f }));
HandAimMath.ToWorld(new float[] { 0, 0, 0 }, YawQuat(90), Pose(0, 0, -1), out var p2, out var r2);
Check("anchor-yaw-rotates-position", NearV(p2, new[] { 1f, 0f, 0f }));
Check("anchor-yaw-rotates-forward", NearV(HandAimMath.Forward(r2), new[] { 1f, 0f, 0f }));

// --- independence: two hands with different poses produce unrelated, individually correct aim rays
HandAimMath.ToWorld(new float[] { 0, 0, 0 }, Identity, Pose(-0.2f, 1f, -0.3f, 0, MathF.Sin(MathF.PI / 4), 0, MathF.Cos(MathF.PI / 4)), out _, out var leftRot);
HandAimMath.ToWorld(new float[] { 0, 0, 0 }, Identity, Pose(0.2f, 1f, -0.3f), out _, out var rightRot);
float[] lf = HandAimMath.Forward(leftRot), rf = HandAimMath.Forward(rightRot);
Check("independent-hands-diverge", HandAimMath.AngleDegrees(lf, rf) > 80f);
Check("right-hand-unaffected-by-left-pose", NearV(rf, new[] { 0f, 0f, 1f }));
Check("left-yaw-90-points-sideways", MathF.Abs(MathF.Abs(lf[0]) - 1f) < 1e-3f);

// --- rest orientation: container keeps its authored offset relative to the aim frame
float[] rest = YawQuat(10);
float[] driven = HandAimMath.DriveRotation(r0, rest);
Check("rest-offset-preserved", Near(HandAimMath.AngleDegrees(HandAimMath.Forward(driven), HandAimMath.Forward(r0)), 10f, 0.05f));
float[] roundTrip = HandAimMath.Relative(Identity, YawQuat(25));
Check("relative-to-identity-parent", NearV(roundTrip, YawQuat(25)));
float[] childOfParent = HandAimMath.Relative(YawQuat(30), YawQuat(50));
Check("relative-removes-parent-yaw", NearV(childOfParent, YawQuat(20), 1e-3f));
Check("conjugate-inverts", NearV(HandAimMath.Relative(YawQuat(40), YawQuat(40)), Identity, 1e-4f));

// --- D-202 (BUG-202): the hand/aim frame. Until 0.9.4 the eye cameras were composed with identity (D-195) while the
// hands kept multiplying the game camera rotation in. That camera is the player here, so its +-90 deg yaw is exactly
// the "hand bent 90 deg to the right" report: the driver believed the controller pointed 90 deg away from the view,
// and the muzzle alignment then pinned the weapon to that wrong direction (receipt showed muzzleToHand = 0).
float[] handNow = YawQuat(40);                                                   // the controller, head frame (what the view sees)
float[] handPoseAnchor = StereoMath.MultiplyQuaternion(YawQuat(90), handNow);    // pre-D-202: what the driver composed
Check("d202-anchor-frame-controller-direction-is-90-degrees-off-the-view",
    Near(HandAimMath.AngleDegrees(HandAimMath.Forward(handPoseAnchor), HandAimMath.Forward(handNow)), 90f, 0.05f));
Check("d202-head-frame-controller-direction-is-the-view",
    Near(HandAimMath.AngleDegrees(HandAimMath.Forward(StereoMath.MultiplyQuaternion(Identity, handNow)), HandAimMath.Forward(handNow)), 0f, 1e-3f));
float[] authoredContainer = YawQuat(10);
float[] restHead = HandAimMath.Relative(handNow, authoredContainer);
Check("d202-rest-capture-divides-out-the-capture-pose", NearV(restHead, YawQuat(-30f), 1e-3f));
Check("d202-head-frame-takeover-keeps-the-authored-rotation",
    Near(HandAimMath.AngleDegrees(HandAimMath.Forward(HandAimMath.DriveRotation(handNow, restHead)), HandAimMath.Forward(authoredContainer)), 0f, 0.05f));
Check("d202-frame-only-rest-capture-omits-the-pose", !NearV(HandAimMath.Relative(Identity, authoredContainer), restHead, 0.05f));
HandSample poseUnderTest = Pose(0, 0, 0, 0f, MathF.Sin(MathF.PI / 9f), 0f, MathF.Cos(MathF.PI / 9f));
Check("d202-pose-rotation-is-the-base-times-the-pose",
    NearV(HandAimMath.PoseRotation(YawQuat(25), poseUnderTest),
        StereoMath.MultiplyQuaternion(YawQuat(25), HandAimMath.PoseRotation(Identity, poseUnderTest))));
Throws("d202-pose-rotation-rejects-an-inactive-hand", () => HandAimMath.PoseRotation(Identity, default));
Throws("d202-pose-rotation-rejects-a-bad-base", () => HandAimMath.PoseRotation(new float[] { 0, 0 }, Pose(0, 0, 0)));

// --- rejection paths
Throws("reject-inactive-hand", () => HandAimMath.ToWorld(new float[] { 0, 0, 0 }, Identity, default, out _, out _));
Throws("reject-bad-quaternion", () => HandAimMath.ToWorld(new float[] { 0, 0, 0 }, Identity, Pose(0, 0, 0, 0, 0, 0, 2f), out _, out _));
Throws("reject-nan-position", () => HandAimMath.ToWorld(new float[] { 0, 0, 0 }, Identity, Pose(float.NaN, 0, 0), out _, out _));
Throws("reject-origin-dimensions", () => HandAimMath.ToWorld(new float[] { 0, 0 }, Identity, Pose(0, 0, 0), out _, out _));
Throws("reject-zero-direction", () => HandAimMath.AngleDegrees(new float[] { 0, 0, 0 }, new float[] { 0, 0, 1 }));
Check("angle-opposites-180", Near(HandAimMath.AngleDegrees(new float[] { 0, 0, 1 }, new float[] { 0, 0, -1 }), 180f, 0.01f));

// --- rest plausibility
Check("rest-near-origin-ok", HandAimMath.RestPlausible(0.3f, -0.2f, 0.5f, HandAimPolicy.MaxRestLocalDistance));
Check("rest-far-rejected", !HandAimMath.RestPlausible(100f, 0, 0, HandAimPolicy.MaxRestLocalDistance));
Check("rest-nan-rejected", !HandAimMath.RestPlausible(float.NaN, 0, 0, HandAimPolicy.MaxRestLocalDistance));
Check("rest-infinity-rejected", !HandAimMath.RestPlausible(0, float.PositiveInfinity, 0, HandAimPolicy.MaxRestLocalDistance));

// --- state policy: fail closed, hands never interact
Check("no-container-waits", HandAimPolicy.Next(false, false, true, true, 0) == HandAimState.WaitingForWeapons);
Check("shared-container-rejected", HandAimPolicy.Next(true, true, true, true, 0) == HandAimState.RejectedSharedContainer);
Check("implausible-rest-rejected", HandAimPolicy.Next(true, false, false, true, 0) == HandAimState.RejectedImplausibleRest);
Check("fresh-sample-drives", HandAimPolicy.Next(true, false, true, true, 0) == HandAimState.Driving);
Check("stale-short-holds-pose", HandAimPolicy.Next(true, false, true, false, 5) == HandAimState.HoldingLastPose);
Check("stale-long-releases", HandAimPolicy.Next(true, false, true, false, HandAimPolicy.ReleaseAfterTicks) == HandAimState.ReleasedInactive);
Check("shared-wins-over-fresh-sample", HandAimPolicy.Next(true, true, true, true, 0) != HandAimState.Driving);
Check("per-hand-states-independent",
    HandAimPolicy.Next(true, false, true, true, 0) == HandAimState.Driving &&
    HandAimPolicy.Next(false, false, true, false, 999) == HandAimState.WaitingForWeapons);

// --- static source guards (labelled: not live Unity execution)
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string driver = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandAimDriver.cs"));
string host = File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHost.cs")) + "\n" + File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHostConfig.cs"));
string inputSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandInput.cs"));
string handAimSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandAim.cs"));
string readiness = File.ReadAllText(Path.Combine(root, "tests", "verify-handaim-ready.ps1"));
string schema = File.ReadAllText(Path.Combine(root, "config", "schema.json"));
// The 0.6-final preview payload used to live under dist\ (now ignored/pruned); it is kept at the project root.
string previewInstallPath = Path.Combine(root, "dist", "VampireHuntersVR-0.6-final", "install.ps1");
if (!File.Exists(previewInstallPath)) previewInstallPath = Path.Combine(root, "VampireHuntersVR-0.6-final", "install.ps1");
if (!File.Exists(previewInstallPath)) previewInstallPath = Path.Combine(root, "installer", "install.ps1"); // source-only copy: the shipped installer carries the same marker defaults
string previewInstall = File.ReadAllText(previewInstallPath);
WorldMarkerPolicy.TryParseKinds(WorldMarkerPolicy.DefaultKinds, out var defaultKinds);
string native = File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Host.cpp"));
string hands = File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Hands.cpp"));
string fireSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandFireTrigger.cs"));
string reticleSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandReticle.cs"));
Check("source-driver-core-never-fires-weapons", !driver.Contains(".fire(") && !driver.Contains(".shoot_forward(") &&
    !driver.Contains("fire_secondary_shot") && !driver.Contains(".reload(") && !hands.Contains("keybd_event"));
Check("source-no-hand-link", !driver.Contains("TwoHand") && !driver.Contains("support") && driver.Contains("linkedAim = false"));
Check("source-per-slot-ownership", driver.Contains("weapon.current_weapon_slot") && driver.Contains("EWeaponSlot.LEFT_HAND") && driver.Contains("EWeaponSlot.RIGHT_HAND"));
Check("source-shared-container-fails-closed", driver.Contains("own.Overlaps(other)") && driver.Contains("own.Count > 1"));
Check("source-hand-opt-in-requires-gameplay", host.Contains("public bool HandAim { get; set; }") && host.Contains("HandAim requires GameplayOnly"));
Check("source-hand-failure-isolated", host.Contains("HAND_AIM disabled") && host.Contains("handAim.Release()"));
Check("source-native-hands-isolated", native.Contains("HANDS DISABLED") && native.Contains("HANDS SYNC DISABLED"));
Check("source-hands-enabled-before-init", host.IndexOf("HandAimNative.EnableHands(1)", StringComparison.Ordinal) < host.IndexOf("GL.IssuePluginEvent(callback, 0)", StringComparison.Ordinal));
Check("source-touch-profile-no-private-api", hands.Contains("/interaction_profiles/oculus/touch_controller") && !hands.Contains("XR_EXT_conformance_automation"));
// --- trigger fire policy (pure): hysteresis, edges, and always-release when not allowed
Check("fire-below-press-threshold-none", HandFirePolicy.Next(false, 0.4f, true, out bool h0) == TriggerEdge.None && !h0);
Check("fire-press-edge", HandFirePolicy.Next(false, 0.8f, true, out bool h1) == TriggerEdge.Pressed && h1);
Check("fire-held-no-new-edge", HandFirePolicy.Next(true, 0.5f, true, out bool h2) == TriggerEdge.None && h2);
Check("fire-hysteresis-keeps-hold", HandFirePolicy.Next(true, 0.40f, true, out bool h3) == TriggerEdge.None && h3);
Check("fire-release-edge", HandFirePolicy.Next(true, 0.1f, true, out bool h4) == TriggerEdge.Released && !h4);
Check("fire-not-allowed-releases-held", HandFirePolicy.Next(true, 1f, false, out bool h5) == TriggerEdge.Released && !h5);
Check("fire-not-allowed-never-presses", HandFirePolicy.Next(false, 1f, false, out bool h6) == TriggerEdge.None && !h6);
Check("fire-nan-releases", HandFirePolicy.Next(true, float.NaN, true, out bool h7) == TriggerEdge.Released && !h7);
Check("fire-hands-independent", HandFirePolicy.Next(false, 0.9f, true, out _) == TriggerEdge.Pressed && HandFirePolicy.Next(false, 0.0f, true, out _) == TriggerEdge.None);
Check("fire-thresholds-ordered", HandFirePolicy.ReleaseAt < HandFirePolicy.PressAt);
// --- static source guards for fire and reticle (not live Unity execution)
Check("source-fire-opt-in-requires-handaim", host.Contains("public bool HandFire { get; set; }") && host.Contains("HandFire requires HandAim") && host.Contains("Config.HandFire"));
Check("source-fire-per-slot-and-released-on-teardown", fireSrc.Contains("player.fire(slot.Slot, ref pressed") && fireSrc.Contains("player.fire_release(slot.Slot, ref released") && fireSrc.Contains("ReleaseFireBestEffort") && driver.Contains("ReleaseFireBestEffort();"));
Check("source-fire-no-input-synthesis-no-reload", !fireSrc.Contains("keybd_event") && !fireSrc.Contains("mouse_event") && !fireSrc.Contains("SendInput") && !fireSrc.Contains("reload") && !fireSrc.Contains("fire_secondary_shot") && !fireSrc.Contains("shoot_forward"));
Check("source-fire-blocked-while-paused-or-face-held", fireSrc.Contains("Time.timeScale > 0.01f") && fireSrc.Contains("lock_shoot_input") && fireSrc.Contains("!slot.Face"));
Check("source-reticle-collider-free-and-cleaned", reticleSrc.Contains("collider.enabled = false") && reticleSrc.Contains("DestroyReticles") && driver.Contains("DestroyReticles();") && reticleSrc.Contains("QueryTriggerInteraction.Ignore"));

string installer = File.ReadAllText(Path.Combine(root, "src", "core", "Install-Stage0.ps1"));
string drill = File.ReadAllText(Path.Combine(root, "tests", "headset-handaim.ps1"));
Check("source-headset-requires-handaim-gameplay-vdxr", host.Contains("public bool Headset { get; set; }") && host.Contains("invalid headset config: needs GameplayOnly (or MenuFirst), HandAim") && host.Contains("ConfigureHeadset(Config.HeadsetSeconds)") && host.IndexOf("ConfigureHeadset(Config.HeadsetSeconds)", StringComparison.Ordinal) < host.IndexOf("HostNative.Configure(", StringComparison.Ordinal));
Check("source-native-runtime-gate-explicit", native.Contains("RuntimeNameAllowed(") && native.Contains("headsetAllowed=true;host.budgetSeconds") && File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Host.h")).Contains("if(name==\"Meta XR Simulator\") return true;") && File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Host.h")).Contains("return headsetAllowed;"));
Check("source-installer-headset-runtime-flexible-with-flag", installer.Contains("$runtimeSpecified") && installer.Contains("$isHeadset") && installer.Contains("Headset config needs GameplayOnly, HandAim and HeadsetSeconds 0 (unlimited) or 60-900."));
// D-145: any registered OpenXR runtime (not just Virtual Desktop) and an unlimited (0) session budget.
Check("source-d145-runtime-autodetect-registry", native.Contains("RegGetValueA(HKEY_LOCAL_MACHINE") && native.Contains("runtime[0]&&!SetEnvironmentVariableA") && native.Contains("no active OpenXR runtime registered") && host.Contains("auto: machine ActiveRuntime / loader default"));
Check("source-d145-unlimited-session-and-stall-watchdog", host.Contains("TimeSpan.FromSeconds(60.0)") && host.Contains("lastFrameProgress = DateTime.UtcNow") && host.Contains("host render stall watchdog") && native.Contains("if(budgetSeconds>0&&Clock::now()-start>std::chrono::seconds(budgetSeconds))") && native.Contains("budgetSeconds<0||(budgetSeconds>0&&(budgetSeconds<60||budgetSeconds>900))") && host.Contains("HeadsetSeconds 0 (unlimited) or 60-900"));
// D-146: a stage that ends (camera/player torn down or gameplay scene left) closes the sitting as a normal end, not a failure.
Check("source-d146-stage-end-closes-orderly", host.Contains("private bool StageEnded()") && host.Contains("private void EndGameplay(NativeHostStatus status)") && host.Contains("HEADSET_GAMEPLAY_LEFT_NOT_ACCEPTED") && host.Contains("EndGameplay(status)") && host.Contains("GAMEPLAY_ENDED"));
Check("source-d146-end-reason-in-receipt", host.Contains("private string endReason = \"running\"") && host.Contains("endReason = endReason") && host.Contains("endReason = \"level-exit\"") && host.Contains("endReason = \"stop-request\"") && host.Contains("endReason = \"session-ended\"") && host.Contains("e is TimeoutException) ? \"watchdog\" : \"error\""));
Check("source-d146-previous-run-archived", host.Contains("private void ResetRunFolder()") && host.Contains("RUN_FOLDER previous receipts archived") && host.Contains("stale stop.request removed") && host.Contains("ResetRunFolder();") && host.Contains("\"previous\""));
// --- D-147: per-eye resolution preset (scales the configured base per eye, keeping the compositor's eye aspect ratio)
Check("resolution-normal-is-configured-per-eye", RenderResolutionPolicy.EyeSize(2064, 2208, "normal") == (2064, 2208) && Near((float)RenderResolutionPolicy.Scale("normal"), 1f));
Check("resolution-low-is-0.8-per-eye", RenderResolutionPolicy.EyeSize(2064, 2208, "low") == (1651, 1766));
Check("resolution-high-is-1.2-per-eye", RenderResolutionPolicy.EyeSize(2064, 2208, "high") == (2477, 2650));
Check("resolution-custom-keeps-explicit-size", RenderResolutionPolicy.EyeSize(1920, 2160, "custom") == (1920, 2160));
Check("resolution-clamps-to-device-limits", RenderResolutionPolicy.EyeSize(8192, 100, "high") == (4096, 512) && RenderResolutionPolicy.Dimension(4096, "high") == 4096 && RenderResolutionPolicy.Dimension(500, "low") == 512);
Check("resolution-rejects-unknown-preset", !RenderResolutionPolicy.IsValid("ultra") && !RenderResolutionPolicy.IsValid(null) && !RenderResolutionPolicy.IsValid("") && RenderResolutionPolicy.IsValid("low") && RenderResolutionPolicy.IsValid("normal") && RenderResolutionPolicy.IsValid("high") && RenderResolutionPolicy.IsValid("custom"));
Check("source-resolution-preset-wired", host.Contains("public string EyePreset { get; set; } = \"normal\";") && host.Contains("RenderResolutionPolicy.EyeSize(Config.EyeWidth, Config.EyeHeight, Config.EyePreset") && host.Contains("invalid eye preset: low|normal|high|custom") && host.Contains("eyePreset = Config.EyePreset") && File.ReadAllText(Path.Combine(root, "config", "schema.json")).Contains("\"EyePreset\""));
// --- D-170: eye image MSAA. The eye render target is the only place anti-aliasing can reach the submitted image (D-147









// --- D-100/D-114: right stick up/down as held buttons (Bloody Rush / Special Ability), game-asset gamepad mapping
Check("stick-latch-push-presses", StickLatchPolicy.Next(false, 0.9f, 0.1f, true));
Check("stick-latch-below-press-ignored", !StickLatchPolicy.Next(false, 0.5f, 0f, true));
Check("stick-latch-hysteresis-holds", StickLatchPolicy.Next(true, 0.5f, 0f, true) && !StickLatchPolicy.Next(true, 0.3f, 0f, true));
Check("stick-latch-diagonal-snap-does-not-fire", !StickLatchPolicy.Next(false, 0.75f, 0.95f, true));
Check("stick-latch-not-allowed-releases", !StickLatchPolicy.Next(true, 1f, 0f, false));
Check("stick-latch-nan-ignored", !StickLatchPolicy.Next(false, float.NaN, 0f, true) && !StickLatchPolicy.Next(true, 1f, float.NaN, true) && !StickLatchPolicy.Next(false, float.PositiveInfinity, 0f, true));
Check("stick-latch-opposite-direction-does-not-fire", !StickLatchPolicy.Next(false, -0.9f, 0f, true));
string shotSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandShot.cs"));
string uiSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandUi.cs"));
Check("source-pad-bits-match-game-asset", inputSrc.Contains("BitNorth = 1u << 4") && inputSrc.Contains("BitEast = 1u << 5") && inputSrc.Contains("BitSouth = 1u << 6") && inputSrc.Contains("BitLeftStick = 1u << 8") && inputSrc.Contains("BitLeftShoulder = 1u << 10") && inputSrc.Contains("BitRightShoulder = 1u << 11") && inputSrc.Contains("BitStart = 1u << 12"));
Check("source-blood-on-stick-up-special-on-stick-down-both-routes", inputSrc.Contains("AbilityStickPolicy.Next(specialHeld, bloodHeld, rightHand.StickY, rightHand.StickX, alive)") && inputSrc.Contains("bool special = abilities.Special, blood = abilities.Blood;") && inputSrc.Contains("(special || specialStep ? BitEast : 0u)") && inputSrc.Contains("(blood ? BitNorth : 0u)") && inputSrc.Contains("inputController.SpecialAbilityInput(special || specialStep)") && inputSrc.Contains("inputController.BloodyRushInput(blood)"));
Check("ability-up-blood-only", AbilityStickPolicy.Next(false, false, 0.9f, 0f, true) == (false, true));
Check("ability-down-special-only", AbilityStickPolicy.Next(false, false, -0.9f, 0f, true) == (true, false));
Check("ability-centre-releases-both", AbilityStickPolicy.Next(true, true, 0f, 0f, true) == (false, false));
Check("ability-up-holds-through-hysteresis", AbilityStickPolicy.Next(false, true, 0.5f, 0f, true) == (false, true));
Check("ability-up-releases-at-boundary", AbilityStickPolicy.Next(false, true, 0.3f, 0f, true) == (false, false));
Check("ability-down-holds-through-hysteresis", AbilityStickPolicy.Next(true, false, -0.5f, 0f, true) == (true, false));
Check("ability-down-releases-at-boundary", AbilityStickPolicy.Next(true, false, -0.3f, 0f, true) == (false, false));
Check("ability-up-to-down-does-not-overlap", AbilityStickPolicy.Next(false, true, -0.9f, 0f, true) == (true, false));
Check("ability-down-to-up-does-not-overlap", AbilityStickPolicy.Next(true, false, 0.9f, 0f, true) == (false, true));
Check("ability-diagonal-turn-does-not-start-either", AbilityStickPolicy.Next(false, false, 0.75f, 0.95f, true) == (false, false) && AbilityStickPolicy.Next(false, false, -0.75f, -0.95f, true) == (false, false));
Check("ability-equal-axis-does-not-start-either", AbilityStickPolicy.Next(false, false, 0.9f, 0.9f, true) == (false, false));
Check("ability-nonfinite-releases-both", AbilityStickPolicy.Next(true, true, float.NaN, 0f, true) == (false, false) && AbilityStickPolicy.Next(true, true, float.PositiveInfinity, 0f, true) == (false, false) && AbilityStickPolicy.Next(true, true, 0.9f, float.NaN, true) == (false, false));
Check("ability-paused-held-releases", AbilityStickPolicy.Next(true, true, 0.9f, 0f, false) == (false, false));

// --- D-211 (BUG-203): snap turn is a real view turn with exactly three choices (off / 30 / 45)
Check("snap-turn-offers-exactly-three-choices",
    SnapTurnPolicy.Choices.Length == 3 && SnapTurnPolicy.IsChoice(0f) && SnapTurnPolicy.IsChoice(30f) && SnapTurnPolicy.IsChoice(45f));
Check("snap-turn-unlisted-value-resolves-to-thirty-not-silently",
    SnapTurnPolicy.Resolve(60f) == 30f && SnapTurnPolicy.Resolve(float.NaN) == 30f && SnapTurnPolicy.Resolve(-30f) == 30f);
Check("snap-turn-listed-values-pass-through",
    SnapTurnPolicy.Resolve(0f) == 0f && SnapTurnPolicy.Resolve(30f) == 30f && SnapTurnPolicy.Resolve(45f) == 45f);
Check("snap-turn-off-means-the-stick-does-nothing",
    SnapTurnPolicy.Next(true, 0.95f, SnapTurnPolicy.Resolve(0f), out bool offArmed) == 0f && offArmed);
Check("snap-turn-each-choice-steps-that-many-degrees",
    SnapTurnPolicy.Next(true, 0.95f, 30f, out _) == 30f && SnapTurnPolicy.Next(true, -0.95f, 45f, out _) == -45f);
Check("snap-view-yaw-wraps-at-half-a-turn",
    Near(HandAimMath.WrapYaw(190f), -170f) && Near(HandAimMath.WrapYaw(-190f), 170f) &&
    Near(HandAimMath.WrapYaw(180f), 180f) && Near(HandAimMath.WrapYaw(360f), 0f) && Near(HandAimMath.WrapYaw(float.NaN), 0f));
Check("snap-view-yaw-accumulates-and-wraps-exactly",
    Near(HandAimMath.AddYaw(0f, 45f), 45f) && Near(HandAimMath.AddYaw(45f, 45f), 90f) &&
    Near(HandAimMath.AddYaw(90f, 45f), 135f) && Near(HandAimMath.AddYaw(135f, 45f), 180f) &&
    Near(HandAimMath.AddYaw(180f, 45f), -135f) && Near(HandAimMath.AddYaw(-135f, -45f), 180f) &&
    Near(HandAimMath.AddYaw(30f, -30f), 0f));
Check("ability-inactive-down-does-not-start", AbilityStickPolicy.Next(false, false, -0.9f, 0f, false) == (false, false));
using (var bindingData = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "config", "bindings.json"))))
{
    var runtimeMap = bindingData.RootElement.GetProperty("runtimeOverride");
    var abilityMap = runtimeMap.GetProperty("rightStickAbilities");
    var bloodMap = abilityMap.GetProperty("blood_ability");
    var specialMap = abilityMap.GetProperty("dash");
    Check("ability-config-override-mirrors-production", runtimeMap.GetProperty("decision").GetString() == "D-114" &&
        bloodMap.GetProperty("direction").GetString() == "positive" && specialMap.GetProperty("direction").GetString() == "negative" &&
        Near(bloodMap.GetProperty("engage").GetSingle(), StickLatchPolicy.PressAt) && Near(bloodMap.GetProperty("release").GetSingle(), StickLatchPolicy.ReleaseAt) &&
        Near(specialMap.GetProperty("engage").GetSingle(), -StickLatchPolicy.PressAt) && Near(specialMap.GetProperty("release").GetSingle(), -StickLatchPolicy.ReleaseAt));
}
Check("source-robot-arms-on-stick-clicks", inputSrc.Contains("leftHand.StickClick == 1") && inputSrc.Contains("rightHand.StickClick == 1") && inputSrc.Contains("(armL ? BitLeftShoulder : 0u)") && inputSrc.Contains("(armR ? BitRightShoulder : 0u)"));
Check("source-jump-stays-on-south-and-interact-on-left-x", inputSrc.Contains("(jump ? BitSouth : 0u)") && inputSrc.Contains("bool interact = alive && leftHand.Lower == 1") && inputSrc.Contains("(interact ? (BitLeftStick | BitSelect) : 0u)") && !inputSrc.Contains("bool run ="));
Check("source-input-release-clears-all-held", inputSrc.Contains("menuHeld = jumpHeld = interactHeld = selectHeld = reloadHeld = reloadLHeld = reloadRHeld = faceLWas = faceRWas = specialHeld = bloodHeld = armLHeld = armRHeld = false"));
Check("source-ammo-measurement-read-only", shotSrc.Contains("private void RecordAmmo(Weapon weapon)") && shotSrc.Contains("s.RecordAmmo(__instance);") && !shotSrc.Contains("current_ammo =") && !shotSrc.Contains("force_set_ammo"));
Check("source-ammo-receipt-finite-safe", driver.Contains("FiniteOrNull(Shot.FirstAmmo)") && driver.Contains("FiniteOrNull(Shot.WeaponFireRate)") && driver.Contains("maxShootsPerFrame = Shot.MaxShootsPerFrame"));
Check("source-game-crosshair-hidden-via-canvasrenderer", uiSrc.Contains("HideGameCrosshair();") && uiSrc.Contains("GetComponentsInChildren<Graphic>(true)") && uiSrc.Contains("g.canvasRenderer.cull = true") && driver.Contains("crosshairHidden = CrosshairHidden"));
// --- D-101: reload = Y / B pressed with that controller beside the headset; left X = Interact; trigger press counters
Check("head-reach-boundary", HeadReachPolicy.Near(0.30f) && HeadReachPolicy.Near(0f) && !HeadReachPolicy.Near(0.31f) && !HeadReachPolicy.Near(-0.1f));
Check("head-reach-nonfinite-is-far", !HeadReachPolicy.Near(float.NaN) && !HeadReachPolicy.Near(float.PositiveInfinity) && !HeadReachPolicy.Near(float.NegativeInfinity));
Check("reload-latch-near-press-starts", ReloadLatchPolicy.Next(false, false, true, 0.15f));
Check("reload-latch-far-press-ignored", !ReloadLatchPolicy.Next(false, false, true, 0.6f));
Check("reload-latch-press-far-then-move-near-ignored", !ReloadLatchPolicy.Next(false, true, true, 0.1f));
Check("reload-latch-holds-after-hand-leaves", ReloadLatchPolicy.Next(true, true, true, 2.0f));
Check("reload-latch-release-clears", !ReloadLatchPolicy.Next(true, true, false, 0.1f) && !ReloadLatchPolicy.Next(false, false, false, 0.1f));
Check("reload-latch-unknown-head-never-starts", !ReloadLatchPolicy.Next(false, false, true, float.PositiveInfinity) && !ReloadLatchPolicy.Next(false, false, true, float.NaN));
Check("source-reload-on-west-near-head", inputSrc.Contains("BitWest = 1u << 7") && inputSrc.Contains("ReloadLatchPolicy.Next(reloadLHeld, faceLWas, faceL, headL)") && inputSrc.Contains("ReloadLatchPolicy.Next(reloadRHeld, faceRWas, faceR, headR)") && inputSrc.Contains("(reload ? BitWest : 0u)") && inputSrc.Contains("(slot.AimOrigin - headPos).magnitude"));
Check("hud-dark-panel-hides-backdrop", HudDarkPanelPolicy.ShouldHide(0.05f, 0.05f, 0.08f, 0.5f, 0.15f, 0.40f, false));
Check("hud-dark-panel-keeps-gauges", !HudDarkPanelPolicy.ShouldHide(0.05f, 0.05f, 0.05f, 0.5f, 0.15f, 0.40f, true) && !HudDarkPanelPolicy.ShouldHide(0.05f, 0.05f, 0.05f, 0.5f, 0.25f, 0.02f, false));
Check("hud-dark-panel-keeps-bright-opaque-and-invisible", !HudDarkPanelPolicy.ShouldHide(0.9f, 0.9f, 0.9f, 0.5f, 0.2f, 0.4f, false) && !HudDarkPanelPolicy.ShouldHide(0.05f, 0.05f, 0.05f, 1.0f, 0.2f, 0.4f, false) && !HudDarkPanelPolicy.ShouldHide(0.05f, 0.05f, 0.05f, 0.0f, 0.2f, 0.4f, false));
Check("hud-dark-panel-nonfinite-kept", !HudDarkPanelPolicy.ShouldHide(float.NaN, 0f, 0f, 0.5f, 0.2f, 0.4f, false) && !HudDarkPanelPolicy.ShouldHide(0f, 0f, 0f, 0.5f, float.PositiveInfinity, 0.4f, false) && !HudDarkPanelPolicy.ShouldHide(0f, 0f, 0f, 0.5f, 0f, 0.4f, false));
Check("source-hud-head-locked-rigid", uiSrc.Contains("HudLayoutPolicy.UseHeadPose(c.HeadLocked, paused)") && uiSrc.Contains("hudRect.rotation = rot;") && uiSrc.Contains("hudRect.position = UiHeadTarget(pos, rot, c.Distance);") && uiSrc.Contains("HudLayoutPolicy.IsHeadLocked(path)"));
Check("source-hud-dark-culled-and-restored", uiSrc.Contains("HideDarkHudPanels();") && uiSrc.Contains("HudDarkPanelPolicy.ShouldHide(") && uiSrc.Contains("g.canvasRenderer.cull = true;") && uiSrc.Contains("g.canvasRenderer.cull = false;") && uiSrc.Contains("c.HeadLocked"));
Check("source-hud-receipt-lists-candidates", driver.Contains("hudDarkCandidates = hudDarkLog") && driver.Contains("hudDarkHidden = HudDarkHidden") && driver.Contains("hudLocked = HudLockedCanvases"));
// --- D-105: level-up screen head-locked, backdrop hidden, ray picks the canvas it really crosses, shots aim the camera at the muzzle
Check("levelup-path-detected", HudLayoutPolicy.IsLevelUp("AllThingsBaseLevel/Prefab_Player_Level1/Level Up/NewNewCanvas") && !HudLayoutPolicy.IsLevelUp("Canvas - Screens") && !HudLayoutPolicy.IsLevelUp(null) && !HudLayoutPolicy.IsLevelUp(""));
Check("backdrop-hides-full-dark-panel", UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.8f, 1.0f, 1.0f, false) && UiBackdropPolicy.ShouldHide(0.1f, 0.1f, 0.1f, 0.5f, 0.9f, 0.6f, false));
Check("backdrop-keeps-cards-bright-filled-and-invisible", !UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.8f, 0.2f, 0.6f, false) && !UiBackdropPolicy.ShouldHide(0.9f, 0.9f, 0.9f, 0.8f, 1f, 1f, false) && !UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.8f, 1f, 1f, true) && !UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.0f, 1f, 1f, false) && !UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.8f, 1f, 0.3f, false));
Check("backdrop-nonfinite-kept", !UiBackdropPolicy.ShouldHide(float.NaN, 0f, 0f, 0.8f, 1f, 1f, false) && !UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.8f, float.PositiveInfinity, 1f, false));
// --- D-155 (TOOL-074, regression BUG-215): backdrop modes. The shipped default is `authored`, so the level-up / chest /
// boss-chest / pause / results backdrops keep cull=false and their own tint alpha; only the explicit `hide` restores the
// pre-D-155 cull. These guards exist because the mode gates were silently dropped from HandUi while the docs claimed them.
Check("backdrop-mode-accepts-exactly-three-values", GameBackdropPolicy.IsValid(GameBackdropPolicy.Authored) && GameBackdropPolicy.IsValid(GameBackdropPolicy.Dim) && GameBackdropPolicy.IsValid(GameBackdropPolicy.Hide) && !GameBackdropPolicy.IsValid("") && !GameBackdropPolicy.IsValid(null) && !GameBackdropPolicy.IsValid("Authored"));
Check("backdrop-mode-cull-only-in-hide", !GameBackdropPolicy.ShouldCullBackdrops(GameBackdropPolicy.Authored) && !GameBackdropPolicy.ShouldCullBackdrops(GameBackdropPolicy.Dim) && GameBackdropPolicy.ShouldCullBackdrops(GameBackdropPolicy.Hide));
Check("backdrop-mode-alpha-cap-never-in-authored", !GameBackdropPolicy.ShouldDim(GameBackdropPolicy.Authored) && GameBackdropPolicy.ShouldDim(GameBackdropPolicy.Dim) && GameBackdropPolicy.ShouldDim(GameBackdropPolicy.Hide));
Check("backdrop-authored-keeps-levelup-and-chest-even-when-legacy-rule-matches",
    UiBackdropPolicy.ShouldHide(0f, 0f, 0f, 0.8f, 1f, 1f, false) && !GameBackdropPolicy.ShouldCullBackdrops(GameBackdropPolicy.Authored) &&
    HudTranslucentPanelPolicy.IsKnownBottomShadow("AllThingsBaseLevel/Prefab_Player_Level1/Player HUD/Bottom Shadow", "HUD Bottom Shadow", false));
Check("source-backdrop-mode-gates-cull-and-alpha-cap",
    uiSrc.Contains("bool legacyCull = GameBackdropPolicy.ShouldCullBackdrops(GameBackdropMode);") &&
    uiSrc.Contains("bool hide = legacyCull && UiBackdropPolicy.ShouldHide(") &&
    uiSrc.Contains("knownShadow || (legacyCull &&") &&
    uiSrc.Contains("bool dimAllowed = GameBackdropPolicy.ShouldDim(GameBackdropMode);") &&
    uiSrc.Contains("if (!dimAllowed) continue;"));
Check("source-backdrop-mode-wired-host-and-receipt",
    host.Contains("if (!GameBackdropPolicy.IsValid(Config.GameBackdropMode))") &&
    host.Contains("handAim.GameBackdropMode = Config.GameBackdropMode;") &&
    host.Contains("GAME_BACKDROP mode=") &&
    driver.Contains("gameBackdropMode = GameBackdropMode"));

Check("ray-ignores-point-outside-canvas", UiRayPolicy.Choose(new[] { 1.0f }, new[] { false }, new[] { false }) == -1);
Check("ray-prefers-canvas-with-pressable-over-nearer-empty", UiRayPolicy.Choose(new[] { 0.9f, 1.2f }, new[] { true, true }, new[] { false, true }) == 1);
Check("ray-nearest-when-both-pressable-or-both-empty", UiRayPolicy.Choose(new[] { 1.2f, 0.9f }, new[] { true, true }, new[] { true, true }) == 1 && UiRayPolicy.Choose(new[] { 1.2f, 0.9f }, new[] { true, true }, new[] { false, false }) == 1);
Check("ray-skips-behind-and-nonfinite", UiRayPolicy.Choose(new[] { -1f, float.NaN, 2f }, new[] { true, true, true }, new[] { true, true, false }) == 2 && UiRayPolicy.Choose(new float[0], new bool[0], new bool[0]) == -1);
Check("source-levelup-head-locked-when-paused", uiSrc.Contains("HudLayoutPolicy.UseHeadPose(c.HeadLocked, paused)") && uiSrc.Contains("entry.LevelUp = HudLayoutPolicy.IsLevelUp(path)"));
Check("source-backdrop-culled-and-restored-on-resume", uiSrc.Contains("UiBackdropPolicy.ShouldHide(") && uiSrc.Contains("uiBackdropCulled.Add(g)") && uiSrc.Contains("if (!paused && uiWasPaused) UiBackdropRestore();") && uiSrc.Contains("g.canvasRenderer.cull = false;") && uiSrc.Contains("UiBackdropRestore();") && uiSrc.Contains("foreach (var g in hudDarkCulled)") && uiSrc.Contains("hudBackdropOriginal.Clear();"));
Check("source-cards-flattened-and-restored", uiSrc.Contains("GetComponentsInChildren<LookAtCamera>(true)") && uiSrc.Contains("rt.rotation = flatCanvases[i].transform.rotation;") && uiSrc.Contains("flatRects[i].localRotation = flatOriginal[i];") && uiSrc.Contains("b.enabled = true;") && driver.Contains("flattened = flatLog"));
Check("source-ray-uses-rect-containment-policy", uiSrc.Contains("UiRayPolicy.Choose(dist, inside, pressable)") && uiSrc.Contains("crect.rect.Contains(crect.InverseTransformPoint(point))") && !uiSrc.Contains("float bestT = UiMaxRay"));
// --- D-106: the translucent panel behind the in-game HUD is colour-agnostic (fp3 had hudDarkHidden=0 with an empty candidate list)
Check("hud-translucent-hides-wide-panel-any-tint", HudTranslucentPanelPolicy.ShouldHide(0.4f, 0.55f, 0.20f, false) && HudTranslucentPanelPolicy.ShouldHide(0.25f, 0.90f, 0.15f, false));
Check("hud-translucent-keeps-gauge-bar-opaque-invisible", !HudTranslucentPanelPolicy.ShouldHide(0.4f, 0.55f, 0.20f, true) && !HudTranslucentPanelPolicy.ShouldHide(0.4f, 0.30f, 0.02f, false) && !HudTranslucentPanelPolicy.ShouldHide(1.0f, 0.55f, 0.20f, false) && !HudTranslucentPanelPolicy.ShouldHide(0.0f, 0.55f, 0.20f, false) && !HudTranslucentPanelPolicy.ShouldHide(0.4f, 0.10f, 0.50f, false));
Check("hud-translucent-nonfinite-kept", !HudTranslucentPanelPolicy.ShouldHide(float.NaN, 0.5f, 0.2f, false) && !HudTranslucentPanelPolicy.ShouldHide(0.4f, float.PositiveInfinity, 0.2f, false) && !HudTranslucentPanelPolicy.ShouldHide(0.4f, 0.5f, 0f, false));
string hapticSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandInput.cs")) + "\n" + File.ReadAllText(Path.Combine(root, "src", "interaction", "HandPose.cs"));
string hostSrc = host;
string handsCpp = File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Hands.cpp"));
string hostCpp = File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Host.cpp"));
Check("haptic-enters-pulse-once", HeadHapticPolicy.Next(false, 0.25f, 99f, out bool n1) && n1 && !HeadHapticPolicy.Next(true, 0.20f, 99f, out bool n2) && n2);
Check("haptic-far-does-not-pulse", !HeadHapticPolicy.Next(false, 0.31f, 99f, out bool n3) && !n3 && !HeadHapticPolicy.Next(false, 2f, 99f, out bool n4) && !n4);
Check("haptic-boundary-matches-reload-reach", HeadHapticPolicy.Next(false, 0.30f, 99f, out _) && !HeadHapticPolicy.Next(false, 0.3001f, 99f, out _));
Check("haptic-hysteresis-no-buzz-on-the-line", !HeadHapticPolicy.Next(true, 0.34f, 99f, out bool stay) && stay && !HeadHapticPolicy.Next(true, 0.37f, 99f, out bool gone) && !gone && HeadHapticPolicy.Next(gone, 0.25f, 99f, out _));
Check("haptic-reentry-after-leaving-pulses-again", HeadHapticPolicy.Next(false, 0.2f, 99f, out bool again) && again);
Check("haptic-min-interval-guard", !HeadHapticPolicy.Next(false, 0.2f, 0.1f, out bool quick) && quick && HeadHapticPolicy.Next(false, 0.2f, 0.4f, out _));
Check("haptic-unknown-distance-is-far", !HeadHapticPolicy.Next(false, float.NaN, 99f, out bool u1) && !u1 && !HeadHapticPolicy.Next(true, float.PositiveInfinity, 99f, out bool u2) && !u2 && !HeadHapticPolicy.Next(false, -0.1f, 99f, out bool u3) && !u3);
Check("haptic-clamps", Near(HeadHapticPolicy.ClampAmplitude(2f), 1f) && Near(HeadHapticPolicy.ClampAmplitude(-1f), 0f) && Near(HeadHapticPolicy.ClampAmplitude(float.NaN), 0.4f) && HeadHapticPolicy.ClampMilliseconds(9999) == 500 && HeadHapticPolicy.ClampMilliseconds(-5) == 0 && Near(HeadHapticPolicy.DefaultAmplitude, 0.4f) && HeadHapticPolicy.DefaultMilliseconds == 60);
Check("source-haptic-per-hand-edge-wiring", hapticSrc.Contains("HeadHapticPolicy.Next(wasNear, distance, now - lastAt, out bool near)") && hapticSrc.Contains("HapticHand(left, 0,") && hapticSrc.Contains("HapticHand(right, 1,") && hapticSrc.Contains("HandAimNative.RequestHaptic(index,") && hapticSrc.Contains("bool alive = available && Time.timeScale > 0.01f;") && driver.Contains("HapticTick(available);") && driver.Contains("RequestHaptic(int hand, float amplitude, int durationMs)"));
Check("source-haptic-config-and-receipt", hostSrc.Contains("public bool HandHaptic { get; set; } = true;") && hostSrc.Contains("handAim.HapticEnabled = Config.HandHaptic;") && driver.Contains("pulsesLeft = HapticPulsesLeft") && driver.Contains("pulsesRight = HapticPulsesRight"));
Check("source-haptic-native-render-thread-only", handsCpp.Contains("XR_ACTION_TYPE_VIBRATION_OUTPUT") && handsCpp.Contains("/user/hand/left/output/haptic") && handsCpp.Contains("/user/hand/right/output/haptic") && handsCpp.Contains("xrApplyHapticFeedback") && hostCpp.Contains("HandsHaptic(session,hand,hapticAmplitude[hand],hapticDurationNs[hand])") && hostCpp.Contains("API int __cdecl RequestHaptic(int hand,float amplitude,int durationMs)") && !hostCpp.Contains("xrApplyHapticFeedback"));
Check("source-haptic-failure-disables-haptic-only", hostCpp.Contains("hapticDisabled=true;Log(\"HAPTIC DISABLED \"+hapticError);") && !hostCpp.Contains("handsCreated=false;Log(\"HAPTIC"));
// --- D-104: frame timing (measurement only) and opt-in anisotropic filtering
// D-148 added the submit split (copyMs/fenceMs/endFrameMs) between submitMs and displayPeriodMs in this same log line.
Check("source-timing-measurement-only-native-log", hostCpp.Contains("void Host::RecordTiming(Clock::time_point submitStart)") && hostCpp.Contains("RecordTiming(submitStart);") && hostCpp.Contains("cycleMs=%.2f waitMs=%.2f prepToSubmitMs=%.2f submitMs=%.2f copyMs=%.2f fenceMs=%.2f endFrameMs=%.2f displayPeriodMs=%.2f") && hostCpp.Contains("if(submitCount<120) return;") && !hostCpp.Contains("hands.reserved=Ms("));
Check("source-anisotropic-opt-in-default-off", hostSrc.Contains("public bool Anisotropic { get; set; }") && !hostSrc.Contains("public bool Anisotropic { get; set; } = true") && hostSrc.Contains("QualitySettings.anisotropicFiltering") && hostSrc.Contains("SetGlobalAnisotropicFilteringLimits(16, 16)"));
// D-148b: the 3.7 ms that d148e left unmeasured between waitMs+prepToSubmitMs+submitMs and cycleMs is now
// attributed: the Prepare entry gap, Pump, the xrBeginFrame/xrLocateViews/HandsSync body and the per-frame Log()
// writes on the render thread. The identity cycleMs = gapMs + pumpMs + waitMs + prepBodyMs + prepToSubmitMs +
// submitMs is what the next sitting reads; logMs is a sub-span and prepares is the Prepare-side divisor.
Check("source-timing-cycle-decomposition", hostCpp.Contains("displayPeriodMs=%.2f prepBodyMs=%.2f handsMs=%.2f pumpMs=%.2f gapMs=%.2f logMs=%.2f prepares=%d") && hostCpp.Contains("const Clock::time_point bodyStart=Clock::now();") && hostCpp.Contains("preparedAt=Clock::now();prepBodyMsSum+=Ms(preparedAt-bodyStart);status.state=2;") && hostCpp.Contains("handsMsSum+=Ms(Clock::now()-handsStart);") && hostCpp.Contains("prepareGapMsSum+=Ms(entryAt-lastSubmitEnd);prepareCount++;") && hostCpp.Contains("void Host::TimedLog(const std::string& phase)") && hostCpp.Contains("prepBodyMsSum=handsMsSum=pumpMsSum=prepareGapMsSum=logMsSum=0;prepareCount=0;") && !hostCpp.Contains("preparedAt=Clock::now();status.state=2;"));
Check("source-drill-forwards-anisotropic-and-reads-timing", File.ReadAllText(Path.Combine(root, "tests", "headset-handaim.ps1")).Contains("Anisotropic = $Anisotropic.IsPresent") && File.ReadAllText(Path.Combine(root, "tests", "headset-handaim.ps1")).Contains("timingMedianFps"));











































































































Check("source-rest-validates-snapshot-not-driven-local", driver.Contains("HandAimPolicy.ShouldCaptureRest") && driver.Contains("slot.RestCaptured && HandAimMath.RestPlausible(slot.RestLocalPosition.x") && driver.Contains("if (chosen != slot.Container)"));
Check("source-shadow-persistent-and-restored", uiSrc.Contains("HudTranslucentPanelPolicy.IsKnownBottomShadow(path, art, filled)") && uiSrc.Contains("hudBackdropOriginal.Add") && uiSrc.Contains("color.a = entry.Alpha") && uiSrc.Contains("if (HideHudBackdrop) ApplyHudBackdropSuppression();"));
Check("source-layout-render-share-offset-target", uiSrc.Contains("UiHeadTarget(headPos, headRot, c.Distance)") && uiSrc.Contains("UiHeadTarget(pos, rot, c.Distance) - hudRect.TransformVector") && uiSrc.Contains("HudLayoutPolicy.ClampVerticalOffset(UiVerticalOffset)"));
Check("source-ui-config-and-pose-diagnostics", host.Contains("handAim.HideHudBackdrop") && host.Contains("Config.UiVerticalOffset") && driver.Contains("capturedRestLocal =") && driver.Contains("currentLocal =") && driver.Contains("currentWorld ="));

// D-202 (BUG-202): pin the wiring, not just the maths. The frame is one key, one composition base, one rest capture.
// D-211 (BUG-203): the head frame now TURNS the view - the same view-yaw offset is composed into both eyes and handed
// to HandAim as the composition base, and it is reset whenever a new anchor is captured.
Check("source-hand-pose-frame-key-and-default",
    host.Contains("public string HandPoseFrame { get; set; } = \"Head\";") && host.Contains("invalid hand pose frame: Head|Anchor"));
Check("source-hand-frame-selects-the-composition-base",
    host.Contains("Quaternion handPoseBase = (handPoseAnchorFrame ? anchorRotation : ViewYawRotation);") && host.Contains("HAND_FRAME "));
Check("source-rest-capture-uses-the-same-frame-as-the-drive",
    driver.Contains("RestCaptureFrame(poseBaseRotation, pose, slot.SampleActive)") && driver.Contains("restCapturedWithPose =")
    && !driver.Contains("HandAimMath.Relative(Rotation(source.transform.rotation)") && handAimSrc.Contains("PoseRotation(poseBaseRotation, hand)"));
Check("source-head-frame-snap-turns-the-view-not-the-anchor",
    host.Contains("snapViewYaw = HandAimMath.AddYaw(snapViewYaw, degrees);")
    && host.Contains("private Quaternion ViewYawRotation => snapViewYaw == 0f ? Quaternion.identity : Quaternion.AngleAxis(snapViewYaw, Vector3.up);")
    && host.Contains("Vector3 val2 = val + (snapViewYaw == 0f ? eyeOffset : viewYaw * eyeOffset);")
    && host.Contains("Quaternion val3 = (snapViewYaw == 0f ? eyeRotation : viewYaw * eyeRotation);")
    && !host.Contains("SNAP ignored on the head frame"));
Check("source-snap-view-yaw-is-reset-with-every-new-anchor",
    System.Text.RegularExpressions.Regex.Matches(host, "anchorRotation = \\(\\(Component\\)source\\)\\.transform\\.rotation;\\s*\\r?\\n\\s*ResetSnapView\\(\\);").Count == 4);
Check("source-snap-turn-step-is-one-of-three-choices-and-logged",
    host.Contains("snapStepDegrees = SnapTurnPolicy.Resolve(Config.SnapTurnDegrees);")
    && host.Contains("is not one of the shipped choices (0, 30, 45); using ")
    && host.Contains("handAim.SnapDegrees = snapStepDegrees;"));
Check("source-snap-receipt-exposes-the-view-yaw",
    host.Contains("snapTurnDegrees = snapStepDegrees,") && host.Contains("snapViewYaw = snapViewYaw,") && host.Contains("snapTurnsApplied = snapAppliedTurns,"));
Check("source-schema-lists-the-hand-pose-frame", schema.Contains("\"HandPoseFrame\"") && schema.Contains("\"enum\": [\"Head\", \"Anchor\"]"));
Check("source-schema-lists-the-three-snap-turn-choices",
    schema.Contains("\"SnapTurnDegrees\"") && schema.Contains("\"enum\": [0, 30, 45]"));
Check("source-installer-ships-a-valid-snap-step",
    File.ReadAllText(Path.Combine(root, "installer", "install.ps1")).Contains("SnapTurnDegrees = 30"));


















































































































Check("marker-size-is-angular-and-clamped",
    Near(WorldMarkerPolicy.SizeFor(0f), WorldMarkerPolicy.MinSizeMeters) && Near(WorldMarkerPolicy.SizeFor(2f), 0.1f) &&
    Near(WorldMarkerPolicy.SizeFor(10f), 0.5f) && Near(WorldMarkerPolicy.DrawDistance(200f), WorldMarkerPolicy.MaxDrawDistance) &&
    Near(WorldMarkerPolicy.SizeFor(-1f), WorldMarkerPolicy.MinSizeMeters) && Near(WorldMarkerPolicy.SizeFor(float.NaN), WorldMarkerPolicy.MinSizeMeters));
Check("marker-priority-and-ranking",
    WorldMarkerPolicy.Priority("BOSS_CHEST") > WorldMarkerPolicy.Priority(WorldMarkerPolicy.ChestToken) &&
    WorldMarkerPolicy.Priority(WorldMarkerPolicy.ChestToken) > WorldMarkerPolicy.Priority("HEALING") &&
    // In view beats off gaze, then the kind, then the nearer one: what the player is looking at is never dropped first.
    WorldMarkerPolicy.Compare(false, 1, 50f, true, 3, 1f) < 0 && WorldMarkerPolicy.Compare(true, 3, 1f, false, 1, 50f) > 0 &&
    WorldMarkerPolicy.Compare(false, 3, 50f, false, 1, 1f) < 0 && WorldMarkerPolicy.Compare(false, 1, 1f, false, 1, 5f) < 0 &&
    WorldMarkerPolicy.Compare(false, 1, 5f, false, 1, 5f) == 0);
Check("marker-gaze-angle",
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { 0f, 0f, 1f }), 0f, 0.01f) &&
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { 1f, 0f, 0f }), 90f, 0.01f) &&
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { 0f, 0f, -1f }), 180f, 0.01f) &&
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { 0f, 0f, 3f }), 0f, 0.01f) &&
    // A direction that cannot be trusted counts as fully off gaze, so a broken sample can never sit on the crosshair.
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { 0f, 0f, 0f }), 180f, 0.01f) &&
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { 1f, 0f }), 180f, 0.01f) &&
    Near(WorldMarkerPolicy.AngleFromGaze(new[] { float.NaN, 0f, 1f }), 180f, 0.01f));
Check("marker-offgaze-threshold-is-strict",
    !WorldMarkerPolicy.IsOffGaze(new[] { 0f, 0f, 1f }) &&
    // Exactly on the 40 degree limit is still "in view"; a hair past it is not.
    !WorldMarkerPolicy.IsOffGaze(new[] { MathF.Sin(40f * MathF.PI / 180f), 0f, MathF.Cos(40f * MathF.PI / 180f) }) &&
    WorldMarkerPolicy.IsOffGaze(new[] { MathF.Sin(41f * MathF.PI / 180f), 0f, MathF.Cos(41f * MathF.PI / 180f) }) &&
    WorldMarkerPolicy.IsOffGaze(new[] { 1f, 0f, 0f }));
Check("marker-visor-clamp-keeps-inside-directions",
    NearV(WorldMarkerPolicy.ClampToVisor(new[] { 0f, 0f, 1f }), new[] { 0f, 0f, 1f }) &&
    // A still-in-view direction keeps its direction exactly; only its length is normalised to one.
    WorldMarkerPolicy.TryNormalize(new[] { 0.01f, 0.02f, 1f }, out var inside) &&
    NearV(WorldMarkerPolicy.ClampToVisor(new[] { 0.01f, 0.02f, 1f }), inside, 1e-6f) &&
    Near(WorldMarkerPolicy.Length(WorldMarkerPolicy.ClampToVisor(new[] { 0f, 5f, 5f })), 1f, 1e-4f));
Check("marker-visor-clamp-puts-outside-directions-on-the-ring",
    WorldMarkerPolicy.TryNormalize(WorldMarkerPolicy.ClampToVisor(new[] { 1f, 0f, 0f }), out var ringRight) &&
    Near(WorldMarkerPolicy.AngleFromGaze(ringRight), WorldMarkerPolicy.VisorMaxAngleDegrees, 0.01f) &&
    Near(ringRight[1], 0f) && ringRight[0] > 0f &&
    // The sideways sign is kept, so a target on the left stays on the left.
    WorldMarkerPolicy.TryNormalize(WorldMarkerPolicy.ClampToVisor(new[] { -3f, 0f, 0f }), out var ringLeft) && ringLeft[0] < 0f &&
    WorldMarkerPolicy.TryNormalize(WorldMarkerPolicy.ClampToVisor(new[] { 0f, 1f, 0f }), out var ringUp) &&
    Near(WorldMarkerPolicy.AngleFromGaze(ringUp), WorldMarkerPolicy.VisorMaxAngleDegrees, 0.01f) && ringUp[1] > 0f);
Check("marker-visor-clamp-straight-behind-uses-up",
    WorldMarkerPolicy.TryNormalize(WorldMarkerPolicy.ClampToVisor(new[] { 0f, 0f, -1f }), out var behind) &&
    Near(WorldMarkerPolicy.AngleFromGaze(behind), WorldMarkerPolicy.VisorMaxAngleDegrees, 0.01f) && behind[1] > 0f &&
    Near(behind[0], 0f) && Near(WorldMarkerPolicy.Length(WorldMarkerPolicy.ClampToVisor(new[] { 0f, 0f, -1f })), 1f, 1e-4f) &&
    NearV(WorldMarkerPolicy.ClampToVisor(new[] { 0f, 0f, 0f }), new[] { 0f, 0f, 1f }));
Check("marker-normalize-rejects-untrustworthy-directions",
    !WorldMarkerPolicy.TryNormalize(new[] { 0f, 0f, 0f }, out _) && !WorldMarkerPolicy.TryNormalize(new[] { 0f, 0f }, out _) &&
    !WorldMarkerPolicy.TryNormalize(new[] { 1e-9f, 0f, 1e-9f }, out _) && !WorldMarkerPolicy.TryNormalize(new[] { float.NaN, 0f, 1f }, out _) &&
    WorldMarkerPolicy.TryNormalize(new[] { 0f, 0f, 4f }, out var unitVec) && NearV(unitVec, new[] { 0f, 0f, 1f }));
// --- D-158: the POI token is reserved and deliberately NOT in the default set - it only ever means "the game's own arrow
// points somewhere the mod could not name", so it can never silently pull gates or unknown pickups into the default markers.
Check("marker-poi-token-is-reserved-and-off-by-default",
    WorldMarkerPolicy.IsKnownKind(WorldMarkerPolicy.PoiToken) && !WorldMarkerPolicy.PickupTypes.Contains("POI") &&
    !WorldMarkerPolicy.Wants(defaultKinds, WorldMarkerPolicy.PoiToken) &&
    WorldMarkerPolicy.TryParseKinds("POI", out var poiOnly) && poiOnly.Length == 1 && poiOnly[0] == "POI" &&
    WorldMarkerPolicy.TryParseKinds("POI,CHEST,HEALING", out var poiMix) && poiMix.Length == 3 && poiMix[0] == "POI" &&
    !WorldMarkerPolicy.IsKnownKind("poi") && !WorldMarkerPolicy.IsKnownKind("POIs"));

// --- D-157 (TOOL-086) source guards: the markers must read the game's OWN registries, must draw through walls with the same
// unlit always-on-top recipe as the D-095 reticle, must be placed again before each eye culls, and must be reachable only
// through a host-validated config key.
string markersSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandMarkers.cs"));
string markerReticleSrc = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandReticle.cs"));
Check("marker-source-guards-uses-the-games-own-registries",
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<PoolingSystem>()") && markersSrc.Contains(".active_pickups") &&
    markersSrc.Contains("AddPickupSource(") && markersSrc.Contains(".type") &&
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<RewardBox>()") && markersSrc.Contains("WorldMarkerPolicy.Wants(MarkerKinds"));
Check("marker-source-guards-read-only-and-mod-owned-objects",
    // D-161: the shape is the mod's own arrow mesh, built by hand - no builtin primitive and no collider is ever created.
    markersSrc.Contains("new GameObject(\"VHVR_Marker_\" + index)") && markersSrc.Contains(".AddComponent<MeshFilter>()") &&
    markersSrc.Contains(".sharedMesh = MarkerArrowMesh()") && markersSrc.Contains(".AddComponent<MeshRenderer>()") &&
    !markersSrc.Contains("GameObject.CreatePrimitive") && !markersSrc.Contains("AddComponent<Collider>") &&
    markersSrc.Contains("shadowCastingMode = (ShadowCastingMode)0") && markersSrc.Contains("markerQuad.Go.transform.position =") &&
    // Nothing of the game is written: only the mod's own arrow transform is set.
    !markersSrc.Contains("target.position =") && !markersSrc.Contains("Target.position =") && !markersSrc.Contains("target.transform.position ="));
Check("marker-source-guards-through-wall-material-reuses-the-reticle-recipe",
    markersSrc.Contains("EnsureReticleMaterial()") && markersSrc.Contains("new Material(reticleMaterial)") &&
    markersSrc.Contains("HasProperty(\"_Cull\")") && markersSrc.Contains("SetInt(\"_Cull\", 0)") && markersSrc.Contains("markerShader = reticleShader") &&
    markerReticleSrc.Contains("if (reticleMaterial.HasProperty(\"_ZTest\")) reticleMaterial.SetInt(\"_ZTest\", (int)CompareFunction.Always);") &&
    markerReticleSrc.Contains("reticleMaterial.renderQueue = 5000;"));
int beforeEyeMarker = uiSrc.IndexOf("driver.MarkersPlace();", StringComparison.Ordinal);
int afterLayoutSignature = uiSrc.IndexOf("private static void UiAfterLayout()", StringComparison.Ordinal);
int afterLayoutMarker = uiSrc.IndexOf("driver.MarkersPlace();", afterLayoutSignature, StringComparison.Ordinal);
Check("marker-source-guards-hook-places-before-every-eye",
    uiSrc.Contains("driver.MarkersPlace();") &&
    uiSrc.Contains("if (__0 == driver.uiSource || __0 == driver.uiEyeLeft || __0 == driver.uiEyeRight)") &&
    beforeEyeMarker > 0 && afterLayoutMarker > afterLayoutSignature && beforeEyeMarker < afterLayoutSignature &&
    afterLayoutMarker > uiSrc.IndexOf("driver.UiRenderPlace();", afterLayoutSignature, StringComparison.Ordinal));
Check("marker-source-guards-driver-tick-release-and-receipt",
    driver.Contains("MarkersTick(source);") && driver.Contains("MarkersRelease();") && driver.Contains("worldMarkers = MarkerReceipt(),") &&
    markersSrc.Contains("private object MarkerReceipt()") && markersSrc.Contains("depth test"));
Check("marker-source-guards-host-validates-wires-and-receipts",
    host.Contains("public string WorldMarkers { get; set; } = \"on\";") && host.Contains("public float MarkerMaxDistance { get; set; }") &&
    host.Contains("invalid world markers mode: off|on") && host.Contains("invalid marker kinds:") &&
    host.Contains("invalid marker max distance: 0 (no limit) or 5..200 metres") && host.Contains("invalid marker max count: 1..64") &&
    host.Contains("handAim.MarkersEnabled = Config.WorldMarkers == \"on\";") && host.Contains("handAim.MarkerKinds = kinds;") &&
    host.Contains("[OPENXR_HOST] WORLD_MARKERS mode=") && host.Contains("worldMarkers = Config.WorldMarkers"));
Check("marker-source-guards-drill-readiness-schema-and-install-carry-the-keys",
    drill.Contains("[string]$WorldMarkers = 'on'") && drill.Contains("WorldMarkers = $WorldMarkers") && drill.Contains("MarkerKinds = $MarkerKinds") &&
    readiness.Contains("[string]$ExpectWorldMarkers = 'on'") && readiness.Contains("[string]$ExpectMarkerKinds = 'CHEST,HEALING,POWER_UP,BOSS_CHEST'") &&
    schema.Contains("\"x-host-tunables\"") &&
    previewInstall.Contains("WorldMarkers = 'on'") && previewInstall.Contains("MarkerKinds = '" + WorldMarkerPolicy.DefaultKinds + "'"));
Check("marker-source-guards-plugin-compiles-the-new-partial",
    File.ReadAllText(Path.Combine(root, "src", "core", "VampireHuntersVR.Stage0.csproj")).Contains("<Compile Include=\"..\\interaction\\HandMarkers.cs\" Link=\"HandMarkers.cs\" />"));
Check("marker-source-guards-reads-health-points-and-arrow-targets",
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<HealthPoints>()") && markersSrc.Contains("health_points_object") &&
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<TargetHUDArrow>()") && markersSrc.Contains(".target") &&
    markersSrc.Contains("MarkerKindOf(target)") && markersSrc.Contains(".Contains(\"POIArrows\""));
Check("marker-source-guards-damage-arrows-are-ignored-and-never-hidden",
    markersSrc.Contains(".Contains(\"DemageArrows\", StringComparison.Ordinal)") && markersSrc.Contains("MarkerArrowDamage++;") &&
    !markersSrc.Contains("canvasRenderer") && !markersSrc.Contains("arrow.gameObject.SetActive") &&
    !markersSrc.Contains("box.gameObject.SetActive") && !markersSrc.Contains("point.gameObject.SetActive") &&
    markersSrc.Contains(".Go.SetActive(false)") && markersSrc.Contains(".Go.SetActive(true)"));
Check("marker-source-guards-kind-naming-uses-game-types-only",
    markersSrc.Contains(".GetComponentInParent<PickUp>()") && markersSrc.Contains(".GetComponentInParent<RewardBox>()") &&
    handAimSrc.Contains("public const string PoiToken = \"POI\";") &&
    markersSrc.Contains("liveHealthPoints = MarkerHealthPoints") && markersSrc.Contains("liveArrows = MarkerArrows") &&
    markersSrc.Contains("arrowDamageIgnored = MarkerArrowDamage") && markersSrc.Contains("arrowSample = markerArrowSample.ToArray()"));
// --- D-159: the user's correction - the icon was sitting on the STATUE the potion hangs on, which was in the way. The marker
// must be the potion pickup itself, and a statue with no potion must contribute nothing. Chest icons come from the game's own
// minimap chest targets as well as RewardBox, mapped only by the game's own Target_Type member names.
Check("marker-chest-target-type-mapping-is-by-the-games-own-names",
    WorldMarkerPolicy.ChestKindFromTargetType("LEVEL_UP_CHEST") == WorldMarkerPolicy.ChestToken &&
    WorldMarkerPolicy.ChestKindFromTargetType("MINI_BOSS_CHEST") == "BOSS_CHEST" &&
    WorldMarkerPolicy.ChestKindFromTargetType("HEALTH_POINT") == "" && WorldMarkerPolicy.ChestKindFromTargetType("MINI_BOSS") == "" &&
    WorldMarkerPolicy.ChestKindFromTargetType("PORTAL") == "" && WorldMarkerPolicy.ChestKindFromTargetType("") == "" &&
    WorldMarkerPolicy.ChestKindFromTargetType(null) == "" && WorldMarkerPolicy.ChestKindFromTargetType("level_up_chest") == "" &&
    WorldMarkerPolicy.WantsPotion(defaultKinds, "HEALING") && WorldMarkerPolicy.WantsPotion(defaultKinds, "POWER_UP") &&
    !WorldMarkerPolicy.WantsPotion(defaultKinds, "GOLD") && !WorldMarkerPolicy.WantsPotion(defaultKinds, "") && !WorldMarkerPolicy.WantsPotion(defaultKinds, null));
Check("marker-potion-rule-marks-the-potion-object-or-its-holder",
    markersSrc.Contains("AddPotionAt(") && markersSrc.Contains(".current_pickup") &&
    markersSrc.Contains(".health_points_object") && markersSrc.Contains("((Component)potion).transform;") &&
    markersSrc.Contains("AddHangingPotion(") && markersSrc.Contains("AddSource(transform, text);") &&
    markersSrc.Contains("MarkerStatuesEmpty++;") && markersSrc.Contains("AddSource(val, \"HEALING\");") && !markersSrc.Contains("potion.t;") &&
    markersSrc.Contains("if (IsUnderStatue(target))") && markersSrc.Contains("statuesWithoutPotion = MarkerStatuesEmpty") &&
    !markersSrc.Contains("Target = statue") && !markersSrc.Contains("Target = point"));
Check("marker-chest-source-guards-minimap-targets-and-rewardbox",
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<MiniMapTargets>()") && markersSrc.Contains(".mini_map_targets_queue") &&
    markersSrc.Contains("WorldMarkerPolicy.ChestKindFromTargetType(") &&
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<RewardBox>()") &&
    markersSrc.Contains("chestTargets = MarkerChestTargets") && markersSrc.Contains("chestTargetsSkipped = MarkerChestTargetsSkipped"));
// --- D-160: the 0.4h sitting showed shown=2 for a single potion (HealthPoints.current_pickup and the live pickup registry both
// describe it). One quad per target is now enforced in a single place, and the receipt counts the dropped duplicates.
Check("marker-one-quad-per-target-dedupe-guard",
    markersSrc.Contains("private void AddSource(Transform target, string kind)") &&
    markersSrc.Contains("if (!markerSourceIds.Add(((Object)target).GetInstanceID()))") && markersSrc.Contains("MarkerSourceDuplicates++;") &&
    markersSrc.Contains("duplicateSourcesDropped = MarkerSourceDuplicates") && markersSrc.Contains("markerSourceIds.Clear();") &&
    // Every source must go through AddSource: exactly one direct markerSources.Add call may exist (inside AddSource itself).
    markersSrc.Split("markerSources.Add(new MarkerSource").Length - 1 == 1);
// --- D-161: the user asked for three changes at once - mark the potions that hang ON statues too, mark every chest/potion
// regardless of distance, and draw a semi-transparent ARROW instead of an opaque quad. Each is pinned below.
Check("marker-arrow-geometry-is-the-mods-own-shape",
    // Seven vertices, three triangles, 1 unit tall, symmetric, flat in Z, and the head is wider than the shaft.
    WorldMarkerPolicy.ArrowVertices.Length == 21 && WorldMarkerPolicy.ArrowTriangles.Length == 9 &&
    Near(WorldMarkerPolicy.ArrowVertices[1], WorldMarkerPolicy.ArrowTipY) && Near(WorldMarkerPolicy.ArrowTipY, 0.5f) &&
    Near(WorldMarkerPolicy.ArrowTailY, -0.5f) &&
    WorldMarkerPolicy.ArrowVertices[0] == 0f && WorldMarkerPolicy.ArrowVertices[2] == 0f &&
    Near(WorldMarkerPolicy.ArrowVertices[4], WorldMarkerPolicy.ArrowHeadBaseY) &&
    Near(WorldMarkerPolicy.ArrowVertices[3], -WorldMarkerPolicy.ArrowHeadHalfWidth) && Near(WorldMarkerPolicy.ArrowVertices[6], WorldMarkerPolicy.ArrowHeadHalfWidth) &&
    Near(WorldMarkerPolicy.ArrowVertices[9], -WorldMarkerPolicy.ArrowShaftHalfWidth) && Near(WorldMarkerPolicy.ArrowVertices[12], WorldMarkerPolicy.ArrowShaftHalfWidth) &&
    Near(WorldMarkerPolicy.ArrowVertices[15], -WorldMarkerPolicy.ArrowShaftHalfWidth) && Near(WorldMarkerPolicy.ArrowVertices[18], WorldMarkerPolicy.ArrowShaftHalfWidth) &&
    WorldMarkerPolicy.ArrowHeadHalfWidth > WorldMarkerPolicy.ArrowShaftHalfWidth &&
    // Every triangle index is a real vertex and every Z is zero (a flat billboard).
    WorldMarkerPolicy.ArrowTriangles.All(i => i >= 0 && i < WorldMarkerPolicy.ArrowVertices.Length / 3) &&
    Enumerable.Range(0, 7).All(i => WorldMarkerPolicy.ArrowVertices[i * 3 + 2] == 0f) &&
    WorldMarkerPolicy.ArrowAlpha > 0f && WorldMarkerPolicy.ArrowAlpha < 1f &&
    WorldMarkerPolicy.RingArrowAlpha > 0f && WorldMarkerPolicy.RingArrowAlpha < WorldMarkerPolicy.ArrowAlpha);
Check("marker-distance-limit-is-none-by-default",
    // 0 = no limit (the user's "regardless of distance"), a non-finite value also means no limit, and 5..200 is a real cap.
    WorldMarkerPolicy.DefaultMaxDistance == 0f && WorldMarkerPolicy.ClampMaxDistance(0f) == 0f &&
    WorldMarkerPolicy.ClampMaxDistance(-3f) == 0f && WorldMarkerPolicy.ClampMaxDistance(float.NaN) == 0f &&
    WorldMarkerPolicy.ClampMaxDistance(float.PositiveInfinity) == 0f &&
    Near(WorldMarkerPolicy.ClampMaxDistance(40f), 40f) && Near(WorldMarkerPolicy.ClampMaxDistance(1f), WorldMarkerPolicy.MinMaxDistance) &&
    Near(WorldMarkerPolicy.ClampMaxDistance(9999f), WorldMarkerPolicy.MaxMaxDistance) &&
    !WorldMarkerPolicy.IsDistanceLimited(0f) && WorldMarkerPolicy.IsDistanceLimited(40f) &&
    WorldMarkerPolicy.WithinDistance(1234f, 0f) && !WorldMarkerPolicy.WithinDistance(1234f, 40f) &&
    WorldMarkerPolicy.WithinDistance(40f, 40f) && !WorldMarkerPolicy.WithinDistance(float.NaN, 0f));
Check("marker-count-default-covers-a-whole-level",
    WorldMarkerPolicy.DefaultMaxCount == 32 && WorldMarkerPolicy.MaxMaxCount == 64 &&
    WorldMarkerPolicy.ClampMaxCount(32) == 32 && WorldMarkerPolicy.ClampMaxCount(64) == 64 &&
    WorldMarkerPolicy.ClampMaxCount(65) == 32 && WorldMarkerPolicy.ClampMaxCount(0) == 32);
Check("marker-source-guards-every-live-pickup-is-hunted",
    markersSrc.Contains("Resources.FindObjectsOfTypeAll<PickUp>()") && markersSrc.Contains("MarkerPickupObjects++;") &&
    markersSrc.Contains("\"@live\"") && markersSrc.Contains("\"@self\" : \"@off\"") &&
    markersSrc.Contains("livePickupObjects = MarkerPickupObjects") && markersSrc.Contains("pickupSample = markerPickupSample.ToArray()") &&
    markersSrc.Contains("AddPickupSource(item2);"));
Check("marker-source-guards-arrow-orientation-tells-the-way",
    markersSrc.Contains("headPos - val5;") && markersSrc.Contains("Quaternion.Euler(0f, 0f, 180f)") &&
    markersSrc.Contains("Quaternion.LookRotation(val6, val8.normalized)") &&
    markersSrc.Contains("headRot * new Vector3(array[0], array[1], 0f);") &&
    markersSrc.Contains("if (val8.sqrMagnitude < 1E-08f)") &&
    markersSrc.Contains("WorldMarkerPolicy.WithinDistance(magnitude, MarkerMaxDistance)"));
Check("marker-source-guards-transparent-arrow-material",
    markersSrc.Contains("markerMaterial.SetColor(\"_Color\", new Color(1f, 1f, 1f, 0.5f))") &&
    markersSrc.Contains("markerMaterial.SetInt(\"_SrcBlend\", 5)") &&
    markersSrc.Contains("markerMaterial.SetInt(\"_DstBlend\", 10)") &&
    markersSrc.Contains("markerMaterial.renderQueue = 5000;") &&
    markersSrc.Contains("result.a = (offGaze ? 0.35f : 0.5f);") &&
    markersSrc.Contains("shape = \"arrow\"") && markersSrc.Contains("alpha = 0.5f") &&
    markersSrc.Contains("distanceLimited = WorldMarkerPolicy.IsDistanceLimited(MarkerMaxDistance)") &&
    markersSrc.Contains("SEMI-TRANSPARENT arrow"));
Check("marker-through-wall-shader-is-chosen-by-property",
    markersSrc.Contains("{ \"Hidden/Internal-Colored\", \"UI/Default\", \"Sprites/Default\" };") &&
    markersSrc.Contains("val3.HasProperty(\"_ZTest\");") &&
    markersSrc.Contains("if (flag)") &&
    markersSrc.Contains("markerZTestForced = markerMaterial.HasProperty(\"_ZTest\");") &&
    markersSrc.Contains("markerMaterial.SetInt(\"_ZTest\", 8);") &&
    markersSrc.Contains("\"marker shader \" + markerShader + \" has no _ZTest: walls may hide the arrows\";") &&
    markersSrc.Contains("zTestForced = markerZTestForced"));
Check("marker-arrow-follows-the-target-transform",
    markersSrc.Contains("return target.position + Vector3.up * 0.35f;") &&
    markersSrc.Contains("MarkerWorldOf(target);") && markersSrc.Contains("heightOffset = 0.35f") &&
    !markersSrc.Contains("bounds.max.y +") && !markersSrc.Contains("BoundsClearance") &&
    WorldMarkerPolicy.HeightOffset > 0f && WorldMarkerPolicy.HeightOffset < 1f &&
    markersSrc.Contains("\"@self\" : \"@off\""));

// D-166 behavioural policy checks, plus narrowly scoped guards for every production pickup route.
Check("marker-empty-holder-is-not-a-potion",
    !WorldMarkerPolicy.CanMarkPotion(false, true, defaultKinds, "HEALING") &&
    !WorldMarkerPolicy.CanMarkPotion(false, false, defaultKinds, null));
Check("marker-inactive-potion-is-not-available",
    !WorldMarkerPolicy.CanMarkPotion(true, false, defaultKinds, "HEALING"));
Check("marker-active-potion-respects-configured-kinds",
    WorldMarkerPolicy.CanMarkPotion(true, true, defaultKinds, "HEALING") &&
    WorldMarkerPolicy.CanMarkPotion(true, true, defaultKinds, "POWER_UP") &&
    !WorldMarkerPolicy.CanMarkPotion(true, true, new[] { "CHEST" }, "HEALING"));
Check("marker-nonpotion-pickup-cannot-certify-a-holder",
    !WorldMarkerPolicy.CanMarkPotion(true, true, defaultKinds, "BOSS_CHEST") &&
    !WorldMarkerPolicy.CanMarkPotion(true, true, new[] { "GOLD", "POI" }, "GOLD") &&
    !WorldMarkerPolicy.CanMarkPotion(true, true, defaultKinds, null));
string potionBody = markersSrc.Split("private bool AddPotionAt(")[1].Split("private void AddHangingPotion(")[0];
Check("marker-production-potion-filter-precedes-addsource",
    potionBody.Contains("WorldMarkerPolicy.CanMarkPotion(flag, flag2, MarkerKinds, text)") &&
    potionBody.IndexOf("WorldMarkerPolicy.CanMarkPotion", StringComparison.Ordinal) < potionBody.IndexOf("AddSource(transform, text);", StringComparison.Ordinal));
Check("marker-all-pickup-paths-use-component-transform",
    markersSrc.Split("AddPickupSource(").Length - 1 >= 3 &&
    markersSrc.Contains("Transform transform = ((Component)pickup).transform;") &&
    !System.Text.RegularExpressions.Regex.IsMatch(markersSrc, @"=\s*(pickup|potion)!?\.t\s*;"));
Check("marker-addsource-and-placement-reject-stale-inactive-targets",
    markersSrc.Contains("if (((Component)target).gameObject.activeInHierarchy && WorldMarkerPolicy.Wants(MarkerKinds, kind))") &&
    markersSrc.Contains("if (!((Object)(object)target == (Object)null) && ((Component)target).gameObject.activeInHierarchy)"));
Check("marker-recon-includes-seventh-child-and-bounded-descendants",
    markersSrc.Contains("j < statue.childCount && j < 32") && !markersSrc.Contains("j < statue.childCount && j < 6") &&
    markersSrc.Contains("AppendHolderNodes(statue") && markersSrc.Contains("int remaining = 96;") &&
    markersSrc.Contains("#depthTruncated") && markersSrc.Contains("#nodeBudgetRemaining="));

// D-206 (BUG-204): the engineer's turret build mode. Grips are unbound in normal play (D-057); inside the game's own
// turret mode (PlayerController.is_turret_mode) the left grip steps the blueprint back, the right grip steps it
// forward, and both grips held together hold the game's Fire action - the confirm the game's own text names.
var grip = new TurretGripPolicy();
var g1 = grip.Next(true, 1f, 0f, 0f);
Check("d206-left-press-does-not-step-while-held", !g1.Previous && !g1.Next && !g1.Confirm);
var g2 = grip.Next(true, 0f, 0f, 0.5f);
Check("d206-left-release-steps-back", g2.Previous);
var g3 = grip.Next(true, 0f, 0f, 0.5f + TurretGripPolicy.StepSeconds + 0.001f);
Check("d206-step-pulse-ends", !g3.Previous);
var g4 = grip.Next(true, 0f, 1f, 1f);
Check("d206-right-press-does-not-step-while-held", !g4.Next);
var g5 = grip.Next(true, 0f, 0f, 1.1f);
Check("d206-right-release-steps-forward", g5.Next && !g5.Previous);

var chord = new TurretGripPolicy();
var c1 = chord.Next(true, 1f, 0f, 10f);
var c2 = chord.Next(true, 1f, 1f, 10.2f); // the second grip joins the same hold: a chord, not a step
Check("d206-chord-confirms-and-steps-nothing", !c1.Confirm && c2.Confirm && !c2.Previous && !c2.Next);
var c3 = chord.Next(true, 0f, 1f, 10.5f); // a grip released out of a chord is not a step
Check("d206-chord-break-confirms-nothing-and-steps-nothing", !c3.Confirm && !c3.Previous && !c3.Next);
var c4 = chord.Next(true, 0f, 0f, 10.6f);
Check("d206-second-chord-release-does-not-step", !c4.Next && !c4.Previous);
var c5 = chord.Next(true, 0f, 1f, 11f);
var c6 = chord.Next(true, 0f, 0f, 11.1f);
Check("d206-grip-steps-again-after-a-chord", !c5.Next && c6.Next);

var twice = new TurretGripPolicy();
var t1 = twice.Next(true, 1f, 0f, 20f);
var t2 = twice.Next(true, 0f, 0f, 20.1f);
var t3 = twice.Next(true, 0f, 1f, 20.2f);
var t4 = twice.Next(true, 0f, 0f, 20.3f);
Check("d206-press-release-press-release-steps-both-ways",
    !t1.Previous && t2.Previous && !t2.Next && !t3.Next && t4.Next && !t4.Previous);

var reenter = new TurretGripPolicy();
reenter.Next(true, 1f, 0f, 30f);                       // held inside build mode
var modeOff = reenter.Next(false, 1f, 0f, 30.1f);      // the game leaves build mode while the grip is still down
Check("d206-mode-off-resets-and-emits-nothing", !modeOff.Previous && !modeOff.Confirm);
var modeBack = reenter.Next(true, 1f, 0f, 30.2f);      // back in build mode, same held grip: no phantom step
Check("d206-mode-on-with-a-held-grip-does-not-step", !modeBack.Previous);
var releasedAgain = reenter.Next(true, 0f, 0f, 30.3f);
Check("d206-grip-released-after-re-entering-steps-once", releasedAgain.Previous && !releasedAgain.Confirm);

var hyst = new TurretGripPolicy();
Check("d206-below-press-threshold-is-not-a-press", !hyst.Next(true, 0.5f, 0f, 40f).Previous);
hyst.Next(true, 0.8f, 0f, 40.1f);
var middleBand = hyst.Next(true, 0.5f, 0f, 40.2f);
var belowRelease = hyst.Next(true, 0.2f, 0f, 40.3f);
Check("d206-grip-hysteresis-holds-through-the-middle-band", !middleBand.Previous && belowRelease.Previous);

var nanGrip = new TurretGripPolicy();
Check("d206-nan-squeeze-is-not-a-press",
    !nanGrip.Next(true, float.NaN, float.NaN, 50f).Previous && !nanGrip.Next(true, float.NaN, float.NaN, 50.1f).Previous &&
    !nanGrip.Next(true, 0f, 0f, 50.2f).Previous && !nanGrip.Next(true, float.NaN, 1f, 50.3f).Confirm);

var reversed = new TurretGripPolicy();
reversed.Next(true, 0f, 1f, 60f);
var r1 = reversed.Next(true, 1f, 1f, 60.05f);
Check("d206-chord-is-symmetric-in-order", r1.Confirm && !r1.Previous && !r1.Next);

// D-206 wiring guards (BUG-204): the policy is reachable from the input path, the gate is the game's own flag, and the
// confirm is carried by both surfaces the game's own action asset binds Fire to (the callback and <Gamepad>/rightTrigger).
Check("d206-input-reads-the-games-own-turret-mode",
    inputSrc.Contains("player.is_turret_mode") && inputSrc.Contains("turretGrips.Next(cycleOn,") &&
    inputSrc.Contains("turretModeFailed = true; TurretModeError") && inputSrc.Contains("public string TurretCycle = \"Stick\";"));
Check("d206-confirm-is-the-game-fire-action",
    inputSrc.Contains("inputController.FirePressedInput(confirmHeld);") &&
    inputSrc.Contains("pad.Send(axis.x, axis.y, 0f, 0f, 0f, confirmHeld ? 1f : 0f, bits);") &&
    inputSrc.Contains("if (turret)") && inputSrc.Contains("else if (turretFireLatched)") &&
    inputSrc.Contains("controller.FirePressedInput(false);") && inputSrc.Contains("controller.navigationDirection = Vector2.zero;"));
Check("d206-every-step-surface-and-the-off-switch-are-wired",
    inputSrc.Contains("navigationDirection = step ? new Vector2(stepForward ? 1f : -1f, 0f) : Vector2.zero") &&
    inputSrc.Contains("if (stickStep) axis = new Vector2(stepForward ? StepStick : -StepStick, axis.y);") &&
    inputSrc.Contains("(dpadStep ? (stepForward ? BitDpadRight : BitDpadLeft) : 0u)") &&
    inputSrc.Contains("inputController.SpecialAbilityInput(special || specialStep);") &&
    inputSrc.Contains("TurretCycle.Equals(\"Off\", StringComparison.OrdinalIgnoreCase)"));
Check("d206-receipt-carries-the-turret-counters",
    driver.Contains("turret = new { cycle = TurretCycle, modeFrames = TurretModeFrames, stepsBack = TurretStepsBack, stepsForward = TurretStepsForward, confirms = TurretConfirms"));
Check("d206-native-grip-action-and-its-managed-mirror",
    hands.Contains("\"%s/input/squeeze/value\"") && hands.Contains("sample.squeeze=squeeze.isActive?squeeze.currentState:0.0f;") &&
    hands.Contains("XrActionSuggestedBinding bindings[19];") && hands.Contains("xrCreateAction(g_set,&create,&g_squeeze[hand]),name") &&
    hands.Contains("g_stick[hand]=g_stickClick[hand]=g_lower[hand]=g_menu[hand]=g_squeeze[hand]=XR_NULL_HANDLE;") &&
    File.ReadAllText(Path.Combine(root, "src", "render", "NativeHost", "Host.h")).Contains("int stick; int lower; int menu; float squeeze;") &&
    driver.Contains("public float Squeeze;"));
Check("d206-config-key-is-validated-in-both-places-and-drilled",
    host.Contains("public string TurretCycle { get; set; } = \"Stick\";") &&
    File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHost.cs")).Contains("invalid turret cycle source: Stick|Dpad|Special|Off") &&
    schema.Contains("\"TurretCycle\"") && installer.Contains("TurretCycle must be Stick, Dpad, Special or Off.") &&
    drill.Contains("TurretCycle = $TurretCycle;"));

// D-213: smooth view turn policy and source guards.
Check("d213-smooth-speed-clamps-and-defaults",
    SmoothTurnPolicy.ClampDegreesPerSecond(120f) == 120f &&
    SmoothTurnPolicy.ClampDegreesPerSecond(10f) == 30f &&
    SmoothTurnPolicy.ClampDegreesPerSecond(500f) == 240f &&
    SmoothTurnPolicy.ClampDegreesPerSecond(float.NaN) == 120f);
Check("d213-smooth-deadzone-and-hysteresis",
    SmoothTurnPolicy.Next(false, 0.20f, 120f, 0.02f, out bool e0) == 0f && !e0 &&
    SmoothTurnPolicy.Next(false, 0.50f, 120f, 0.02f, out bool e1) > 0f && e1 &&
    SmoothTurnPolicy.Next(true, 0.20f, 120f, 0.02f, out bool e2) > 0f && e2 &&
    SmoothTurnPolicy.Next(true, 0.10f, 120f, 0.02f, out bool e3) == 0f && !e3);
Check("d213-smooth-continuous-intermediate-angles-and-caps",
    Near(SmoothTurnPolicy.Next(false, 1.0f, 120f, 1f / 60f, out _), 2f) &&
    Near(SmoothTurnPolicy.Next(false, -1.0f, 120f, 1f / 60f, out _), -2f) &&
    SmoothTurnPolicy.Next(false, 1.0f, 240f, 0.5f, out _) == SmoothTurnPolicy.MaxDegreesPerFrame &&
    SmoothTurnPolicy.Next(false, float.NaN, 120f, 0.02f, out _) == 0f);
string openXrHostSrc = File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHost.cs"));
Check("d213-host-and-driver-wire-smooth-turn",
    inputSrc.Contains("SmoothTurnPolicy.Next(smoothEngaged, x, SmoothDegreesPerSecond, deltaSeconds, out smoothEngaged)") &&
    inputSrc.Contains("if (smooth && Time.timeScale <= 0.01f)") &&
    driver.Contains("TurnTick(status, available, Time.deltaTime);") &&
    openXrHostSrc.Contains("ApplySnap(handAim.TakeTurn(out int snapSteps), snapSteps);") &&
    openXrHostSrc.Split("handAim.TurnMode = turnMode;").Length - 1 == 2 &&
    openXrHostSrc.Contains("viewTurnedDegrees = viewTurnedDegrees,"));

Console.WriteLine($"RESULT {passed} hand aim fixtures passed; math/policy and source guards only, no game/headset/shot-direction gate");