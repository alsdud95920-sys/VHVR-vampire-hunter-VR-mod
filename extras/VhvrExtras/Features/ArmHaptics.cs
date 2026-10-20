using System.Runtime.InteropServices;
using BepInEx.Configuration;
using UnityEngine;

namespace VhvrExtras;

// D-183 / D-186 / D-187: robot-arm cooldown haptics driven by the game's actual SecondaryWeapon state.
//
// Disassembly of SecondaryWeapon in GameAssembly.dll (all 23 robot-arm weapons inherit from SecondaryWeapon):
//   - SecondaryWeapon.start (+0x2E8): false during the initial arm deploy animation, set to true by
//     mech_arm_start_check (0x1804A0AE0) along with can_attack = true.
//   - SecondaryWeapon.can_attack (+0x2DC) & has_attack_ended (+0x2DD):
//     set to false when the arm fires (default_shoot, 0x1804A0880); while !can_attack, attack_t (+0x2D8,
//     initialised from Weapon.reload_time()) counts down in default_secondary_weapon_reload (0x1804A0700)
//     (and is also reduced by on_enemy_killed / force_reload / maybe_death_reload). When attack_t <= 0,
//     default_reload (0x1804A05B0) sets can_attack = true and calls Weapon.toggle_top_arm_button_hint(true).
//   - The old D-183 check used Weapon.second_shot_cooldown (15 s base-class field for primary weapons'
//     level-3 alt fire), which is why the arm never buzzed on its real cooldown and only buzzed 15 s after
//     a weapon happened to update last_shot_t.
//   - Also fixed in D-187: RequestHaptic returns 6 when the render thread holds host.mutex; we now check
//     tracked arms every frame and keep PendingRetry=true on result 6 so the next frame retries immediately.
internal static class ArmHaptics
{
    private const string Dll = "VhvrNativeHost.dll";
    private const float ScanInterval = 0.25f;

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RequestHaptic(int hand, float amplitude, int durationMs);

    private static ConfigEntry<bool>? hapticEnabled;
    private static ConfigEntry<float>? hapticAmplitude;
    private static ConfigEntry<int>? hapticMilliseconds;

    private sealed class ArmState
    {
        public int Hand;
        public Weapon? Weapon;
        public SecondaryWeapon? Secondary;
        public ArmReadyState ReadyState = ArmReadyState.Create();
        public float LastShot = -1f;
        public float Cooldown = -1f;
        public string Name = "";
    }

    private static readonly Dictionary<int, ArmState> Arms = new();
    private static readonly List<int> DeadIds = new();
    private static float nextScan;
    private static int readyPulses, rejected, busyRetries;
    private static bool warned;

    // D-186: one pending delivery check per queued pulse.
    private static bool deliveryPending;
    private static float deliveryAt;
    private static int deliveryReservedBefore = -1;

    internal static void Apply(PatchRegistry registry, ExtrasConfig config)
    {
        hapticEnabled = config.CooldownHaptic;
        hapticAmplitude = config.CooldownHapticAmplitude;
        hapticMilliseconds = config.CooldownHapticMilliseconds;
    }

