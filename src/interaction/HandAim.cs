using System;
using VampireHuntersVR.Render;

namespace VampireHuntersVR.Interaction;

// Pure per-hand aim math/policy shared by the plugin and net10 fixtures (no Unity types).
// OpenXR local space -> Unity world uses exactly the same mapping as the eye cameras in OpenXrHost.PrepareCameras.
internal readonly record struct HandSample(bool Active, float X, float Y, float Z, float Qx, float Qy, float Qz, float Qw);

internal enum HandAimState
{
    WaitingForWeapons,
    RejectedSharedContainer,
    RejectedImplausibleRest,
    Driving,
    HoldingLastPose,
    ReleasedInactive,
}

internal static class HandAimPolicy
{
    // Native hand samples arrive once per submitted XR frame (several Unity frames apart).
    public const int FreshTicks = 20;
    public const int ReleaseAfterTicks = 90;
    public const float MaxRestLocalDistance = 10f;
    // D-153 (TOOL-073): the per-hand weapon rest-twist dial is measured in degrees about the muzzle axis. A non-finite
    // value is 0 and the magnitude is clamped below half a turn, so the dial can never flip a weapon's facing.
    public const float MaxWeaponTwistDegrees = 180f;

    public static float ClampWeaponTwist(float degrees) =>
        float.IsFinite(degrees) ? Math.Clamp(degrees, -MaxWeaponTwistDegrees, MaxWeaponTwistDegrees) : 0f;

    // D-112: only an authored pose may establish rest; controller-driven coordinates are not rest.
    public static bool ShouldCaptureRest(bool captured, bool shared, float x, float y, float z) =>
        !captured && !shared && HandAimMath.RestPlausible(x, y, z, MaxRestLocalDistance);

    public static bool PreserveWorldForGaze(bool hasWritten) => hasWritten;

    // Fail closed: a missing/ambiguous container or an implausible rest pose never drives anything.
    public static HandAimState Next(bool hasContainer, bool sharedWithOtherHand, bool restPlausible,
        bool handActiveAndFresh, int staleTicks)
    {
        if (!hasContainer) return HandAimState.WaitingForWeapons;
        if (sharedWithOtherHand) return HandAimState.RejectedSharedContainer;
        if (!restPlausible) return HandAimState.RejectedImplausibleRest;
        if (handActiveAndFresh) return HandAimState.Driving;
        return staleTicks >= ReleaseAfterTicks ? HandAimState.ReleasedInactive : HandAimState.HoldingLastPose;
    }
}

internal enum TriggerEdge { None, Pressed, Released }

// Pure trigger latch with hysteresis. A hand that may not fire (paused, not driving, no weapon) is always released.
internal static class HandFirePolicy
{
    public const float PressAt = 0.6f;
    public const float ReleaseAt = 0.35f;

    // Stock fire consumes the press; Weapon.update owns sustained cadence, not repeated starts.
    // D-180: NoInlining on purpose. The extras plugin patches this method with Harmony; a JIT that inlines this tiny
    // static into FireTick bypasses the patch entirely (observed: fireCalls == triggerPresses over 18 s of hold).
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool StartThisTick(TriggerEdge edge, bool held, bool stockCadence) =>
        stockCadence ? edge == TriggerEdge.Pressed : held;

    public static TriggerEdge Next(bool wasHeld, float trigger, bool allowed, out bool held)
    {
        if (!allowed || float.IsNaN(trigger) || float.IsInfinity(trigger))
        {
            held = false;
            return wasHeld ? TriggerEdge.Released : TriggerEdge.None;
        }
        if (!wasHeld)
        {
            held = trigger >= PressAt;
            return held ? TriggerEdge.Pressed : TriggerEdge.None;
        }
        held = trigger > ReleaseAt;
        return held ? TriggerEdge.None : TriggerEdge.Released;
    }
}

// D-100/D-114: a stick direction read as a HELD button (right stick up = blood ability, down = special ability).
// Hysteresis like the trigger latch. The dominant-axis rule keeps a diagonal push (a snap turn that drifts up) from firing it.
internal static class StickLatchPolicy
{
    public const float PressAt = 0.7f;
    public const float ReleaseAt = 0.3f;

    // value: the stick component toward the wanted direction (positive = pushed that way). other: the perpendicular component.
    public static bool Next(bool wasHeld, float value, float other, bool allowed)
    {
        if (!allowed || float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(other) || float.IsInfinity(other)) return false;
        if (!wasHeld) return value >= PressAt && value > MathF.Abs(other);
        return value > ReleaseAt;
    }
}

