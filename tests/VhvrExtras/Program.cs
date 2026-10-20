using System.Runtime.InteropServices;
using VhvrExtras;

int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
    passed++;
}

// D-207 / BUG-205: the native host appends float squeeze (D-206), making each hand 64 bytes and the
// complete ReadHands output 144 bytes. These guards exercise the exact shared interop structs used by Extras.
Check("native-hand-pose-is-64-bytes", Marshal.SizeOf<NativeHandPose>() == 64);
Check("native-hands-status-is-144-bytes", Marshal.SizeOf<NativeHandsStatus>() == 144);
Check("native-squeeze-field-offset-is-60", Marshal.OffsetOf<NativeHandPose>(nameof(NativeHandPose.Squeeze)).ToInt32() == 60);
Check("native-left-hand-offset-is-16", Marshal.OffsetOf<NativeHandsStatus>(nameof(NativeHandsStatus.Left)).ToInt32() == 16);
Check("native-right-hand-offset-is-80", Marshal.OffsetOf<NativeHandsStatus>(nameof(NativeHandsStatus.Right)).ToInt32() == 80);
NativeHandsAbi.AssertLayout();
Check("native-hand-abi-runtime-guard-passes", true);

float AngleBetween(float[] a, float[] b)
{
    var dot = (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2]);
    var lengthA = MathF.Sqrt((a[0] * a[0]) + (a[1] * a[1]) + (a[2] * a[2]));
    var lengthB = MathF.Sqrt((b[0] * b[0]) + (b[1] * b[1]) + (b[2] * b[2]));
    return MathF.Acos(Math.Clamp(dot / (lengthA * lengthB), -1f, 1f)) * 180f / MathF.PI;
}

// --- requested behaviour 5: re-send a fire signal at a fixed interval while the trigger is held (D-176:
// every weapon, crossbow-like one-press weapons included). A tick the shipped build already fired only
// restarts the interval, so an auto weapon is never double-fired.
float lastSignal = SemiAutoFirePolicy.NoSignal;
bool Signal(bool held, bool alreadyFiring, float now, float interval) =>
    SemiAutoFirePolicy.ShouldSignal(held, alreadyFiring, now, interval, ref lastSignal);

Check("hold-starts-timer-no-immediate-signal", !Signal(true, false, 1.0f, 0.6f));
Check("no-signal-before-interval", !Signal(true, false, 1.5f, 0.6f));
Check("signal-at-interval", Signal(true, false, 1.7f, 0.6f));
Check("no-second-signal-within-interval", !Signal(true, false, 1.8f, 0.6f));
Check("second-signal-after-interval", Signal(true, false, 2.4f, 0.6f));
Check("release-resets-timer", !Signal(false, false, 2.5f, 0.6f));
Check("rehold-restarts-timer", !Signal(true, false, 2.6f, 0.6f));
Check("rehold-signals-after-interval", Signal(true, false, 3.3f, 0.6f));
Check("not-held-never-signals", !Signal(false, false, 3.4f, 0.6f));
Check("game-firing-restarts-interval", !Signal(true, true, 3.5f, 0.6f) && !Signal(true, true, 4.2f, 0.6f));
Check("auto-weapon-gets-no-extra-signal", !Signal(true, true, 4.9f, 0.6f));
Check("zero-interval-is-disabled", !Signal(true, false, 6.0f, 0f));
Check("nan-time-rejected", !Signal(true, false, float.NaN, 0.6f));
Check("infinite-time-rejected", !Signal(true, false, float.PositiveInfinity, 0.6f));

// --- requested behaviour 4 (D-186, user decision 2026-10-16): the Y/B ability pulse is REMOVED. Y/B carries the
// weapon's secondary/alt fire from the CORE now (WeaponRuntimeBridge: fire2Pressing /
// is_holding_down_seccond_shoot_button follow the face buttons, never the trigger) plus the head-near reload
// gesture. The old FaceAbilityPolicy fixtures left with the feature; that wiring is game-side and is covered by
// tests/verify-bindings.ps1 and the headset checklist instead of pure policy fixtures.

