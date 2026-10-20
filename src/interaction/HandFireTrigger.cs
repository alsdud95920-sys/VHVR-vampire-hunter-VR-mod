using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-111: native fire consumes ref press and starts the slot. Weapon.update owns sustained fire.
// Start on press edge, release on release edge; WeaponRuntimeBridge projects per-hand held input
// only during the native update. No global input event, keyboard/mouse synthesis or RPM/ammo tuning.
// Flag-off explicitly retains the old repeated-start path for diagnosis.
internal sealed partial class HandAimDriver
{
    public int CamAimed;
    private bool fireFailed;
    private string? fireError;
    private PlayerController? firedPlayer;

    // Actual-shot diagnostics only: an unheld shot may be a native burst tail, never suppress it here.
    internal bool IsFireHeld(EWeaponSlot slot) => slot == EWeaponSlot.LEFT_HAND ? left.FireHeld :
        slot == EWeaponSlot.RIGHT_HAND && right.FireHeld;

    // Each hand fires only its own slot. The upper face button is reserved for the secondary-fire selector (not wired):
    // while it is held this hand never fires the primary.
    private void FireTick(PlayerController player, Camera source)
    {
        firedPlayer = player;
        bool allowedWorld = !fireFailed && Time.timeScale > 0.01f && !player.lock_shoot_input;
        foreach (var slot in new[] { left, right })
        {
            ShotFrame? aim = slot.Container == null ? null : slot.Container.GetComponentsInChildren<Weapon>(false)
                .Select(AimFor).FirstOrDefault(a => a != null);
            bool allowed = allowedWorld && slot.State == HandAimState.Driving && slot.WeaponCount > 0 && !slot.Face && aim != null;
            var edge = HandFirePolicy.Next(slot.FireHeld, slot.Trigger, allowed, out bool held);
            slot.FireHeld = held;
            if (edge == TriggerEdge.Pressed) slot.Presses++;
            if (held) slot.HeldSeconds += Time.deltaTime;
            try
            {
                if (HandFirePolicy.StartThisTick(edge, held, StockFireCadence))
                {
                    bool pressed = true;
                    // D-110: use the published independent hand/weapon frame. Preserve BOTH weapon roots.
                    using (BeginShotScope(source, aim!))
                    {
                        CamAimed++;
                        player.fire(slot.Slot, ref pressed, Time.deltaTime);
                    }
                    slot.FireCalls++;
                }
                if (edge == TriggerEdge.Released)
                {
                    bool released = true;
                    player.fire_release(slot.Slot, ref released);
                    slot.ReleaseCalls++;
                }
            }
            catch (System.Exception e)
            {
                fireFailed = true; fireError = e.GetType().Name + ": " + e.Message;
                ReleaseFireBestEffort();
                return;
            }
        }
    }

    // Release best effort so a held trigger never leaves a weapon firing after the session or driver ends.
    private void ReleaseFireBestEffort()
    {
        var player = firedPlayer;
        foreach (var slot in new[] { left, right })
        {
            if (!slot.FireHeld) continue;
            slot.FireHeld = false;
            try
            {
                if (player != null) { bool released = true; player.fire_release(slot.Slot, ref released); slot.ReleaseCalls++; }
            }
            catch (System.Exception) { }
        }
        firedPlayer = null;
    }
}