// D-114: both virtual-pad and native fallback routes consume the same right-stick ability directions.
internal static class AbilityStickPolicy
{
    // D-180: NoInlining so the extras' Harmony postfix (face-button Y/B) cannot be bypassed by the JIT.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static (bool Special, bool Blood) Next(bool specialHeld, bool bloodHeld, float y, float x, bool allowed) =>
        (StickLatchPolicy.Next(specialHeld, -y, x, allowed), StickLatchPolicy.Next(bloodHeld, y, x, allowed));
}

// D-101: reload gesture = the upper face button (Y / B) pressed while that controller is beside the headset.
internal static class HeadReachPolicy
{
    public const float NearMeters = 0.30f;

    public static bool Near(float distance) => float.IsFinite(distance) && distance >= 0f && distance <= NearMeters;
}

// D-102: the in-game HUD (health, blood gauge, dash, minimap, score, combo) is locked to the head while playing, and the dark
// translucent panel behind it is culled. D-108 also puts every paused menu on the full HMD pose.
internal static class HudLayoutPolicy
{
    private static readonly string[] HeadLockedNames = { "Player HUD", "HealthBarMainHUD", "ComboAndCrosshairs", "Canvas - Score", "FloatingIcons" };

    // path: the full hierarchy path of a root canvas; only its own (last) name decides.
    public static bool IsHeadLocked(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains("Level Up", StringComparison.Ordinal)) return false;
        string leaf = path.Substring(path.LastIndexOf('/') + 1);
        foreach (var name in HeadLockedNames) if (leaf.Contains(name, StringComparison.Ordinal)) return true;
        return false;
    }

    // D-113: additional 15 cm below the D-112 (-0.10 m) sitting.
    public const float DefaultVerticalOffset = -0.25f;
    public static float ClampVerticalOffset(float value) => float.IsFinite(value) ? Math.Clamp(value, -0.30f, 0.10f) : DefaultVerticalOffset;

    // D-108: every paused canvas, not only Level Up, uses the HMD pose without the old heading dead zone.
    public static bool UseHeadPose(bool headLocked, bool paused) => headLocked || paused;

    // D-105: the level-up card screens (any canvas under "Level Up") sit rigidly in front of the headset while the game is paused.
    public static bool IsLevelUp(string? path) => !string.IsNullOrEmpty(path) && path.Contains("Level Up", StringComparison.Ordinal);
}

// D-107: the level-up canvas was placed by its PIVOT, but the pivot / anchors / card layout of the game canvas are not centred, so the
// content showed up above the head (upper left or upper right, clipped). The content bounds are measured in canvas-local units and
// the centre of those bounds is put straight ahead; a content block bigger than the comfortable view is scaled down to fit.
internal static class UiContentCenterPolicy
{
    public const float MaxOffsetFraction = 1.0f;

    // min/max: bounds of the visible content in canvas-local units (origin = pivot). Returns false for anything not trustworthy,
    // in which case D-108 keeps the previous content centre or falls back to the rectangle centre, never the pivot.
    public static bool TryMeasure(float minX, float minY, float maxX, float maxY, float canvasWidth, float canvasHeight, out float centerX, out float centerY)
    {
        centerX = 0f; centerY = 0f;
        if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY) || !float.IsFinite(canvasWidth) || !float.IsFinite(canvasHeight)) return false;
        if (!(maxX > minX) || !(maxY > minY) || !(canvasWidth > 0f) || !(canvasHeight > 0f)) return false;
        float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
        if (MathF.Abs(cx) > canvasWidth * MaxOffsetFraction || MathF.Abs(cy) > canvasHeight * MaxOffsetFraction) return false;
        centerX = cx; centerY = cy;
        return true;
    }

    // D-108: safe first-frame fallback for any pivot. A bottom-left (0,0) pivot needs half the rectangle subtracted.
    public static float RectCenter(float min, float max) => (min + max) * 0.5f;

    // Uniform shrink (never enlarges) so the content block is at most maxWidth x maxHeight metres. Invalid input leaves the size alone.
    public static float FitScale(float widthMeters, float heightMeters, float maxWidth, float maxHeight)
    {
        if (!float.IsFinite(widthMeters) || !float.IsFinite(heightMeters) || !(widthMeters > 0f) || !(heightMeters > 0f) || !(maxWidth > 0f) || !(maxHeight > 0f)) return 1f;
        return MathF.Min(1f, MathF.Min(maxWidth / widthMeters, maxHeight / heightMeters));
    }
}

// D-105: the full-screen dark backdrop of a paused menu / level-up screen is hidden outright (CanvasRenderer cull), not just dimmed:
// the alpha cap of D-096 left it black (video, 2026-10-10 21:03). Only a wide AND tall, dark, non-Filled graphic qualifies, so
// cards, slots and stat boxes (narrow) stay. Tint colour decides; unknown values never hide anything.
internal static class UiBackdropPolicy
{
    public const float MaxChannel = 0.35f;
    public const float MinAlpha = 0.02f;
    public const float MinWidthFraction = 0.85f;
    public const float MinHeightFraction = 0.5f;

