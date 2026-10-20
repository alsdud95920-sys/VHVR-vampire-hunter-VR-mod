using System.Text;
using UnityEngine;

namespace VhvrExtras;

// D-181 (user plan 2026-10-15): OBSERVATION ONLY. The user wants the controller to buzz when a robot-arm weapon becomes
// usable again, and the arm weapons differ in cooldown, so a fixed 0.5 s model is wrong. The game exposes several
// candidate readouts but it is NOT yet known which one the arm weapons actually use. This sampler logs, for every
// ACTIVE weapon in a non-primary slot (LEFT/RIGHT_TOP_HAND, PASSIVE) and in the user's "Auxiliary / Secondary" category,
// the candidate timers once per second PLUS every moment one of them changes meaning (a ready edge), so ONE playtest
// shows which signal flips to "usable". It writes nothing to the game and drives no haptics.
internal static class ArmCooldownProbe
{
    private static float nextSample;
    private static int lines;
    private const int MaxLines = 160;
    private static readonly Dictionary<int, string> LastState = new();

    internal static void Reset()
    {
        nextSample = 0f;
        lines = 0;
        LastState.Clear();
    }

    // Called from the already-hooked HandAimDriver.FireTick postfix, so it runs once per gameplay frame on the main thread.
    internal static void Tick()
    {
        var now = Time.time;
        if (now < nextSample || lines >= MaxLines)
        {
            return;
        }
        nextSample = now + 0.25f;

        try
        {
            foreach (var weapon in Resources.FindObjectsOfTypeAll<Weapon>())
            {
                if (weapon == null || !weapon.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var slot = weapon.current_weapon_slot;
                if (slot != EWeaponSlot.LEFT_TOP_HAND && slot != EWeaponSlot.RIGHT_TOP_HAND && slot != EWeaponSlot.PASSIVE)
                {
                    continue;
                }

                var id = weapon.GetInstanceID();
                var state = Describe(weapon, slot);
                if (LastState.TryGetValue(id, out var previous) && previous == state)
                {
                    continue;
                }

                LastState[id] = state;
                lines++;
                PatchRegistry.LogInfo("arm-probe " + state);
            }
        }
        catch (Exception e)
        {
            lines = MaxLines;
            PatchRegistry.LogWarning("arm-probe stopped: " + e.GetType().Name + ": " + e.Message);
        }
    }

    // Everything read here is a plain getter on the game's own Weapon. The string is also the change key, so a line is
    // logged exactly when one of the candidate readouts changes - a ready edge shows up as a line, not as spam.
    private static string Describe(Weapon weapon, EWeaponSlot slot)
    {
        var sb = new StringBuilder(220);
        sb.Append(weapon.gameObject.name).Append(" slot=").Append(slot);
        sb.Append(" type=").Append(Safe(() => weapon.type.ToString()));
        sb.Append(" canSecondary=").Append(Safe(() => weapon.can_use_secondary_shot().ToString()));
        sb.Append(" secondT=").Append(Round(Safe(() => weapon.second_shot_t)));
        sb.Append(" secondCd=").Append(Round(Safe(() => weapon.second_shot_cooldown)));
        sb.Append(" secondDur=").Append(Round(Safe(() => weapon.second_shot_duration)));
        sb.Append(" lastShot=").Append(Round(Safe(() => weapon.last_shot_t)));
        sb.Append(" lastSecond=").Append(Round(Safe(() => weapon.last_secondary_shot_t)));
        sb.Append(" fireT=").Append(Round(Safe(() => weapon.fire_t)));
        sb.Append(" reloadT=").Append(Round(Safe(() => weapon.reload_t)));
        sb.Append(" ammo=").Append(Safe(() => weapon.current_ammo.ToString()));
        sb.Append(" firing=").Append(Safe(() => weapon.firing.ToString()));
        sb.Append(" now=").Append(Round(Time.time));
        return sb.ToString();
    }

    private static string Round(float value)
    {
        return (Mathf.Round(value * 10f) / 10f).ToString("0.0");
    }

    private static string Round(string value)
    {
        return float.TryParse(value, out var number) ? Round(number) : value;
    }

    private static string Safe<T>(Func<T> read)
    {
        try
        {
            return read()?.ToString() ?? "null";
        }
        catch (Exception e)
        {
            return "err:" + e.GetType().Name;
        }
    }
}