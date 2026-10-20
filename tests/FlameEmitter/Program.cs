using System.Numerics;
using VampireHuntersVR.Interaction;
using static Geo;

int passed = 0;
void Check(string name, bool ok) { if (!ok) throw new InvalidOperationException(name); Console.WriteLine("PASS " + name); passed++; }

// ---- policy ---------------------------------------------------------------------------------------------------
Check("write-below-threshold-skipped", !FlameEmitterPinPolicy.ShouldWrite(0.01f));
Check("write-above-threshold", FlameEmitterPinPolicy.ShouldWrite(0.5f));
Check("write-nonfinite-error-rewrites", FlameEmitterPinPolicy.ShouldWrite(float.NaN) && FlameEmitterPinPolicy.ShouldWrite(float.PositiveInfinity));
Check("hand-owns-mount-only-when-driving-or-holding", FlameEmitterPinPolicy.HandOwnsMount(true, false) && FlameEmitterPinPolicy.HandOwnsMount(false, true) && !FlameEmitterPinPolicy.HandOwnsMount(false, false));
Check("usable-direction-rejects-zero-nan-inf", !FlameEmitterPinPolicy.UsableDirection(0, 0, 0) && !FlameEmitterPinPolicy.UsableDirection(float.NaN, 0, 1) && !FlameEmitterPinPolicy.UsableDirection(float.PositiveInfinity, 0, 0) && FlameEmitterPinPolicy.UsableDirection(0, 0, 1));
Check("attack-aim-used-only-with-valid-live-ray", FlameEmitterPinPolicy.UseAttackAim(true, true, true, 0, 0, 1) && !FlameEmitterPinPolicy.UseAttackAim(false, true, true, 0, 0, 1) &&
    !FlameEmitterPinPolicy.UseAttackAim(true, false, true, 0, 0, 1) && !FlameEmitterPinPolicy.UseAttackAim(true, true, false, 0, 0, 1) && !FlameEmitterPinPolicy.UseAttackAim(true, true, true, 0, 0, 0));
Check("pre-cull-threshold-tighter-than-tick", FlameEmitterPinPolicy.ThresholdFor(FlameEmitterStage.PreCull) < FlameEmitterPinPolicy.ThresholdFor(FlameEmitterStage.Tick));
Check("busy-threshold-equals-tick", FlameEmitterPinPolicy.ThresholdFor(FlameEmitterStage.Busy) == FlameEmitterPinPolicy.ThresholdFor(FlameEmitterStage.Tick));
int budget = FlameEmitterPinPolicy.MaxErrorsBeforeDisable; double rescan = FlameEmitterPinPolicy.RescanSeconds;
Check("error-budget-bounded", budget > 0 && budget <= 100 && rescan > 0);

// ---- statistics -----------------------------------------------------------------------------------------------
var stats = new FlameEmitterPinStats();
stats.Record(FlameEmitterStage.Tick, 10f, true);
stats.Record(FlameEmitterStage.Tick, 1f, false);
stats.Record(FlameEmitterStage.PreCull, 3f, true);
stats.Record(FlameEmitterStage.PreCull, float.NaN, true);
Check("stats-tick-counted", stats.Samples(FlameEmitterStage.Tick) == 2 && stats.Writes(FlameEmitterStage.Tick) == 1 && stats.Over(FlameEmitterStage.Tick) == 1);
Check("stats-precull-uses-tight-threshold", stats.Over(FlameEmitterStage.PreCull) == 2);
Check("stats-nonfinite-error-counted-as-180", stats.MaxError(FlameEmitterStage.PreCull) == 180f);
Check("stats-stages-independent", stats.Samples(FlameEmitterStage.Busy) == 0 && stats.Over(FlameEmitterStage.Busy) == 0);
stats.Scans = 3; stats.Emitters = 1; stats.ParentedToMuzzle = 1; stats.Dead = 2; stats.Errors = 4; stats.AttackAimUsed = 5; stats.MuzzleUsed = 6; stats.LastError = "x";
string receipt = System.Text.Json.JsonSerializer.Serialize(stats.Receipt());
Check("stats-receipt-serialisable", receipt.Contains("\"preCull\"") && receipt.Contains("\"attackAimUsed\":5") && receipt.Contains("\"muzzleUsed\":6") && receipt.Contains("\"parentedToMuzzle\":1") && receipt.Contains("\"dead\":2") && receipt.Contains("\"errors\":4") && receipt.Contains("\"scans\":3") && receipt.Contains("\"emitters\":1") && receipt.Contains("\"lastError\":\"x\""));