// --- requested behaviour 3: UI scale is applied to the target world width the shipped build uses.
Check("ui-scale-1.5-grows-width", MathF.Abs(UiScalePolicy.ScaleWidth(1.9f, 1.5f) - 2.85f) < 1e-4f);
Check("ui-scale-clamps-high", MathF.Abs(UiScalePolicy.ScaleWidth(2f, 9f) - 6f) < 1e-4f);
Check("ui-scale-clamps-low", MathF.Abs(UiScalePolicy.ScaleWidth(2f, 0.1f) - 1f) < 1e-4f);
Check("ui-scale-nan-falls-back-to-1", MathF.Abs(UiScalePolicy.ScaleWidth(2f, float.NaN) - 2f) < 1e-4f);
Check("ui-scale-rejects-absurd-values", !UiScalePolicy.IsUsable(4f) && !UiScalePolicy.IsUsable(0.4f) && UiScalePolicy.IsUsable(1.5f));
Check("ui-scale-keeps-invalid-width", UiScalePolicy.ScaleWidth(-1f, 1.5f) < 0f);

// --- requested behaviour 2: modelled cooldown from a weapon's fire rate, one pulse per cycle.
Check("cooldown-interval-from-fire-rate", MathF.Abs(CooldownHapticPolicy.Interval(2f, 0.12f, 3f, 0.5f) - 0.5f) < 1e-4f);
Check("cooldown-interval-clamps-slow-weapon", MathF.Abs(CooldownHapticPolicy.Interval(0.1f, 0.12f, 3f, 0.5f) - 3f) < 1e-4f);
Check("cooldown-interval-clamps-fast-weapon", MathF.Abs(CooldownHapticPolicy.Interval(100f, 0.12f, 3f, 0.5f) - 0.12f) < 1e-4f);
Check("cooldown-interval-falls-back-when-unknown", MathF.Abs(CooldownHapticPolicy.Interval(-1f, 0.12f, 3f, 0.5f) - 0.5f) < 1e-4f);
var cooldown = CooldownHapticState.Create();
Check("cooldown-no-pulse-without-shot", !CooldownHapticPolicy.Tick(false, 1f, 0.5f, ref cooldown));
Check("cooldown-shot-arms-cycle", !CooldownHapticPolicy.Tick(true, 2f, 0.5f, ref cooldown));
Check("cooldown-not-ready-yet", !CooldownHapticPolicy.Tick(false, 2.4f, 0.5f, ref cooldown));
Check("cooldown-pulses-when-ready", CooldownHapticPolicy.Tick(false, 2.5f, 0.5f, ref cooldown));
Check("cooldown-pulses-once-only", !CooldownHapticPolicy.Tick(false, 3f, 0.5f, ref cooldown));
Check("cooldown-new-shot-rearms", !CooldownHapticPolicy.Tick(true, 3.2f, 0.5f, ref cooldown) && CooldownHapticPolicy.Tick(false, 3.7f, 0.5f, ref cooldown));
Check("cooldown-rejects-nan-time", !CooldownHapticPolicy.Tick(false, float.NaN, 0.5f, ref cooldown));

