using HarmonyLib;
using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-111: the game's Weapon.update owns cadence. No manual update/RPM cap/ammo/damage writes.
internal sealed partial class HandAimDriver
{
    public bool StockFireCadence, AutonomousHeadAim;
    private Harmony? weaponRuntimeHarmony;
    private static HandAimDriver? runtimeDriver;
    private Camera? runtimeSource;
    private PlayerController? runtimePlayer;
    private Vector3 sourceRestPosition;
    private Quaternion sourceRestRotation;
    private bool sourceRestCaptured;
    public int GazeWrites, NativeWeaponUpdates, HeldWeaponUpdates;
    public string? WeaponRuntimeError;

    public void InstallWeaponRuntime(Camera source, PlayerController player)
    {
        runtimeSource = source; runtimePlayer = player;
        if (StockFireCadence && player._playerInputController == null)
            throw new InvalidOperationException("StockFireCadence requires the player's native input controller");
        var h = new Harmony("vhvr.weaponruntime");
        try
        {
            if (StockFireCadence)
                h.Patch(AccessTools.Method(typeof(Weapon), "update"),
                    prefix: new HarmonyMethod(typeof(HandAimDriver), nameof(BeforeWeaponUpdate)),
                    finalizer: new HarmonyMethod(typeof(HandAimDriver), nameof(AfterWeaponUpdate)));
            if (AutonomousHeadAim)
                h.Patch(AccessTools.Method(typeof(PlayerController), "Update"),
                    prefix: new HarmonyMethod(typeof(HandAimDriver), nameof(BeforePlayerUpdate)),
                    finalizer: new HarmonyMethod(typeof(HandAimDriver), nameof(AfterPlayerUpdate)));
            weaponRuntimeHarmony = h; runtimeDriver = this;
        }
        catch
        {
            h.UnpatchSelf(); runtimeSource = null; runtimePlayer = null;
            throw; // never silently run edge-only fire without held-input projection
        }
    }

    private void ApplyAutonomousGaze(Camera source)
    {
        if (!AutonomousHeadAim || !hasHead || ShotScopeDepth != 0) return;
        if (!sourceRestCaptured)
        {
            sourceRestPosition = source.transform.localPosition;
            sourceRestRotation = source.transform.localRotation;
            sourceRestCaptured = true;
        }
        // Preserve ONLY controller mounts. Top-arm/PASSIVE inherit gaze and keep authored local
        // animation/target selection. Detached projectiles/turrets are never rotated or reparented.
        var roots = new HashSet<Transform>();
        foreach (var weapon in source.GetComponentsInChildren<Weapon>(false))
        {
            if (weapon.current_weapon_slot != EWeaponSlot.LEFT_HAND && weapon.current_weapon_slot != EWeaponSlot.RIGHT_HAND) continue;
            var root = weapon.transform;
            while (root.parent != null && root.parent != source.transform) root = root.parent;
            // Newly acquired/unwritten mounts must inherit gaze with authored LOCAL rest intact.
            // Only controller-driven roots need world preservation. Never relax the rest threshold.
            bool driven = (root == left.Container && HandAimPolicy.PreserveWorldForGaze(left.HasWritten)) ||
                (root == right.Container && HandAimPolicy.PreserveWorldForGaze(right.HasWritten));
            if (root.parent == source.transform && driven) roots.Add(root);
        }
        var preserved = roots.Select(t => (Root: t, Position: t.position, Rotation: t.rotation)).ToArray();
        source.transform.SetPositionAndRotation(headPos, headRot);
        foreach (var p in preserved) p.Root.SetPositionAndRotation(p.Position, p.Rotation);
        GazeWrites++;
    }

    private static void BeforePlayerUpdate(PlayerController __instance)
    {
        var d = runtimeDriver;
        if (d == null || d.runtimePlayer != __instance || d.runtimeSource == null) return;
        try { d.ApplyAutonomousGaze(d.runtimeSource); }
        catch (Exception e) { d.WeaponRuntimeError = e.Message; }
        d.ObservePlayerPhase("player-before-native-after-gaze");
    }
    private static void AfterPlayerUpdate(PlayerController __instance)
    {
        var d = runtimeDriver;
        if (d == null || d.runtimePlayer != __instance || d.runtimeSource == null) return;
        d.ObservePlayerPhase("player-after-native-before-gaze");
        try { d.ApplyAutonomousGaze(d.runtimeSource); }
        catch (Exception e) { d.WeaponRuntimeError = e.Message; }
        // D-193: Unity invokes this Harmony finalizer after PlayerController.Update. The game's flame
        // emitter uses the player/camera-facing muzzle during Update; re-hold the tracked hand mount and
        // pin its emitter now before subsequent Unity particle work. Whether this is before the game's exact
        // particle emission callback is runtime/version dependent and counted in the hand-aim receipt.
        d.HoldHandsBeforeParticles(__instance);
        d.ObservePlayerPhase("player-after-native-after-gaze");
    }