// ---- fake hierarchy: reproduces the D-140 set_forward scenario --------------------------------------------------
// Emitter is a CHILD of the muzzle. World forward of the emitter = parent rotation * local offset * +Z.
// Native Update runs while the muzzle follows the head; the hand correction rotates the parent afterwards.
var rig = new Rig();
Quaternion head = Yaw(30), hand = Yaw(-20) * Pitch(10);
rig.Parent = head;
Vector3 aimWorld = Vector3.Transform(Vector3.UnitZ, hand);   // attack direction (end-start) = hand aim
rig.SetWorldForward(aimWorld);                               // native draw_shoot_vfx: set_forward(WORLD vector) while the muzzle still follows the head
Check("native-write-points-along-aim-at-write-time", Angle(rig.EmitterForward, aimWorld) < 0.01f);
var muzzleBranch = new Rig { Parent = head }; muzzleBranch.SetWorldForward(muzzleBranch.MuzzleForward); muzzleBranch.Parent = hand;
Check("muzzle-forward-branch-follows-parent-by-itself", Angle(muzzleBranch.EmitterForward, muzzleBranch.MuzzleForward) < 0.01f);
rig.Parent = hand;                                           // hand correction rotates the parent afterwards
float unpinned = Angle(rig.EmitterForward, aimWorld);
Check("without-pin-emitter-keeps-head-minus-hand-offset", unpinned > 40f);
Vector3 expected = Vector3.Transform(aimWorld, Quaternion.Concatenate(Quaternion.Inverse(head), hand));
Check("without-pin-final-direction-is-hand-times-inverse-head-times-aim", Angle(rig.EmitterForward, expected) < 0.05f);
Vector3 aim = aimWorld;
rig.SetWorldForward(aim);                                    // the pin
Check("pin-aligns-emitter-with-attack-aim", Angle(rig.EmitterForward, aim) < 0.01f);
Check("pin-is-idempotent", FlameEmitterPinPolicy.ShouldWrite(Angle(rig.EmitterForward, aim)) == false);

// Error direction: head turned right makes the unpinned emitter swing the OPPOSITE way (D-140 finding 4).
foreach (float headYaw in new[] { 15f, 30f, 60f })
{
    var r = new Rig { Parent = Yaw(headYaw) }; r.SetWorldForward(Vector3.UnitZ); r.Parent = Quaternion.Identity;   // aim straight ahead, hand = identity
    float emitterYaw = MathF.Atan2(r.EmitterForward.X, r.EmitterForward.Z) * 180f / MathF.PI;
    Check($"unpinned-swings-opposite-to-head-yaw-{headYaw}", emitterYaw * headYaw < 0f && MathF.Abs(MathF.Abs(emitterYaw) - headYaw) < 0.1f);
    r.SetWorldForward(Vector3.UnitZ);
    Check($"pinned-no-swing-yaw-{headYaw}", Angle(r.EmitterForward, Vector3.UnitZ) < 0.01f);
}