// D-187: SecondaryWeapon (robot arm) ready-edge and native busy-retry policy.
var armState = ArmReadyState.Create();
Check("arm-deploy-not-started-silent", !CooldownHapticPolicy.ShouldPulseSecondaryArm(false, false, true, ref armState));
Check("arm-initial-deploy-ready-silent", !CooldownHapticPolicy.ShouldPulseSecondaryArm(true, true, true, ref armState));
Check("arm-staying-ready-silent", !CooldownHapticPolicy.ShouldPulseSecondaryArm(true, true, true, ref armState));
Check("arm-firing-arms-cooldown", !CooldownHapticPolicy.ShouldPulseSecondaryArm(true, false, false, ref armState));
Check("arm-attack-ended-still-cooling-down", !CooldownHapticPolicy.ShouldPulseSecondaryArm(true, false, true, ref armState));
Check("arm-cooldown-finished-requests-pulse", CooldownHapticPolicy.ShouldPulseSecondaryArm(true, true, true, ref armState));
CooldownHapticPolicy.RecordResult(6, ref armState); // native host busy -> must retry next frame
Check("arm-busy-result-retries-next-frame", CooldownHapticPolicy.ShouldPulseSecondaryArm(true, true, true, ref armState));
CooldownHapticPolicy.RecordResult(0, ref armState); // queued -> disarms until next shot
Check("arm-queued-result-disarms-repeat", !CooldownHapticPolicy.ShouldPulseSecondaryArm(true, true, true, ref armState));
var midCooldownArm = ArmReadyState.Create();
Check("arm-discovered-mid-cooldown-arms-without-immediate-pulse", !CooldownHapticPolicy.ShouldPulseSecondaryArm(true, false, true, ref midCooldownArm));
Check("arm-discovered-mid-cooldown-pulses-when-ready", CooldownHapticPolicy.ShouldPulseSecondaryArm(true, true, true, ref midCooldownArm));

// --- requested behaviour 1: panel geometry (1.5 m away, 40 deg wide) and controller ray maths.
Check("panel-width-at-1.5m-40deg", MathF.Abs(PanelGeometryPolicy.Width(1.5f, 40f) - 1.09191f) < 1e-3f);
Check("panel-height-keeps-16by9", MathF.Abs(PanelGeometryPolicy.Height(1.09191f, 16f / 9f) - 0.61420f) < 1e-3f);
Check("panel-rejects-zero-distance", PanelGeometryPolicy.Width(0f, 40f) == 0f);
Check("panel-clamps-silly-fov", MathF.Abs(PanelGeometryPolicy.Width(1f, 500f) - PanelGeometryPolicy.Width(1f, 120f)) < 1e-4f);
float[] panelCenter = PanelGeometryPolicy.Center(new[] { 0f, 1.6f, 0f }, new[] { 0f, 0f, 1f }, 1.5f);
Check("panel-centre-is-in-front-of-the-head", MathF.Abs(panelCenter[2] - 1.5f) < 1e-4f && MathF.Abs(panelCenter[1] - 1.6f) < 1e-4f);
float[] headPos = { 0f, 1.6f, 0f };
float[] panelNormal = { 0f, 0f, -1f };
float[] panelRight = { 1f, 0f, 0f };
float[] panelUp = { 0f, 1f, 0f };
Check("panel-ray-hits-the-centre",
    PanelGeometryPolicy.TryRayPanel(headPos, new[] { 0f, 0f, 1f }, panelCenter, panelNormal, panelRight, panelUp, 0.54595f, 0.30710f, out var travel, out var u, out var v) &&
    MathF.Abs(travel - 1.5f) < 1e-3f && MathF.Abs(u) < 1e-3f && MathF.Abs(v) < 1e-3f);
Check("panel-ray-half-right-edge",
    PanelGeometryPolicy.TryRayPanel(headPos, new[] { 0.273f, 0f, 1.5f }, panelCenter, panelNormal, panelRight, panelUp, 0.54595f, 0.30710f, out _, out var uHalf, out _) &&
    MathF.Abs(uHalf - 0.5f) < 1e-3f);
Check("panel-ray-steep-lateral-miss",
    !PanelGeometryPolicy.TryRayPanel(headPos, new[] { 0.5f, 0f, 1f }, panelCenter, panelNormal, panelRight, panelUp, 0.54595f, 0.30710f, out _, out _, out _));
Check("panel-ray-misses-outside",
    !PanelGeometryPolicy.TryRayPanel(headPos, new[] { 3f, 0f, 1f }, panelCenter, panelNormal, panelRight, panelUp, 0.54595f, 0.30710f, out _, out _, out _));
Check("panel-ray-parallel-rejected",
    !PanelGeometryPolicy.TryRayPanel(headPos, new[] { 1f, 0f, 0f }, panelCenter, panelNormal, panelRight, panelUp, 0.54595f, 0.30710f, out _, out _, out _));
