using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace VhvrExtras;

// Requested behaviours 2 (robot-arm ready buzzer) and 5 (keep firing while the trigger is held).
//
// D-183 update (user decisions 2026-10-15):
//   2. The arm-ready buzzer is driven by the WEAPON'S OWN cooldown and lives in ArmHaptics.cs
//      (last_shot_t + the per-weapon second_shot_cooldown). The old fixed-0.5 s model is gone.
//   5. Every weapon gets an extra fire signal every RetriggerSeconds while the trigger is held, so a
//      one-press-per-shot weapon (crossbow) keeps firing. A tick the shipped build already fired resets
//      the interval instead of adding a signal, so automatic weapons are never double-tapped.
//      D-180: StartThisTick is marked NoInlining in the rebuilt Stage0 because a JIT that inlines this tiny
//      method bypasses this patch entirely (observed: fireCalls == triggerPresses over 18 s of holding).
internal static class FireLoopPatch
{
    private static ConfigEntry<bool>? retriggerEnabled;
    private static ConfigEntry<float>? retriggerSeconds;

    private static readonly float[] LastSignal = { SemiAutoFirePolicy.NoSignal, SemiAutoFirePolicy.NoSignal };
    private static int slotCursor;

    private static int retriggerCalls;
    private static int retriggerSignals;

    internal static void Apply(PatchRegistry registry, ExtrasConfig config)
    {
        ArmHaptics.Apply(registry, config); // D-183: the arm pulse is driven by each weapon's own cooldown

        retriggerEnabled = config.SemiAutoRetrigger;
        retriggerSeconds = config.SemiAutoRetriggerSeconds;

        var fireTick = registry.Find("VampireHuntersVR.Interaction.HandAimDriver", "FireTick", "fire.loop", 2);
        if (fireTick != null)
        {
            registry.Hook(fireTick, "HandAimDriver.FireTick->slot-context",
                postfix: new HarmonyMethod(typeof(FireLoopPatch), nameof(FireTickPostfix)));
        }

        var startThisTick = registry.Find("VampireHuntersVR.Interaction.HandFirePolicy", "StartThisTick", "fire.retrigger");
        if (startThisTick != null)
        {
            registry.Hook(startThisTick, "HandFirePolicy.StartThisTick->retrigger",
                postfix: new HarmonyMethod(typeof(FireLoopPatch), nameof(StartThisTickPostfix)));
        }
    }

    private static void FireTickPostfix(object __instance)
    {
        ArmHaptics.Tick();
    }

    private static void StartThisTickPostfix(bool __1, ref bool __result)
    {
        if (retriggerEnabled is not { Value: true })
        {
            return;
        }

        retriggerCalls++;
        if (retriggerCalls == 1)
        {
            PatchRegistry.LogInfo("auto-fire hook is being called by the game (StartThisTick postfix alive)");
        }

        var index = slotCursor < 2 ? slotCursor : 0;
        slotCursor = index + 1;
        var step = retriggerSeconds?.Value ?? 0.6f;
        // __1 = the trigger is held this tick; __result = the shipped build already decided to fire.
        if (SemiAutoFirePolicy.ShouldSignal(__1, __result, Time.time, step, ref LastSignal[index]))
        {
            __result = true;
            retriggerSignals++;
            if (retriggerSignals <= 5 || retriggerSignals % 50 == 0)
            {
                PatchRegistry.LogInfo("auto-fire signal #" + retriggerSignals + " sent to hand " + index);
            }
        }
    }
}