// Frame simulation: 2,000 frames, random head and hand, 33% skipped/busy hand ticks, 775/8128-like native muzzle flips.
var rng = new Random(140);
int tickBad = 0, preCullBad = 0, unpinnedBad = 0, frames = 2000;
float maxPre = 0f, maxTick = 0f;
Quaternion lastHand = Quaternion.Identity; Vector3 lastAim = Vector3.UnitZ;
var sim = new Rig(); var naive = new Rig();
var simStats = new FlameEmitterPinStats();
for (int f = 0; f < frames; f++)
{
    var headNow = Yaw((float)(rng.NextDouble() * 120 - 60)) * Pitch((float)(rng.NextDouble() * 30 - 15));
    bool busy = rng.NextDouble() < 0.33;
    if (!busy) { lastHand = Yaw((float)(rng.NextDouble() * 80 - 40)) * Pitch((float)(rng.NextDouble() * 40 - 20)); lastAim = Vector3.Transform(Vector3.UnitZ, lastHand); }
    // native Update: muzzle follows the head, then draw_shoot_vfx sets the emitter from that pose
    sim.Parent = headNow; sim.SetWorldForward(lastAim);
    naive.Parent = headNow; naive.SetWorldForward(lastAim);
    // our tick (or the busy path): parent is the hand pose; the emitter pin runs afterwards
    sim.Parent = lastHand; naive.Parent = lastHand;
    float err = Angle(sim.EmitterForward, lastAim);
    bool write = FlameEmitterPinPolicy.ShouldWrite(err);
    simStats.Record(busy ? FlameEmitterStage.Busy : FlameEmitterStage.Tick, err, write);
    if (write) sim.SetWorldForward(lastAim);
    maxTick = MathF.Max(maxTick, Angle(sim.EmitterForward, lastAim));
    if (Angle(sim.EmitterForward, lastAim) > 0.5f) tickBad++;
    if (Angle(naive.EmitterForward, lastAim) > FlameEmitterPinPolicy.SignificantErrorDegrees) unpinnedBad++;
    // pre-cull: nothing moved the parent, so the pin must have nothing left to fix
    float residual = Angle(sim.EmitterForward, lastAim);
    simStats.Record(FlameEmitterStage.PreCull, residual, FlameEmitterPinPolicy.ShouldWrite(residual));
    maxPre = MathF.Max(maxPre, residual);
    if (residual > FlameEmitterPinPolicy.ResidualErrorDegrees) preCullBad++;
}
Check("simulation-unpinned-emitter-often-wrong", unpinnedBad > frames / 2);
Check("simulation-pinned-emitter-matches-aim-every-frame", tickBad == 0 && maxTick < 0.5f);
Check("simulation-busy-frames-also-pinned", simStats.Samples(FlameEmitterStage.Busy) > frames / 5 && simStats.Samples(FlameEmitterStage.Busy) < frames / 2);
Check("simulation-precull-residual-below-two-degrees-p100", preCullBad == 0 && maxPre < FlameEmitterPinPolicy.ResidualErrorDegrees);
Check("simulation-stats-precull-no-overthreshold", simStats.Over(FlameEmitterStage.PreCull) == 0);
Check("simulation-stats-tick-saw-large-native-error", simStats.MaxError(FlameEmitterStage.Tick) > 20f);

// Parent moved AFTER the Tick pin (for example a gaze write): the pre-cull pin repairs it.
var late = new Rig { Parent = hand }; late.SetWorldForward(Vector3.Transform(Vector3.UnitZ, hand));
late.Parent = Yaw(70) * hand;
float lateErr = Angle(late.EmitterForward, aim);
Check("late-parent-move-breaks-tick-pin", lateErr > FlameEmitterPinPolicy.ResidualErrorDegrees);
if (FlameEmitterPinPolicy.ShouldWrite(lateErr)) late.SetWorldForward(aim);
Check("pre-cull-pin-repairs-late-parent-move", Angle(late.EmitterForward, aim) < 0.01f);