    public static bool ShouldHide(float r, float g, float b, float a, float widthFraction, float heightFraction, bool filled)
    {
        if (filled) return false;
        if (!float.IsFinite(r) || !float.IsFinite(g) || !float.IsFinite(b) || !float.IsFinite(a) || !float.IsFinite(widthFraction) || !float.IsFinite(heightFraction)) return false;
        return MathF.Max(r, MathF.Max(g, b)) <= MaxChannel && a >= MinAlpha && widthFraction >= MinWidthFraction && heightFraction >= MinHeightFraction;
    }
}

// D-155 (TOOL-074): the user's decision - the in-game HUD Bottom Shadow stays hidden (it is the one backdrop that is the mod's
// own doing), but every OTHER backdrop is game content and must be left exactly as authored (level-up black background, menu
// backdrops, chest / boss-chest screens). The pre-D-155 blanket rules (D-096 dim, D-105/D-106/D-112 cull) are therefore now an
// explicit opt-in instead of the default. Three modes, no silent middle ground:
//   authored: never cull, never touch the tint alpha - every backdrop keeps the look the game gave it (the UI is still moved in
//             front of the head, which is the D-090 in-VR UI feature, not a backdrop edit).
//   dim:      keep the D-096 alpha cap on wide dark panels so the scene shows through, but never cull anything.
//   hide:     the pre-D-155 policy (alpha cap + the D-105/D-106/D-112 cull of wide dark / translucent panels).
// An unknown mode is never guessed: the host fails loud at Initialize (same contract as EyePreset/PerfProfile).
internal static class GameBackdropPolicy
{
    public const string Authored = "authored";
    public const string Dim = "dim";
    public const string Hide = "hide";

    public static bool IsValid(string? mode) => mode == Authored || mode == Dim || mode == Hide;

    // Only the explicit legacy mode may cull a backdrop; authored and dim both leave the CanvasRenderer alone.
    public static bool ShouldCullBackdrops(string? mode) => mode == Hide;

    // Whether the wide dark panel may still have its alpha capped (D-096). authored = the game's own alpha, untouched.
    public static bool ShouldDim(string? mode) => mode != Authored;
}

// D-105: which canvas the UI ray talks to. fp3 had 10 hovers and 0 clicks: the old code took the NEAREST INFINITE PLANE, even when the
// hit point lay outside that canvas or on a canvas with nothing to press, so the ray stuck on an empty side panel (screenshots) and never
// reached the cards. Now only a canvas whose rect CONTAINS the hit point counts, and one with a selectable under the point wins.
// This has nothing to do with the input device (keyboard / Xbox): it is pure geometry.
internal static class UiRayPolicy
{
    // Returns the index of the chosen candidate or -1. distance: ray length to the canvas plane. inside: the hit point is within the
    // canvas rect. selectable: a pressable element lies under the point.
    public static int Choose(IReadOnlyList<float> distance, IReadOnlyList<bool> inside, IReadOnlyList<bool> selectable)
    {
        int best = -1;
        for (int i = 0; i < distance.Count && i < inside.Count && i < selectable.Count; i++)
        {
            if (!inside[i] || !float.IsFinite(distance[i]) || distance[i] <= 0f) continue;
            if (best < 0) { best = i; continue; }
            if (selectable[i] != selectable[best]) { if (selectable[i]) best = i; continue; }
            if (distance[i] < distance[best]) best = i;
        }
        return best;
    }
}

internal static class HudDarkPanelPolicy
{
    public const float MaxChannel = 0.35f;
    public const float MinAlpha = 0.02f;
    public const float MaxAlpha = 0.95f;
    public const float MinAreaFraction = 0.02f;

    public static bool IsDarkTranslucent(float r, float g, float b, float a)
    {
        if (!float.IsFinite(r) || !float.IsFinite(g) || !float.IsFinite(b) || !float.IsFinite(a)) return false;
        return MathF.Max(r, MathF.Max(g, b)) <= MaxChannel && a >= MinAlpha && a <= MaxAlpha;
    }

    // widthFraction / heightFraction: the graphic's size relative to its canvas. Thin bars (health, blood) stay below the area limit.
    public static bool IsPanelSized(float widthFraction, float heightFraction)
    {
        if (!float.IsFinite(widthFraction) || !float.IsFinite(heightFraction) || widthFraction <= 0f || heightFraction <= 0f) return false;
        return widthFraction * heightFraction >= MinAreaFraction;
    }

