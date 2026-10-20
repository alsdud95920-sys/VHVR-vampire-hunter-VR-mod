namespace VhvrExtras;

// Pure decision for requested behaviour 5 (re-sending a fire signal while the trigger is held).
// Kept free of Unity types so tests/VhvrExtras can drive it deterministically.
//
// D-176 (user decision 2026-10-14): the goal is "crossbow-like" weapons that normally need one press per
// shot. Discriminating auto from per-press weapons is not proven at runtime, so instead EVERY weapon gets an
// extra fire signal at a fixed interval while the trigger is held (the game's own cooldown still caps the
// real rate). The signal is only for the held case and never on top of a tick the shipped build already fired.
internal static class SemiAutoFirePolicy
{
    internal const float NoSignal = -100f;

    // held           the trigger is still down this tick
    // alreadyFiring  the shipped build already decided to fire this tick (a per-press edge, or an auto tick)
    // Returns true when an extra fire signal must be synthesised for this tick.
    internal static bool ShouldSignal(bool held, bool alreadyFiring, float now, float interval,
        ref float lastSignal)
    {
        if (!held)
        {
            lastSignal = NoSignal;
            return false;
        }

        if (!float.IsFinite(now) || interval <= 0f)
        {
            return false;
        }

        // The shipped build is already firing this tick; treat that as the shot and restart the interval so
        // an auto weapon never receives a duplicate signal on top of its own cadence.
        if (alreadyFiring)
        {
            lastSignal = now;
            return false;
        }

        // A fresh hold starts the interval and does not signal immediately: the first shot comes from the
        // game's own press edge, so an extra signal right then would double it.
        if (lastSignal <= NoSignal)
        {
            lastSignal = now;
            return false;
        }

        if (now - lastSignal < interval)
        {
            return false;
        }

        lastSignal = now;
        return true;
    }
}