Check("panel-ray-behind-rejected",
    !PanelGeometryPolicy.TryRayPanel(new[] { 0f, 1.6f, 5f }, new[] { 0f, 0f, 1f }, panelCenter, panelNormal, panelRight, panelUp, 0.54595f, 0.30710f, out _, out _, out _));
PanelGeometryPolicy.MapToPixels(0f, 0f, 1280, 720, out var px, out var py);
Check("panel-centre-maps-to-pixel-centre", MathF.Abs(px - 640f) < 1e-3f && MathF.Abs(py - 360f) < 1e-3f);
PanelGeometryPolicy.MapToPixels(-1f, 1f, 1280, 720, out var leftTopX, out var leftTopY);
Check("panel-corner-maps-to-texture-corner", MathF.Abs(leftTopX) < 1e-3f && MathF.Abs(leftTopY) < 1e-3f);

// --- requested behaviour 1: controller pose -> world, checked against the shipped HandAim expectations.
float[] unityPos = PoseMathPolicy.UnityPosition(0.1f, 1.2f, -0.5f);
Check("pose-flips-z", MathF.Abs(unityPos[0] - 0.1f) < 1e-5f && MathF.Abs(unityPos[1] - 1.2f) < 1e-5f && MathF.Abs(unityPos[2] - 0.5f) < 1e-5f);
Check("pose-identity-orientation", PoseMathPolicy.TryUnityOrientation(0f, 0f, 0f, 1f, out var identity) && MathF.Abs(identity[3] - 1f) < 1e-5f);
Check("pose-rejects-bad-quaternion", !PoseMathPolicy.TryUnityOrientation(0f, 0f, 0f, 2f, out _));
float[] yaw90 = { 0f, MathF.Sin(MathF.PI / 4f), 0f, MathF.Cos(MathF.PI / 4f) };
Check("pose-to-world-identity-anchor",
    PoseMathPolicy.TryToWorld(new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f, 1f }, 0.1f, 1.2f, -0.5f, 0f, 0f, 0f, 1f, out var worldPos, out _) &&
    MathF.Abs(worldPos[0] - 0.1f) < 1e-4f && MathF.Abs(worldPos[1] - 1.2f) < 1e-4f && MathF.Abs(worldPos[2] - 0.5f) < 1e-4f);
Check("pose-yaw-anchor-rotates-position",
    PoseMathPolicy.TryToWorld(new[] { 0f, 0f, 0f }, yaw90, 0f, 0f, -1f, 0f, 0f, 0f, 1f, out var yawPos, out _) &&
    MathF.Abs(yawPos[0] - 1f) < 1e-3f && MathF.Abs(yawPos[2]) < 1e-3f);
Check("pose-yaw-anchor-rotates-forward",
    PoseMathPolicy.TryToWorld(new[] { 0f, 0f, 0f }, yaw90, 0f, 0f, -1f, 0f, 0f, 0f, 1f, out _, out var yawRot) &&
    MathF.Abs(PoseMathPolicy.Forward(yawRot)[0] - 1f) < 1e-3f);
Check("pose-forward-of-identity-is-plus-z", MathF.Abs(PoseMathPolicy.Forward(new[] { 0f, 0f, 0f, 1f })[2] - 1f) < 1e-5f);
Check("pose-rejects-nan-position", !PoseMathPolicy.TryToWorld(new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f, 1f }, float.NaN, 0f, 0f, 0f, 0f, 0f, 1f, out _, out _));