    // Filled images are gauges (health, blood, dash cooldown), never the backdrop.
    public static bool ShouldHide(float r, float g, float b, float a, float widthFraction, float heightFraction, bool filled)
        => !filled && IsDarkTranslucent(r, g, b, a) && IsPanelSized(widthFraction, heightFraction);
}

// D-106: the translucent panel behind the in-game HUD (screenshot, 2026-10-10: grey-blue haze under health / level / 0-3 counter)
// survived D-102 because fp3 recorded hudDarkHidden=0 and an EMPTY candidate list: no graphic passed the "dark tint" test, so the
// panel's tint is not dark (a white/grey tint over a translucent sprite). This rule is colour-agnostic: any non-Filled, translucent,
// panel-sized and wide graphic on a head-locked HUD canvas goes. Gauges (Filled), thin bars (small area), opaque art (minimap, icons)
// and invisible graphics stay. Unknown values never hide anything.
internal static class HudTranslucentPanelPolicy
{
    // D-112 receipt: white Graphic alpha=1; transparency lives in the sprite texture.
    // Exact hierarchy + art identity only: never infer that opaque minimap/gauge art is a backdrop.
    public static bool IsKnownBottomShadow(string? path, string? art, bool filled) =>
        !filled && path != null && path.EndsWith("/Player HUD/Bottom Shadow", StringComparison.Ordinal) &&
        string.Equals(art, "HUD Bottom Shadow", StringComparison.Ordinal);
    public const float MinAlpha = 0.02f;
    public const float MaxAlpha = 0.95f;
    public const float MinAreaFraction = 0.02f;
    public const float MinWidthFraction = 0.15f;

    public static bool ShouldHide(float a, float widthFraction, float heightFraction, bool filled)
    {
        if (filled) return false;
        if (!float.IsFinite(a) || !float.IsFinite(widthFraction) || !float.IsFinite(heightFraction)) return false;
        if (widthFraction <= 0f || heightFraction <= 0f) return false;
        return a >= MinAlpha && a <= MaxAlpha && widthFraction >= MinWidthFraction && widthFraction * heightFraction >= MinAreaFraction;
    }
}

// D-103: one short haptic pulse when a controller ENTERS the reload reach beside the headset (HeadReachPolicy.NearMeters).
// Edge only: staying near gives nothing more. It re-arms only after the hand has left by LeaveMeters (hysteresis), so a hand
// trembling on the 0.30 m line does not buzz; MinIntervalSeconds is a second guard. Unknown distance counts as far.
internal static class HeadHapticPolicy
{
    public const float LeaveMeters = 0.36f;
    public const float MinIntervalSeconds = 0.4f;
    public const float DefaultAmplitude = 0.4f;
    public const int DefaultMilliseconds = 60;

    // wasNear: this hand was already inside last frame. Returns true exactly on the entering frame.
    public static bool Next(bool wasNear, float distance, float secondsSinceLastPulse, out bool near)
    {
        if (!float.IsFinite(distance) || distance < 0f) { near = false; return false; }
        if (wasNear) { near = distance <= LeaveMeters; return false; }
        near = HeadReachPolicy.Near(distance);
        return near && !(secondsSinceLastPulse < MinIntervalSeconds);
    }

    public static float ClampAmplitude(float value) => float.IsFinite(value) ? MathF.Min(1f, MathF.Max(0f, value)) : DefaultAmplitude;
    public static int ClampMilliseconds(int value) => Math.Min(500, Math.Max(0, value));
}

internal static class ReloadLatchPolicy
{
    // Starts only on the press edge and only when the controller is near the headset; then holds until the button is released,
    // so moving the hand away after the press does not cut the pad button short. Pressing far away and moving in does not start it.
    public static bool Next(bool wasHeld, bool faceWasDown, bool faceDown, float headDistance)
    {
        if (!faceDown) return false;
        if (wasHeld) return true;
        return !faceWasDown && HeadReachPolicy.Near(headDistance);
    }
}

// D-099: right-stick snap turn latch. One turn per push; re-arms only after the stick returns near center.
internal static class SnapTurnPolicy
{
    public const float PressAt = 0.7f;
    public const float ReleaseAt = 0.3f;

    // Returns the signed degrees to turn now (0 = none). Positive stick X = turn right (clockwise seen from above).
    public static float Next(bool armed, float stickX, float degrees, out bool armedAfter)
    {
        armedAfter = armed;
        if (float.IsNaN(stickX) || float.IsInfinity(stickX) || !(degrees > 0f)) return 0f;
        float a = MathF.Abs(stickX);
        if (armed)
        {
            if (a < PressAt) return 0f;
            armedAfter = false;
            return stickX > 0f ? degrees : -degrees;
        }
        if (a <= ReleaseAt) armedAfter = true;
        return 0f;
    }
}

