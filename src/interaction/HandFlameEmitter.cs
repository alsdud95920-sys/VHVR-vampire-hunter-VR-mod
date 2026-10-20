using HarmonyLib;
using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-141 (TOOL-073): keep the native flame emitter pointing where the attack goes. Writes ONE rotation (the Flamethrower's
// vfx_flames_transform) per flamethrower; never calls or detours a weapon/effect method, never passes a Vector3 through a
// detour (D-097), never touches attack arguments, damage, particle lifetime or particle space. Only a PreCull postfix on
// the two eye cameras is patched (the same method the read-only render timing already patches).
// Busy native reads and renders that happen before this frame's tick re-apply the LAST WRITTEN hand pose to the already
// driven container, exactly like the existing HoldingLastPose state, so neither muzzle nor emitter falls back to the head pose.
internal sealed partial class HandAimDriver
{
    private FlameEmitterPinRuntime? emitterPin;
    private bool emitterPinRequested;
    private string? emitterPinError;
    private int lastCompletedTick = -1, handPoseReapplied;

    public void InstallFlameEmitterPin(bool enabled, Camera eyeLeft, Camera eyeRight)
    {
        emitterPinRequested = enabled;
        if (!enabled) return;
        try
        {
            emitterPin = new FlameEmitterPinRuntime(this, eyeLeft, eyeRight);
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(
                "[FLAME_EMITTER_PIN] installed: emitter rotation follows the attack aim after Tick, on busy frames and before eye cull; NOT_VISUALLY_ACCEPTED");
        }
        catch (Exception e)
        {
            emitterPinError = e.GetType().Name + ": " + e.Message;
            try { emitterPin?.Dispose(); } catch (Exception) { }
            emitterPin = null;
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogError("[FLAME_EMITTER_PIN] INSTALL_FAILED " + emitterPinError);
        }
    }

    private void PinFlameEmitters(FlameEmitterStage stage) => emitterPin?.Pin(stage);

    // Busy native read or a render before this frame's tick: hold the last written hand pose instead of the game's pose.
    private void ReapplyLastHandPose()
    {
        if (emitterPin == null || ShotScopeDepth != 0) return;
        foreach (var slot in new[] { left, right })
        {
            if (slot.Container == null || !slot.HasWritten ||
                !FlameEmitterPinPolicy.HandOwnsMount(slot.State == HandAimState.Driving, slot.State == HandAimState.HoldingLastPose)) continue;
            slot.Container.SetPositionAndRotation(slot.LastWrittenPosition, slot.LastWrittenRotation);
            handPoseReapplied++;
        }
    }

    private void ReleaseFlameEmitterPin()
    {
        try { emitterPin?.Dispose(); }
        catch (Exception e) { emitterPinError = e.GetType().Name + ": " + e.Message; }
        emitterPin = null;
    }

    private object FlameEmitterPinReceipt() => new
    {
        enabled = emitterPinRequested, active = emitterPin != null && !emitterPin.Disposed,
        error = emitterPinError ?? emitterPin?.Stats.LastError, stats = emitterPin?.Stats.Receipt(), handPoseReapplied, lastCompletedTick, beforeParticlesCalls,
        scope = "Flamethrower.vfx_flames_transform rotation only (other vfx_* transforms not pinned)",
        acceptance = "NOT_GAME_OR_HEADSET_VERIFIED",
    };

    private sealed class FlameEmitterPinRuntime : IDisposable
    {
        private sealed class Binding
        {
            public HandSlot Slot = null!;
            public Transform Emitter = null!, Muzzle = null!;
            public Quaternion RestLocalRotation;
            public bool Wrote;
        }

        private static FlameEmitterPinRuntime? active;
        private readonly HandAimDriver driver;
        private readonly Camera eyeLeft, eyeRight;
        private readonly Harmony harmony = new("vhvr.flame-emitter-pin.d141");
        private readonly Dictionary<int, Binding> known = new();
        private Binding[] bindings = Array.Empty<Binding>();
        private DateTime nextScan;
        public readonly FlameEmitterPinStats Stats = new();
        public bool Disposed { get; private set; }

        public FlameEmitterPinRuntime(HandAimDriver driver, Camera eyeLeft, Camera eyeRight)
        {
            this.driver = driver; this.eyeLeft = eyeLeft; this.eyeRight = eyeRight;
            if (active != null) throw new InvalidOperationException("flame emitter pin already installed");
            var cull = AccessTools.Method(typeof(Camera), "FireOnPreCull") ?? throw new InvalidOperationException("Camera.FireOnPreCull missing");
            try
            {
                harmony.Patch(cull, postfix: new HarmonyMethod(typeof(FlameEmitterPinRuntime), nameof(BeforeCull)) { priority = Priority.First });
            }
            catch { try { harmony.UnpatchSelf(); } catch (Exception) { } throw; }
            active = this;
        }

        private static void BeforeCull(Camera __0)
        {
            var self = active;
            if (self == null || __0 == null || (__0 != self.eyeLeft && __0 != self.eyeRight)) return;
            self.Pin(FlameEmitterStage.PreCull);
        }

        public void Pin(FlameEmitterStage stage)
        {
            if (Disposed) return;
            try
            {
                if (stage != FlameEmitterStage.PreCull && DateTime.UtcNow >= nextScan)
                {
                    nextScan = DateTime.UtcNow.AddSeconds(FlameEmitterPinPolicy.RescanSeconds);
                    Scan();
                }
                if (stage == FlameEmitterStage.PreCull && driver.lastCompletedTick != Time.frameCount) driver.ReapplyLastHandPose();
                var list = bindings;
                for (int i = 0; i < list.Length; i++) Apply(list[i], stage);
            }
            catch (Exception e)
            {
                Stats.Errors++; Stats.LastError = e.GetType().Name + ": " + e.Message;
                if (Stats.Errors >= FlameEmitterPinPolicy.MaxErrorsBeforeDisable)
                {
                    VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogError("[FLAME_EMITTER_PIN] DISABLED after repeated errors: " + Stats.LastError);
                    try { Dispose(); } catch (Exception) { }
                }
            }
        }

        private void Scan()
        {
            var next = new List<Binding>();
            var seen = new HashSet<int>();
            int parented = 0, flameEmitters = 0, acidEmitters = 0, skippedUnsafe = 0;
            foreach (var slot in new[] { driver.left, driver.right })
            {
                var container = slot.Container;
                if (container == null) continue;
                foreach (var weapon in container.GetComponentsInChildren<Weapon>(false))
                {
                    if (weapon == null) continue;
                    var muzzle = weapon.muzzle_flash_spawn;
                    if (muzzle == null) continue;
                    Transform? emitter = null;
                    // D-143: the same native pattern on both effects - Flamethrower.vfx_flames_transform, and the
                    // AcidFlower's particle_systems parent. Recovered 2026-10-14 (D-178) to match the shipped D-171 build.
                    var flame = weapon.TryCast<Flamethrower>();
                    if (flame != null)
                    {
                        emitter = flame.vfx_flames_transform;
                        if (emitter == null) emitter = flame.vfx_flames?.transform;
                        if (emitter != null) flameEmitters++;
                    }
                    else
                    {
                        var acid = weapon.TryCast<AcidFlower>();
                        if (acid != null)
                        {
                            emitter = acid.particle_systems?.transform.parent;
                            if (emitter != null) acidEmitters++;
                        }
                    }
                    if (emitter == null) continue;
                    // Never rotate the muzzle or one of its ancestors, and never a node outside the hand mount.
                    if (!FlameEmitterPinPolicy.CanPinNode(muzzle.IsChildOf(emitter), emitter.IsChildOf(container)))
                    {
                        skippedUnsafe++;
                        continue;
                    }
                    int id = emitter.GetInstanceID();
                    if (!known.TryGetValue(id, out var binding) || binding.Muzzle != muzzle)
                    {
                        binding = new Binding { Emitter = emitter, Muzzle = muzzle, RestLocalRotation = emitter.localRotation };
                        known[id] = binding;
                    }
                    binding.Slot = slot;
                    if (!seen.Add(id)) continue;
                    if (emitter.parent == muzzle) parented++;
                    next.Add(binding);
                }
            }
            foreach (int id in known.Keys.Where(id => !seen.Contains(id)).ToArray()) known.Remove(id);
            bindings = next.ToArray();
            Stats.Scans++; Stats.Emitters = next.Count; Stats.ParentedToMuzzle = parented;
            Stats.FlameEmitters = flameEmitters; Stats.AcidEmitters = acidEmitters; Stats.SkippedUnsafe = skippedUnsafe;
        }

        private void Apply(Binding binding, FlameEmitterStage stage)
        {
            if (binding.Emitter == null || binding.Muzzle == null) { Stats.Dead++; return; }
            var slot = binding.Slot;
            bool driving = slot.State == HandAimState.Driving;
            if (!FlameEmitterPinPolicy.HandOwnsMount(driving, slot.State == HandAimState.HoldingLastPose)) return; // hand not owning the mount: stay native
            var aim = slot.AimForward;
            bool useAim = FlameEmitterPinPolicy.UseAttackAim(driving, slot.SampleActive, slot.HasAimRay, aim.x, aim.y, aim.z);
            var muzzle = binding.Muzzle;
            var forward = useAim ? aim : muzzle.forward;
            if (!FlameEmitterPinPolicy.UsableDirection(forward.x, forward.y, forward.z)) return;
            if (useAim) Stats.AttackAimUsed++; else Stats.MuzzleUsed++;
            float error = Vector3.Angle(binding.Emitter.forward, forward);
            bool write = FlameEmitterPinPolicy.ShouldWrite(error);
            Stats.Record(stage, error, write);
            if (!write) return;
            binding.Emitter.rotation = Quaternion.LookRotation(forward, muzzle.up); // same effect as native set_forward(world) but after the parent moved
            binding.Wrote = true;
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (active == this) active = null;
            var errors = new List<Exception>();
            try { harmony.UnpatchSelf(); } catch (Exception e) { errors.Add(e); }
            foreach (var binding in known.Values)
            {
                if (!binding.Wrote || binding.Emitter == null) continue;
                try { binding.Emitter.localRotation = binding.RestLocalRotation; }
                catch (Exception e) { errors.Add(e); }
            }
            known.Clear(); bindings = Array.Empty<Binding>();
            if (errors.Count != 0) throw new AggregateException("flame emitter pin release failed", errors);
        }
    }
}