// --- D-201: the menu panel AND the menu laser must be built in the SAME frame as the rendered view (the HMD
// frame). The 0.9.4 install froze the panel from eyes[0].transform, which Camera.CopyFrom had set to the game
// camera's pose (Unity docs: "It will also set this camera's transform to match the other camera"), so the panel
// landed at anchorPosition + DistanceMeters * ArenaCamera.forward while host.json already reported the HMD frame
// (view row 2 = (0, 0, -1)). Numbers below are the 2026-10-18 19:57 run.
float[] d201Identity = { 0f, 0f, 0f, 1f };
float[] d201ArenaCamera = { 0f, -MathF.Sin(MathF.PI / 4f), 0f, MathF.Cos(MathF.PI / 4f) }; // receipt: (0, -0.7071068, 0, 0.7071068)
float[] d201AnchorPosition = { 76.287055f, 34.000000f, 43.753567f }; // host.json eye position minus the OpenXR eye offset
float[] d201HeadPosition = { 76.255480f, 32.466396f, 43.753567f };
Check("menu-pointer-head-frame-is-identity",
    PoseMathPolicy.MenuPointerAnchor(true, d201ArenaCamera) is var d201HeadAnchor &&
    MathF.Abs(d201HeadAnchor[0]) < 1e-6f && MathF.Abs(d201HeadAnchor[1]) < 1e-6f &&
    MathF.Abs(d201HeadAnchor[2]) < 1e-6f && MathF.Abs(d201HeadAnchor[3] - 1f) < 1e-6f);
Check("menu-pointer-game-camera-frame-is-passthrough",
    ReferenceEquals(PoseMathPolicy.MenuPointerAnchor(false, d201ArenaCamera), d201ArenaCamera));
Check("menu-pointer-malformed-anchor-falls-back-to-head-frame",
    PoseMathPolicy.MenuPointerAnchor(false, new[] { 0f, 0f, 1f })[3] == 1f);
Check("menu-yaw-of-identity-is-zero", MathF.Abs(PoseMathPolicy.YawDegrees(d201Identity)) < 1e-3f);
Check("menu-yaw-of-the-receipt-arena-camera-is-minus-90",
    MathF.Abs(PoseMathPolicy.YawDegrees(d201ArenaCamera) + 90f) < 1e-3f);
Check("menu-head-panel-delta-is-zero-in-the-head-frame",
    MathF.Abs(PoseMathPolicy.ForwardAngleDegrees(d201Identity, d201Identity)) < 1e-3f);
Check("menu-game-camera-frame-is-90-degrees-off-the-view",
    MathF.Abs(PoseMathPolicy.ForwardAngleDegrees(d201Identity, d201ArenaCamera) - 90f) < 1e-3f);
float[] d201HeadFrameCenter = PanelGeometryPolicy.Center(d201HeadPosition, PoseMathPolicy.Forward(d201Identity), 2f);
Check("menu-panel-head-frame-sits-in-front-of-the-eyes",
    MathF.Abs(d201HeadFrameCenter[0] - d201HeadPosition[0]) < 1e-3f &&
    MathF.Abs(d201HeadFrameCenter[1] - d201HeadPosition[1]) < 1e-3f &&
    MathF.Abs(d201HeadFrameCenter[2] - (d201HeadPosition[2] + 2f)) < 1e-3f);
float[] d201StaleFrameCenter = PanelGeometryPolicy.Center(d201AnchorPosition, PoseMathPolicy.Forward(d201ArenaCamera), 2f);
Check("menu-panel-stale-frame-reproduces-the-0.9.4-log",
    MathF.Abs(d201StaleFrameCenter[0] - 74.287f) < 1e-2f &&
    MathF.Abs(d201StaleFrameCenter[1] - 34.000f) < 1e-2f &&
    MathF.Abs(d201StaleFrameCenter[2] - 43.7536f) < 1e-2f);
Check("menu-stale-panel-is-90-degrees-off-the-head-forward",
    MathF.Abs(AngleBetween(
        new[]
        {
            d201StaleFrameCenter[0] - d201HeadPosition[0],
            d201StaleFrameCenter[1] - d201HeadPosition[1],
            d201StaleFrameCenter[2] - d201HeadPosition[2]
        },
        PoseMathPolicy.Forward(d201Identity)) - 90f) < 0.1f);

Console.WriteLine($"RESULT {passed} extras fixtures passed; policy + native ABI layout only, no game/headset/behaviour gate");