// ---- source guards (production files are Unity-bound and cannot be compiled here) -----------------------------------
string root = AppContext.BaseDirectory;
while (root.Length > 3 && !File.Exists(Path.Combine(root, "AGENTS.md"))) root = Path.GetDirectoryName(root)!;
string stage0 = File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHost.cs"));
string hostConfig = File.ReadAllText(Path.Combine(root, "src", "core", "OpenXrHostConfig.cs"));
string emitter = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandFlameEmitter.cs"));
string driver = File.ReadAllText(Path.Combine(root, "src", "interaction", "HandAimDriver.cs"));
string weaponRuntime = File.ReadAllText(Path.Combine(root, "src", "interaction", "WeaponRuntimeBridge.cs"));
string csproj = File.ReadAllText(Path.Combine(root, "src", "core", "VampireHuntersVR.Stage0.csproj"));
string schema = File.ReadAllText(Path.Combine(root, "config", "schema.json"));
string nonComment = string.Join('\n', emitter.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));
Check("source-patches-only-camera-precull", emitter.Split("harmony.Patch(").Length == 2 && emitter.Contains("\"FireOnPreCull\"") && emitter.Contains("postfix:"));
Check("source-no-weapon-or-vfx-detour", !nonComment.Contains("draw_shoot_vfx") && !nonComment.Contains("shoot_forward") && !nonComment.Contains("PrefixTracer") && !nonComment.Contains("prefix:"));
Check("source-no-vector3-argument-in-patched-method", emitter.Contains("BeforeCull(Camera __0)") && !emitter.Contains("Vector3 __"));
Check("source-only-emitter-rotation-written", emitter.Split("Emitter.rotation =").Length == 2 && emitter.Split("Emitter.localRotation =").Length == 2 && !emitter.Contains("Emitter.position") && !emitter.Contains("Emitter.localPosition") && !emitter.Contains("SetPositionAndRotation(binding"));
Check("source-no-damage-ammo-or-particle-writes", !nonComment.Contains("already_firing") && !nonComment.Contains(".radius_size") && !nonComment.Contains("projectile") && !nonComment.Contains("startLifetime") && !nonComment.Contains("simulationSpace") && !nonComment.Contains("Play(") && !nonComment.Contains("Stop("));
Check("source-direction-is-hand-attack-aim", emitter.Contains("slot.AimForward") && emitter.Contains("UseAttackAim(") && emitter.Contains("LookRotation(forward, muzzle.up)"));
Check("source-before-particle-pin-hooked-after-player-update", weaponRuntime.Contains("d.HoldHandsBeforeParticles(__instance);") &&
    weaponRuntime.IndexOf("d.ApplyAutonomousGaze(d.runtimeSource);", weaponRuntime.IndexOf("private static void AfterPlayerUpdate")) < weaponRuntime.IndexOf("d.HoldHandsBeforeParticles(__instance);") &&
    File.ReadAllText(Path.Combine(root, "src", "interaction", "HandPose.cs")).Contains("beforeParticlesCalls++;") && emitter.Contains("beforeParticlesCalls,"));
Check("source-gated-to-hand-owned-mount", emitter.Contains("HandOwnsMount(driving, slot.State == HandAimState.HoldingLastPose)) return;"));
Check("source-reapply-guarded-by-shot-scope", emitter.Contains("ShotScopeDepth != 0") && emitter.Contains("slot.Container.SetPositionAndRotation(slot.LastWrittenPosition, slot.LastWrittenRotation)"));
Check("source-restores-and-unpatches-on-dispose", emitter.Contains("harmony.UnpatchSelf()") && emitter.Contains("binding.Emitter.localRotation = binding.RestLocalRotation") && emitter.Contains("Disposed = true"));
Check("source-error-budget-disables", emitter.Contains("MaxErrorsBeforeDisable") && emitter.Contains("DISABLED after repeated errors"));
Check("source-install-failure-does-not-stop-hand-aim", emitter.Contains("INSTALL_FAILED") && emitter.Contains("emitterPin = null;"));
int tickAt = driver.IndexOf("PinFlameEmitters(FlameEmitterStage.Tick)"), fireAt = driver.IndexOf("FireTick(player, source)"), doneAt = driver.IndexOf("lastCompletedTick = frame;"), appliedAt = driver.IndexOf("ObserveHandApplied();");
// D-178 recovery restored the timing-only ObserveHandApplied() call (RenderTiming "applied-only-after-fire-tick" pins it).
// It writes no transform, so the invariant is only that the emitter pin stays the LAST thing in the tick: after it, never before.
Check("source-tick-pin-after-all-parent-writers", fireAt > 0 && doneAt > fireAt && tickAt > doneAt && (appliedAt < 0 || (appliedAt > fireAt && appliedAt < tickAt)));
Check("source-before-particle-pin-hooks-after-player-update", weaponRuntime.Contains("d.HoldHandsBeforeParticles(__instance);") &&
    weaponRuntime.IndexOf("d.HoldHandsBeforeParticles(__instance);") > weaponRuntime.IndexOf("private static void AfterPlayerUpdate"));