    // Called from the already-hooked HandAimDriver.FireTick postfix once per gameplay frame.
    internal static void Tick()
    {
        if (hapticEnabled is not { Value: true })
        {
            return;
        }

        var now = Time.time;

        try
        {
            if (deliveryPending && now >= deliveryAt)
            {
                deliveryPending = false;
                var reserved = MenuScreenRig.HapticAppliedCount();
                PatchRegistry.LogInfo("arm-haptic delivery-check reserved=" + reserved + " before=" + deliveryReservedBefore +
                    " -> " + (reserved > deliveryReservedBefore
                        ? "applied by the native pump (controller buzzed)"
                        : "NOT applied (queue lost, haptics disabled, or session stopped)"));
            }

            if (now >= nextScan)
            {
                nextScan = now + ScanInterval;
                ScanWeapons();
            }

            TickTrackedArms(now);
        }
        catch (Exception e)
        {
            if (!warned)
            {
                warned = true;
                PatchRegistry.LogWarning("arm haptics stopped: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
    private static void ScanWeapons()
    {
        foreach (var weapon in Resources.FindObjectsOfTypeAll<Weapon>())
        {
            if (weapon == null || !weapon.gameObject.activeInHierarchy)
            {
                continue;
            }

            var slot = weapon.current_weapon_slot;
            int hand;
            if (slot == EWeaponSlot.LEFT_TOP_HAND) hand = 0;
            else if (slot == EWeaponSlot.RIGHT_TOP_HAND) hand = 1;
            else continue;

            var id = weapon.GetInstanceID();
            if (!Arms.TryGetValue(id, out var state))
            {
                var secondary = weapon.TryCast<SecondaryWeapon>();
                state = new ArmState
                {
                    Hand = hand,
                    Weapon = weapon,
                    Secondary = secondary,
                    Name = weapon.gameObject.name,
                };
                if (secondary != null)
                {
                    CooldownHapticPolicy.ShouldPulseSecondaryArm(
                        secondary.start, secondary.can_attack, secondary.has_attack_ended, ref state.ReadyState);
                    float reloadSec = SafeReloadTime(weapon, secondary.attack_t);
                    state.Cooldown = reloadSec;
                    PatchRegistry.LogInfo("arm tracked hand=" + (hand == 0 ? "LEFT" : "RIGHT") + " weapon=" + state.Name +
                        " mode=SecondaryWeapon reloadTime=" + reloadSec.ToString("0.00") + "s start=" + secondary.start +
                        " canAttack=" + secondary.can_attack + " attackEnded=" + secondary.has_attack_ended +
                        " attackT=" + secondary.attack_t.ToString("0.00"));
                }
                else
                {
                    state.LastShot = weapon.last_shot_t;
                    state.Cooldown = SafeReloadTime(weapon, weapon.second_shot_cooldown);
                    bool ready = state.LastShot > 0f && state.Cooldown > 0f && Time.time - state.LastShot >= state.Cooldown;
                    state.ReadyState.Started = true;
                    state.ReadyState.WasReady = ready;
                    PatchRegistry.LogInfo("arm tracked hand=" + (hand == 0 ? "LEFT" : "RIGHT") + " weapon=" + state.Name +
                        " mode=WeaponFallback cooldown=" + state.Cooldown.ToString("0.00") + "s ready=" + ready);
                }
                Arms[id] = state;
            }
            else
            {
                state.Hand = hand;
                state.Weapon = weapon;
                state.Secondary ??= weapon.TryCast<SecondaryWeapon>();
            }
        }
    }

    private static void TickTrackedArms(float now)
    {
        if (Arms.Count == 0)
        {
            return;
        }

        DeadIds.Clear();
        foreach (var pair in Arms)
        {
            var state = pair.Value;
            var weapon = state.Weapon;
            if (weapon == null)
            {
                DeadIds.Add(pair.Key);
                continue;
            }
            if (!weapon.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool shouldPulse;
            var sec = state.Secondary;
            if (sec != null)
            {
                bool wasReadyBefore = state.ReadyState.WasReady;
                shouldPulse = CooldownHapticPolicy.ShouldPulseSecondaryArm(
                    sec.start, sec.can_attack, sec.has_attack_ended, ref state.ReadyState);
                if (!sec.can_attack && wasReadyBefore && sec.start)
                {
                    state.Cooldown = SafeReloadTime(weapon, sec.attack_t);
                    PatchRegistry.LogInfo("arm fired hand=" + (state.Hand == 0 ? "LEFT" : "RIGHT") +
                        " weapon=" + state.Name + " attackT=" + sec.attack_t.ToString("0.00") +
                        "s reloadTime=" + state.Cooldown.ToString("0.00") + "s");
                }
            }
            else
            {
                float lastShot = weapon.last_shot_t;
                float cooldown = SafeReloadTime(weapon, weapon.second_shot_cooldown);
                if (state.LastShot >= 0f && lastShot > state.LastShot + 0.001f)
                {
                    state.ReadyState.WasReady = false;
                    state.ReadyState.PendingRetry = false;
                }
                state.LastShot = lastShot;
                if (float.IsFinite(cooldown) && cooldown > 0f)
                {
                    state.Cooldown = cooldown;
                }
                bool ready = state.LastShot > 0f && float.IsFinite(state.Cooldown) && state.Cooldown > 0f &&
                             now - state.LastShot >= state.Cooldown;
                shouldPulse = CooldownHapticPolicy.ShouldPulseSecondaryArm(true, ready, true, ref state.ReadyState);
            }

            if (shouldPulse)
            {
                var amplitude = hapticAmplitude?.Value ?? 0.8f;
                var milliseconds = hapticMilliseconds?.Value ?? 180;
                var result = RequestHaptic(state.Hand, amplitude, milliseconds);
                CooldownHapticPolicy.RecordResult(result, ref state.ReadyState);
                if (result == 6)
                {
                    busyRetries++;
                    continue;
                }

                readyPulses++;
                var who = "arm-ready pulse #" + readyPulses + " hand=" + (state.Hand == 0 ? "LEFT" : "RIGHT") +
                    " weapon=" + state.Name + " cooldown=" + state.Cooldown.ToString("0.00") + "s (amp=" +
                    amplitude.ToString("0.00") + ", " + milliseconds + "ms, retries=" + busyRetries + ")";
                busyRetries = 0;
                if (result != 0)
                {
                    rejected++;
                    PatchRegistry.LogWarning(who + " -> " + result + " (" + ResultName(result) + ")");
                }
                else
                {
                    PatchRegistry.LogInfo(who + " -> 0 (queued)");
                    deliveryReservedBefore = MenuScreenRig.HapticAppliedCount();
                    deliveryPending = true;
                    deliveryAt = now + 0.5f;
                }
            }
        }

        for (int i = 0; i < DeadIds.Count; i++)
        {
            Arms.Remove(DeadIds[i]);
        }
    }

    private static float SafeReloadTime(Weapon weapon, float fallback)
    {
        try
        {
            float reload = weapon.reload_time();
            if (float.IsFinite(reload) && reload > 0f)
            {
                return reload;
            }
        }
        catch (Exception)
        {
        }
        return float.IsFinite(fallback) && fallback > 0f ? fallback : 0f;
    }

    private static string ResultName(int result) => result switch
    {
        0 => "queued",
        1 => "haptics unavailable (no hands yet, or disabled after a failure)",
        2 => "bad argument (amplitude must be 0..1, duration 1..500 ms)",
        6 => "native host busy - retry next frame",
        _ => "unknown result code",
    };

    internal static void Reset()
    {
        Arms.Clear();
        DeadIds.Clear();
        nextScan = 0f;
        readyPulses = 0;
        rejected = 0;
        busyRetries = 0;
        warned = false;
        deliveryPending = false;
        deliveryReservedBefore = -1;
    }
}