// D-206 (BUG-204): the engineer's turret build mode as a pure state machine, so it can be tested without a headset.
// The game's own text names both actions (artifacts\_d204-loc-turret.txt, skill_desc_engineer_1): "Press
// {action=Special Ability} to enter build mode or switch blueprints. Press {action=Fire} to build the turret.", and
// four types exist (skill_desc_engineer_9/17/23 plus achievement_desc_kill_specific_monster_2, "have all 4 turrets
// active at the same time"). The grips are the two controllers' squeeze values. A single grip steps the blueprint ON
// RELEASE, and a grip that was ever down together with the other one is a chord that steps nothing - so "press L, then
// R" (any delay) is always the confirm and never a step plus a confirm, with no timing window to tune. The caller
// resets this every frame the game is not in turret mode, which is what keeps D-057 ("the grips are unbound") true for
// the rest of the game.
internal readonly struct TurretGripState
{
    public readonly bool Previous, Next, Confirm;

    public TurretGripState(bool previous, bool next, bool confirm) { Previous = previous; Next = next; Confirm = confirm; }
}

internal sealed class TurretGripPolicy
{
    public const float PressAt = 0.6f;
    public const float ReleaseAt = 0.35f;
    // One step is held this long at the game: a UI navigation event needs a frame or two to be seen, and 80 ms cannot
    // scroll past a second type.
    public const float StepSeconds = 0.08f;

    private bool leftDown, rightDown, leftInChord, rightInChord;
    private float previousUntil, nextUntil;

    public void Reset()
    {
        leftDown = rightDown = leftInChord = rightInChord = false;
        previousUntil = nextUntil = 0f;
    }

    // Hysteresis like the trigger and stick latches (press 0.6, release 0.35), so a half-squeezed grip cannot chatter.
    private static bool Latch(bool held, float squeeze) => held ? squeeze > ReleaseAt : squeeze >= PressAt;

    public TurretGripState Next(bool turretMode, float leftSqueeze, float rightSqueeze, float now)
    {
        if (!turretMode) { Reset(); return default; }
        bool left = Latch(leftDown, leftSqueeze), right = Latch(rightDown, rightSqueeze);
        if (left != leftDown)
        {
            leftDown = left;
            if (left) leftInChord = false; // a fresh press has not been part of a chord yet
            else { if (!leftInChord) previousUntil = now + StepSeconds; leftInChord = false; }
        }
        if (right != rightDown)
        {
            rightDown = right;
            if (right) rightInChord = false;
            else { if (!rightInChord) nextUntil = now + StepSeconds; rightInChord = false; }
        }
        bool confirm = leftDown && rightDown;
        if (confirm) leftInChord = rightInChord = true; // either release after this is part of the chord, not a step
        return new TurretGripState(now < previousUntil, now < nextUntil, confirm);
    }
}

internal static class HandAimMath
{
    // D-099: yaw the VR anchor about world-up through the tracked head so the head's world point does not move.
    // headLocal is the head position in the anchor's local (Unity-mapped) frame.
    public static void SnapAnchor(float[] anchorPosition, float[] anchorRotation, float[] headLocal, float degrees,
        out float[] newPosition, out float[] newRotation)
    {
        if (anchorPosition.Length != 3 || anchorRotation.Length != 4 || headLocal.Length != 3) throw new ArgumentException("snap dimensions");
        float h = degrees * MathF.PI / 360f;
        float[] yaw = { 0f, MathF.Sin(h), 0f, MathF.Cos(h) };
        newRotation = StereoMath.MultiplyQuaternion(yaw, anchorRotation);
        float[] before = StereoMath.Rotate(anchorRotation, headLocal);
        float[] after = StereoMath.Rotate(newRotation, headLocal);
        newPosition = new[] { anchorPosition[0] + before[0] - after[0], anchorPosition[1] + before[1] - after[1], anchorPosition[2] + before[2] - after[2] };
    }

    public static void ToWorld(float[] origin, float[] poseBaseRotation, HandSample hand,
        out float[] position, out float[] rotation)
    {
        if (origin.Length != 3 || poseBaseRotation.Length != 4) throw new ArgumentException("origin/anchor dimensions");
        float[] local = StereoMath.UnityPosition(hand.X, hand.Y, hand.Z);
        float[] rotated = StereoMath.Rotate(poseBaseRotation, local);
        position = new[] { origin[0] + rotated[0], origin[1] + rotated[1], origin[2] + rotated[2] };
        rotation = PoseRotation(poseBaseRotation, hand);
    }