    private sealed class HeldInvocation : IDisposable
    {
        private readonly IDisposable scope;
        private readonly IDisposable? aimScope;
        internal HeldInvocation(HandAimDriver driver, PlayerController player, PlayerInputController input, bool held, bool secondary, ShotFrame? aim)
        {
            // D-113: native reload/continued-fire branches read PLAYER held flags, not just input.
            // Both aliases describe this one owned weapon; other weapon updates get their own scope.
            // D-186 (user decision 2026-10-16): the SECONDARY pair mirrors the Y/B face buttons (left Y /
            // right B), never the trigger. The trigger drives the primary pair only.
            scope = WorldPoseScope<(bool, bool, bool, bool)>.Enter(
                () => (input.firePressing, input.fire2Pressing,
                    player.is_holding_down_shoot_button, player.is_holding_down_seccond_shoot_button),
                p =>
                {
                    try { input.firePressing = p.Item1; }
                    finally
                    {
                        try { input.fire2Pressing = p.Item2; }
                        finally
                        {
                            try { player.is_holding_down_shoot_button = p.Item3; }
                            finally { player.is_holding_down_seccond_shoot_button = p.Item4; }
                        }
                    }
                },
                Array.Empty<(Func<(bool, bool, bool, bool)>, Action<(bool, bool, bool, bool)>)>(), (held, secondary, held, secondary));
            try { if (held && aim != null) aimScope = driver.BeginShotScope(driver.runtimeSource!, aim); }
            catch { scope.Dispose(); throw; }
        }
        public void Dispose()
        {
            try { aimScope?.Dispose(); }
            finally { scope.Dispose(); }
        }
    }

    private static void BeforeWeaponUpdate(Weapon __instance, out HeldInvocation? __state)
    {
        __state = null;
        var d = runtimeDriver;
        if (d == null || !d.StockFireCadence || d.runtimeSource == null || d.runtimePlayer == null ||
            __instance == null || !__instance.transform.IsChildOf(d.runtimeSource.transform)) return;
        var slot = __instance.current_weapon_slot == EWeaponSlot.LEFT_HAND ? d.left :
            __instance.current_weapon_slot == EWeaponSlot.RIGHT_HAND ? d.right : null;
        if (slot == null) return; // top-arm/passive input/timing stays native
        try
        {
            var input = d.runtimePlayer._playerInputController;
            if (input == null) throw new InvalidOperationException("owned weapon has no stock input controller");
            bool held = !d.fireFailed && slot.FireHeld && slot.State == HandAimState.Driving && slot.SampleActive &&
                !slot.Face && Time.timeScale > 0.01f && !d.runtimePlayer.lock_shoot_input;
            // D-186: secondary/alt fire rides the Y/B face buttons (slot.Face), never the trigger. A face press
            // that is currently the head-near reload gesture (reloadLHeld/reloadRHeld) stays out of the pair.
            bool reloading = ReferenceEquals(slot, d.left) ? d.reloadLHeld : d.reloadRHeld;
            bool secondary = slot.Face && !reloading && !d.fireFailed && slot.State == HandAimState.Driving &&
                slot.SampleActive && Time.timeScale > 0.01f && !d.runtimePlayer.lock_shoot_input;
            __state = new HeldInvocation(d, d.runtimePlayer, input, held, secondary, d.AimFor(__instance));
            d.NativeWeaponUpdates++;
            if (held) d.HeldWeaponUpdates++;
        }
        catch (Exception e) { d.WeaponRuntimeError = e.Message; d.fireFailed = true; d.fireError = e.Message; }
    }

    private static void AfterWeaponUpdate(HeldInvocation? __state)
    {
        try { __state?.Dispose(); }
        catch (Exception e)
        {
            if (runtimeDriver != null) { runtimeDriver.WeaponRuntimeError = e.Message; runtimeDriver.fireFailed = true; }
        }
    }

    private void ReleaseWeaponRuntime()
    {
        if (runtimeDriver == this) runtimeDriver = null;
        try { weaponRuntimeHarmony?.UnpatchSelf(); }
        finally
        {
            weaponRuntimeHarmony = null;
            if (sourceRestCaptured && runtimeSource != null)
                runtimeSource.transform.SetLocalPositionAndRotation(sourceRestPosition, sourceRestRotation);
            sourceRestCaptured = false; runtimeSource = null; runtimePlayer = null;
        }
    }
}