Check("source-busy-and-rejected-reads-pin", driver.Contains("if (read == 6) { ReapplyLastHandPose(); PinFlameEmitters(FlameEmitterStage.Busy); return; }") && driver.Contains("Fail(\"ReadHands rejected\"); ReapplyLastHandPose(); PinFlameEmitters(FlameEmitterStage.Busy); return;"));
Check("source-release-restores-emitter-first", driver.IndexOf("ReleaseFlameEmitterPin();") is > 0 && driver.IndexOf("ReleaseFlameEmitterPin();") < driver.IndexOf("ReleaseFlameEndpoint();"));
Check("source-receipt-published", driver.Contains("flameEmitterPin = FlameEmitterPinReceipt(),"));
Check("source-host-flag-default-on-and-installed-after-hand-driver", hostConfig.Contains("public bool FlameEmitterPin { get; set; } = true;") && stage0.Contains("handAim.InstallFlameEmitterPin(Config.FlameEmitterPin, eyes[0], eyes[1]);") &&
    stage0.IndexOf("handAim.InstallFlameEmitterPin(") > stage0.IndexOf("handAim.InstallFlameEndpoint("));
Check("source-d139-endpoint-patch-kept-default-on", hostConfig.Contains("public bool FlameEndpointVfx { get; set; } = true;") && stage0.Contains("handAim.InstallFlameEndpoint(Config.FlameEndpointVfx);"));
Check("source-csproj-compiles-both-files", csproj.Contains("FlameEmitterPin.cs") && csproj.Contains("HandFlameEmitter.cs"));
Check("source-schema-documents-flag", schema.Contains("\"FlameEmitterPin\""));
Console.WriteLine($"RESULT {passed} flame emitter fixtures; fake transforms/policy/source guards, NOT_UNITY_RUNTIME_NOT_HEADSET_ACCEPTANCE");

sealed class Rig
{
    public Quaternion Parent = Quaternion.Identity;   // muzzle world rotation
    public Quaternion Local = Quaternion.Identity;    // emitter local rotation relative to the muzzle
    public Vector3 EmitterForward => Vector3.Transform(Vector3.Transform(Vector3.UnitZ, Local), Parent);
    public Vector3 MuzzleForward => Vector3.Transform(Vector3.UnitZ, Parent);
    // Transform.set_forward(world): keep the parent, change only the local offset.
    public void SetWorldForward(Vector3 world) => Local = FromTo(Vector3.UnitZ, Vector3.Transform(world, Quaternion.Inverse(Parent)));
}

static class Geo
{
    public static Quaternion Yaw(float deg) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, deg * MathF.PI / 180f);
    public static Quaternion Pitch(float deg) => Quaternion.CreateFromAxisAngle(Vector3.UnitX, deg * MathF.PI / 180f);
    public static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        a = Vector3.Normalize(a); b = Vector3.Normalize(b);
        float d = Vector3.Dot(a, b);
        if (d > 0.999999f) return Quaternion.Identity;
        if (d < -0.999999f) return Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        var axis = Vector3.Normalize(Vector3.Cross(a, b));
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(Math.Clamp(d, -1f, 1f)));
    }
    public static float Angle(Vector3 a, Vector3 b) => MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)), -1f, 1f)) * 180f / MathF.PI;
}