    // D-202 (BUG-202): the tracked pose as a world rotation in the frame it is composed in. `poseBaseRotation` is
    // identity in the head/eye frame (D-195: the eye cameras stopped multiplying anchorRotation into the eye pose) and
    // the game camera rotation in the legacy anchor frame. ToWorld uses it for the aim ray; the rest capture must use
    // the SAME value, because a weapon captured against a different frame than the one it is driven in keeps a
    // rotation the view does not have - exactly the 90 deg hand/aim offset reported for 0.9.4, since the game camera
    // on this game is the player (its yaw is +-90 deg).
    public static float[] PoseRotation(float[] poseBaseRotation, HandSample hand)
    {
        if (poseBaseRotation.Length != 4) throw new ArgumentException("pose base dimensions");
        if (!hand.Active) throw new ArgumentException("inactive hand pose is never converted");
        return StereoMath.MultiplyQuaternion(poseBaseRotation, StereoMath.UnityOrientation(hand.Qx, hand.Qy, hand.Qz, hand.Qw));
    }

    // Unity forward (+Z) of a world rotation; the OpenXR aim pose forward (-Z) maps to it through UnityOrientation.
    public static float[] Forward(float[] rotation) => StereoMath.Rotate(rotation, new[] { 0f, 0f, 1f });

    // The container keeps its captured rest orientation relative to the aim frame.
    public static float[] DriveRotation(float[] handWorldRotation, float[] restLocalRotation) =>
        StereoMath.MultiplyQuaternion(handWorldRotation, restLocalRotation);

    // D-153 (TOOL-073): Unity up (+Y) of a world rotation. Forward() is the direction the weapon points; this is the
    // second axis the hand's full orientation needs, i.e. how the grip is rolled about that direction.
    public static float[] Up(float[] rotation) => StereoMath.Rotate(rotation, new[] { 0f, 1f, 0f });

    // Rotate a rotation about a world axis direction by degrees. The twist is multiplied on the left (world space), so
    // `axis` is the world barrel direction and the result keeps that direction fixed.
    public static float[] TwistAbout(float[] rotation, float[] axis, float degrees)
    {
        if (rotation.Length != 4 || axis.Length != 3) throw new ArgumentException("twist dimensions");
        float length = Length(axis);
        if (!(length > 1e-6f) || !Finite(degrees) || degrees == 0f) return rotation;
        float half = degrees * MathF.PI / 360f;
        float s = MathF.Sin(half) / length;
        return StereoMath.MultiplyQuaternion(new[] { axis[0] * s, axis[1] * s, axis[2] * s, MathF.Cos(half) }, rotation);
    }

    // Signed angle from fromUp to toUp measured about axis (the barrel), both projected perpendicular to it. NaN when
    // the axis or either projection is degenerate: the caller reports "not measurable" rather than a silent 0.
    public static float TwistDegrees(float[] fromUp, float[] toUp, float[] axis)
    {
        if (fromUp.Length != 3 || toUp.Length != 3 || axis.Length != 3) throw new ArgumentException("twist dimensions");
        float length = Length(axis);
        if (!(length > 1e-6f)) return float.NaN;
        float[] n = { axis[0] / length, axis[1] / length, axis[2] / length };
        float[] a = Reject(fromUp, n), b = Reject(toUp, n);
        float la = Length(a), lb = Length(b);
        if (!(la > 1e-6f) || !(lb > 1e-6f)) return float.NaN;
        float dot = Math.Clamp((a[0] * b[0] + a[1] * b[1] + a[2] * b[2]) / (la * lb), -1f, 1f);
        float cross = n[0] * (a[1] * b[2] - a[2] * b[1]) + n[1] * (a[2] * b[0] - a[0] * b[2]) + n[2] * (a[0] * b[1] - a[1] * b[0]);
        return MathF.Atan2(cross, dot) * 180f / MathF.PI;
    }

    private static float[] Reject(float[] v, float[] n)
    {
        float d = v[0] * n[0] + v[1] * n[1] + v[2] * n[2];
        return new[] { v[0] - d * n[0], v[1] - d * n[1], v[2] - d * n[2] };
    }

    public static float[] Conjugate(float[] q)
    {
        if (q.Length != 4) throw new ArgumentException("quaternion dimensions");
        return new[] { -q[0], -q[1], -q[2], q[3] };
    }

    // Rotation of child expressed in the parent frame (unit quaternions): parent^-1 * child.
    public static float[] Relative(float[] parentRotation, float[] childRotation) =>
        StereoMath.MultiplyQuaternion(Conjugate(parentRotation), childRotation);

    public static float AngleDegrees(float[] a, float[] b)
    {
        if (a.Length != 3 || b.Length != 3) throw new ArgumentException("vector dimensions");
        float la = Length(a), lb = Length(b);
        if (!(la > 1e-6f) || !(lb > 1e-6f)) throw new ArgumentException("zero-length direction");
        float dot = (a[0] * b[0] + a[1] * b[1] + a[2] * b[2]) / (la * lb);
        return MathF.Acos(Math.Clamp(dot, -1f, 1f)) * 180f / MathF.PI;
    }

