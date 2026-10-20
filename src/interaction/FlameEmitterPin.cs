namespace VampireHuntersVR.Interaction;

// D-141: engine-independent policy/statistics for re-pinning the native flame emitter to the attack direction.
// Root cause (D-140): Flamethrower.draw_shoot_vfx writes a WORLD forward on vfx_flames_transform once, while the
// the parent muzzle is still in the head-following pose; the hand correction then rotates the parent and the child
// inherits (hand - nativeMuzzle). The pin rewrites the child's ROTATION after every parent change (position follows
// the parent by itself), so the result never depends on that timing.
// D-143 (d140a receipts): particles are EMITTED between PlayerController.Update and our LateUpdate (975/975 count changes,
// 0 inside LateUpdate), i.e. before the D-141 Tick pin. BeforeParticles holds the hand pose + pins right after the
// player update, so emission already sees the hand pose. AcidFlower uses the same native pattern (parent.set_forward).
internal enum FlameEmitterStage { Tick = 0, Busy = 1, PreCull = 2, BeforeParticles = 3 }

internal static class FlameEmitterPinPolicy
{
    public const float WriteThresholdDegrees = 0.05f;
    public const float SignificantErrorDegrees = 5f;   // Tick/Busy: how wrong the native write left the emitter
    public const float ResidualErrorDegrees = 2f;      // PreCull: pin was ineffective if the emitter drifted again before render
    public const int MaxErrorsBeforeDisable = 20;
    public const double RescanSeconds = 1.0;

    // Non-finite error (degenerate vectors) must still rewrite rather than leave a bad emitter.
    public static bool ShouldWrite(float errorDegrees) => !float.IsFinite(errorDegrees) || errorDegrees > WriteThresholdDegrees;

    // The hand owns the mount only while the driver writes it (or holds its last write); otherwise native aiming stays untouched.
    public static bool HandOwnsMount(bool driving, bool holdingLastPose) => driving || holdingLastPose;

    public static bool UsableDirection(float x, float y, float z)
    {
        float sq = x * x + y * y + z * z;
        return float.IsFinite(sq) && sq > 0.5f;
    }

    // Same direction the attack is given (the per-weapon hand aim frame) while the hand ray is valid; otherwise the held muzzle.
    public static bool UseAttackAim(bool driving, bool sampleActive, bool hasAimRay, float x, float y, float z) =>
        driving && sampleActive && hasAimRay && UsableDirection(x, y, z);

    public static float ThresholdFor(FlameEmitterStage stage) =>
        stage == FlameEmitterStage.PreCull ? ResidualErrorDegrees : SignificantErrorDegrees;

    // A node may be re-aimed only if it lives inside the hand-driven mount and is NOT the muzzle or one of its ancestors
    // (rotating an ancestor would rotate the attack muzzle itself).
    public static bool CanPinNode(bool nodeIsMuzzleOrAncestor, bool nodeInsideMount) => !nodeIsMuzzleOrAncestor && nodeInsideMount;

    // Hold the last hand pose while the player body moved this frame: keep the hand where the body carried it.
    public static (float X, float Y, float Z) HoldPosition(float wx, float wy, float wz, float dx, float dy, float dz, bool hasDelta) =>
        hasDelta && float.IsFinite(dx) && float.IsFinite(dy) && float.IsFinite(dz) ? (wx + dx, wy + dy, wz + dz) : (wx, wy, wz);
}

internal sealed class FlameEmitterPinStats
{
    private readonly int[] samples = new int[4], writes = new int[4], over = new int[4];
    private readonly float[] maxError = new float[4];
    public int Scans, Emitters, ParentedToMuzzle, Dead, Errors, AttackAimUsed, MuzzleUsed, FlameEmitters, AcidEmitters, SkippedUnsafe;
    public string? LastError;

    public void Record(FlameEmitterStage stage, float errorDegrees, bool wrote)
    {
        int i = (int)stage;
        samples[i]++;
        if (wrote) writes[i]++;
        float error = float.IsFinite(errorDegrees) ? errorDegrees : 180f;
        if (error > maxError[i]) maxError[i] = error;
        if (error > FlameEmitterPinPolicy.ThresholdFor(stage)) over[i]++;
    }

    public int Samples(FlameEmitterStage stage) => samples[(int)stage];
    public int Writes(FlameEmitterStage stage) => writes[(int)stage];
    public int Over(FlameEmitterStage stage) => over[(int)stage];
    public float MaxError(FlameEmitterStage stage) => maxError[(int)stage];

    private object Stage(FlameEmitterStage stage) => new
    {
        samples = Samples(stage), writes = Writes(stage), overThreshold = Over(stage),
        thresholdDegrees = FlameEmitterPinPolicy.ThresholdFor(stage), maxErrorDegrees = MaxError(stage),
    };

    public object Receipt() => new
    {
        scans = Scans, emitters = Emitters, parentedToMuzzle = ParentedToMuzzle, dead = Dead, errors = Errors, lastError = LastError,
        attackAimUsed = AttackAimUsed, muzzleUsed = MuzzleUsed, flameEmitters = FlameEmitters, acidEmitters = AcidEmitters, skippedUnsafe = SkippedUnsafe,
        tick = Stage(FlameEmitterStage.Tick), busy = Stage(FlameEmitterStage.Busy), preCull = Stage(FlameEmitterStage.PreCull),
        beforeParticles = Stage(FlameEmitterStage.BeforeParticles),
        note = "errors are emitter-forward vs target-forward measured BEFORE each write; beforeParticles runs after PlayerController.Update " +
               "and before particle emission (D-143). preCull never fired in d140a (TOOL-074). Particle simulation space is still unmeasured.",
    };
}