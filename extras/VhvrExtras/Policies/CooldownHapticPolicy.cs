namespace VhvrExtras;

// Pure model for requested behaviour 2 (buzz the controller when the weapon is ready again).
//
// Honest limitation: the game does not expose "cooldown finished" to managed code in a way this plugin can
// read reliably (the Weapon timers have no documented semantics in the interop assembly). So the cooldown is
// MODELLED: every shot the shipped build actually fires starts an interval derived from that weapon's own
// fire rate, and one pulse is sent when the interval elapses with no new shot. It therefore announces "your
// weapon is ready again", not a value read out of the game.
internal struct CooldownHapticState
{
    public float ReadyAt;
    public bool Pulsed;

    internal static CooldownHapticState Create()
    {
        return new CooldownHapticState { ReadyAt = CooldownHapticPolicy.Idle, Pulsed = true };
    }
}

internal static class CooldownHapticPolicy
{
    internal const float Idle = -100f;

    // fireRatePerSecond comes from Weapon.attributes.base_fire_rate. Missing/absurd values fall back to the
    // configured default interval instead of inventing a number.
    internal static float Interval(float fireRatePerSecond, float minSeconds, float maxSeconds, float fallbackSeconds)
    {
        if (!float.IsFinite(fireRatePerSecond) || fireRatePerSecond <= 0.01f)
        {
            return Clamp(fallbackSeconds, minSeconds, maxSeconds);
        }

        return Clamp(1f / fireRatePerSecond, minSeconds, maxSeconds);
    }

    internal static float Clamp(float value, float min, float max)
    {
        if (!float.IsFinite(value))
        {
            return min;
        }
        if (value < min)
        {
            return min;
        }
        if (value > max)
        {
            return max;
        }
        return value;
    }

    internal static bool Tick(bool firedThisTick, float now, float intervalSeconds, ref CooldownHapticState state)
    {
        if (!float.IsFinite(now) || intervalSeconds <= 0f)
        {
            return false;
        }

        if (firedThisTick)
        {
            state.ReadyAt = now + intervalSeconds;
            state.Pulsed = false;
            return false;
        }

        if (state.Pulsed || state.ReadyAt <= Idle || now < state.ReadyAt)
        {
            return false;
        }

        state.Pulsed = true;
        return true;
    }

    // D-187: real SecondaryWeapon (robot arm) cooldown state machine.
    // Disassembly of SecondaryWeapon (GameAssembly.dll):
    //   - default_secondary_weapon_init (0x1804A0610): start = false (+0x2E8), attack_t = reload_time() (+0x2D8),
    //     can_attack = false (+0x2DC), has_attack_ended = true (+0x2DD).
    //   - mech_arm_start_check (0x1804A0AE0): when the initial deploy animation ends, sets start = true,
    //     can_attack = true, and calls toggle_top_arm_button_hint(true). That initial deploy is NOT a cooldown.
    //   - default_shoot (0x1804A0880): sets can_attack = false, has_attack_ended = false, hides button hint.
    //   - default_secondary_weapon_reload (0x1804A0700) / default_reload (0x1804A05B0) / force_reload (0x1804A0980):
    //     decrements attack_t and, when attack_t <= 0, sets can_attack = true and shows the button hint.
    //   - StartVfxCooldown (0x18049EED0): sets has_attack_ended = true when the attack animation finishes.
    //   - Every SecondaryWeapon.shoot checks (can_attack && has_attack_ended).
    // Plus: RequestHaptic returns 6 when the native render thread holds host.mutex; RecordResult keeps
    // PendingRetry=true on 6 so the next frame retries instead of dropping the pulse.
    internal static bool ShouldPulseSecondaryArm(bool started, bool canAttack, bool hasAttackEnded, ref ArmReadyState state)
    {
        if (!started)
        {
            state.Started = false;
            state.WasReady = true;
            state.PendingRetry = false;
            return false;
        }

        bool ready = canAttack && hasAttackEnded;
        if (!state.Started)
        {
            state.Started = true;
            // If discovered already ready (normal initial deploy), stay silent; if discovered mid-cooldown,
            // arm the state so it pulses when the cooldown finishes.
            state.WasReady = ready;
            state.PendingRetry = false;
            return false;
        }

        if (!ready)
        {
            state.WasReady = false;
            state.PendingRetry = false;
            return false;
        }

        return !state.WasReady || state.PendingRetry;
    }

    internal static void RecordResult(int nativeResult, ref ArmReadyState state)
    {
        if (nativeResult == 6)
        {
            // Native host mutex busy (render thread in Prepare/Submit); retry on the very next frame.
            state.WasReady = false;
            state.PendingRetry = true;
            return;
        }

        state.WasReady = true;
        state.PendingRetry = false;
    }
}

internal struct ArmReadyState
{
    public bool Started;
    public bool WasReady;
    public bool PendingRetry;

    internal static ArmReadyState Create() => new() { Started = false, WasReady = true, PendingRetry = false };
}