    public static bool RestPlausible(float x, float y, float z, float maxDistance)
    {
        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(maxDistance) || maxDistance <= 0) return false;
        return x * x + y * y + z * z <= maxDistance * maxDistance;
    }

    private static float Length(float[] v) => MathF.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}













// D-181: REPLACED with the shipped D-171 class (byte-for-byte logic, taken from the shipped DLL decompile). The D-178 source recovery had
// regenerated an older revision whose ClampMaxDistance turned the shipped default 0 (= unlimited) into 5 m, which hid every potion/chest arrow.
internal static class WorldMarkerPolicy
{
    public const string Off = "off";

    public const string On = "on";

    public static readonly string[] PickupTypes = new string[14]
    {
        "XP", "GOLD", "RELICS", "XP_5", "LEVEL_UP", "HEALING", "XP_10", "XP_2", "SOUL_PURIFICATION", "BOSS_CHEST",
        "SCRAP", "DEMONIC_ENERGY", "POWER_UP", "APPLE_WITCH"
    };

    public const string ChestToken = "CHEST";

    public const string PoiToken = "POI";

    public const string DefaultKinds = "CHEST,HEALING,POWER_UP,BOSS_CHEST";

    public const string HangingPotionName = "Potion";

    public const string HangingPotionKind = "HEALING";

    public const float DefaultMaxDistance = 0f;

    public const float MinMaxDistance = 5f;

    public const float MaxMaxDistance = 200f;

    public const int DefaultMaxCount = 32;

    public const int MinMaxCount = 1;

    public const int MaxMaxCount = 64;

    public const float ArrowTipY = 0.5f;

    public const float ArrowTailY = -0.5f;

    public const float ArrowHeadBaseY = 0.1f;

    public const float ArrowShaftHalfWidth = 0.1f;

    public const float ArrowHeadHalfWidth = 0.3f;

    public static readonly float[] ArrowVertices = new float[21]
    {
        0f, 0.5f, 0f, -0.3f, 0.1f, 0f, 0.3f, 0.1f, 0f, -0.1f,
        0.1f, 0f, 0.1f, 0.1f, 0f, -0.1f, -0.5f, 0f, 0.1f, -0.5f,
        0f
    };

    public static readonly int[] ArrowTriangles = new int[9] { 0, 1, 2, 3, 4, 6, 3, 6, 5 };

    public const float ArrowAlpha = 0.5f;

    public const float RingArrowAlpha = 0.35f;

    public const float PositionDedupeMeters = 0.5f;

    public const float AngularSize = 0.05f;

    public const float MinSizeMeters = 0.08f;

    public const float MaxDrawDistance = 100f;

    public const float HeightOffset = 0.35f;

    public const int PriorityBossChest = 3;

    public const int PriorityChest = 2;

    public const int PriorityOther = 1;

    public const float VisorMaxAngleDegrees = 40f;

    public const float VisorDistance = 2f;

    public static bool IsValidMode(string? mode)
    {
        if (!(mode == "off"))
        {
            return mode == "on";
        }
        return true;
    }

    public static bool IsKnownKind(string token)
    {
        if (!(token == "CHEST") && !(token == "POI"))
        {
            return Array.IndexOf(PickupTypes, token) >= 0;
        }
        return true;
    }

    public static bool TryParseKinds(string? csv, out string[] kinds)
    {
        kinds = Array.Empty<string>();
        if (csv == null)
        {
            return false;
        }
        List<string> list = new List<string>();
        string[] array = csv.Split(',');
        for (int i = 0; i < array.Length; i++)
        {
            string text = array[i].Trim();
            if (text.Length != 0)
            {
                if (!IsKnownKind(text))
                {
                    return false;
                }
                if (!list.Contains(text))
                {
                    list.Add(text);
                }
            }
        }
        if (list.Count == 0)
        {
            return false;
        }
        kinds = list.ToArray();
        return true;
    }

    public static bool Wants(string[] kinds, string kind)
    {
        return Array.IndexOf(kinds, kind) >= 0;
    }

    public static string ChestKindFromTargetType(string? targetType)
    {
        if (!(targetType == "LEVEL_UP_CHEST"))
        {
            if (!(targetType == "MINI_BOSS_CHEST"))
            {
                return "";
            }
            return "BOSS_CHEST";
        }
        return "CHEST";
    }

    public static bool IsPotionKind(string? pickupType)
    {
        if (!(pickupType == "HEALING"))
        {
            return pickupType == "POWER_UP";
        }
        return true;
    }

    public static bool WantsPotion(string[] kinds, string? pickupType)
    {
        if (IsPotionKind(pickupType))
        {
            return Wants(kinds, pickupType!); // IsPotionKind is true only for the exact non-null names HEALING / POWER_UP
        }
        return false;
    }

    public static bool CanMarkPotion(bool exists, bool active, string[] kinds, string? pickupType)
    {
        if (exists & active)
        {
            return WantsPotion(kinds, pickupType);
        }
        return false;
    }

    public static bool IsHangingPotion(string? nodeName, bool activeInHierarchy, bool rendererEnabled)
    {
        if (activeInHierarchy & rendererEnabled)
        {
            return string.Equals(nodeName, "Potion", StringComparison.Ordinal);
        }
        return false;
    }

    public static float ClampMaxDistance(float metres)
    {
        if (!float.IsFinite(metres) || metres <= 0f)
        {
            return 0f;
        }
        return Math.Clamp(metres, 5f, 200f);
    }

    public static bool IsDistanceLimited(float maxDistance)
    {
        if (float.IsFinite(maxDistance))
        {
            return maxDistance > 0f;
        }
        return false;
    }

    public static bool WithinDistance(float distance, float maxDistance)
    {
        if (float.IsFinite(distance))
        {
            if (IsDistanceLimited(maxDistance))
            {
                return distance <= maxDistance;
            }
            return true;
        }
        return false;
    }

    public static int ClampMaxCount(int count)
    {
        if (count >= 1 && count <= 64)
        {
            return count;
        }
        return 32;
    }

    public static bool IsSameSpot(float ax, float ay, float az, float bx, float by, float bz)
    {
        float num = ax - bx;
        float num2 = ay - by;
        float num3 = az - bz;
        if (!float.IsFinite(num) || !float.IsFinite(num2) || !float.IsFinite(num3))
        {
            return true;
        }
        return num * num + num2 * num2 + num3 * num3 < 0.25f;
    }

    public static float SizeFor(float distance)
    {
        if (!float.IsFinite(distance) || distance < 0f)
        {
            return 0.08f;
        }
        return Math.Max(distance * 0.05f, 0.08f);
    }

    public static float DrawDistance(float distance)
    {
        if (float.IsFinite(distance) && !(distance < 0f))
        {
            return Math.Min(distance, 100f);
        }
        return 100f;
    }

    public static int Priority(string kind)
    {
        if (!(kind == "BOSS_CHEST"))
        {
            if (!(kind == "CHEST"))
            {
                return 1;
            }
            return 2;
        }
        return 3;
    }

    public static int Compare(bool aOffGaze, int aPriority, float aDistance, bool bOffGaze, int bPriority, float bDistance)
    {
        if (aOffGaze != bOffGaze)
        {
            if (!aOffGaze)
            {
                return -1;
            }
            return 1;
        }
        if (aPriority != bPriority)
        {
            return bPriority.CompareTo(aPriority);
        }
        return aDistance.CompareTo(bDistance);
    }

    public static float Length(float[] v)
    {
        return MathF.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
    }

    public static bool TryNormalize(float[] v, out float[] unit)
    {
        unit = new float[3] { 0f, 0f, 1f };
        if (v == null || v.Length != 3)
        {
            return false;
        }
        float num = Length(v);
        if (!float.IsFinite(num) || !(num > 1E-06f))
        {
            return false;
        }
        unit = new float[3]
        {
            v[0] / num,
            v[1] / num,
            v[2] / num
        };
        return true;
    }

    public static float AngleFromGaze(float[] localDirection)
    {
        if (!TryNormalize(localDirection, out float[] unit))
        {
            return 180f;
        }
        return MathF.Acos(Math.Clamp(unit[2], -1f, 1f)) * 180f / (float)Math.PI;
    }

    public static bool IsOffGaze(float[] localDirection)
    {
        return AngleFromGaze(localDirection) > 40f;
    }

    public static float[] ClampToVisor(float[] localDirection)
    {
        if (!TryNormalize(localDirection, out float[] unit))
        {
            return new float[3] { 0f, 0f, 1f };
        }
        float num = (float)Math.PI * 2f / 9f;
        if (MathF.Acos(Math.Clamp(unit[2], -1f, 1f)) <= num)
        {
            return unit;
        }
        if (!TryNormalize(new float[3]
        {
            unit[0],
            unit[1],
            0f
        }, out float[] unit2))
        {
            unit2 = new float[3] { 0f, 1f, 0f };
        }
        float num2 = MathF.Cos(num);
        float num3 = MathF.Sin(num);
        return new float[3]
        {
            unit2[0] * num3,
            unit2[1] * num3,
            num2
        };
    }
}
