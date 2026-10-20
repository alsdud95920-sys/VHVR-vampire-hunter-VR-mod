using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace VhvrExtras;

// Resolves the shipped plugin's internal types/methods by name and records exactly which hooks landed.
// Nothing here claims a patch "works": applying a Harmony patch only proves the method was found and wrapped,
// so the receipt wording stays at PATCH_APPLIED / PATCH_FAILED until a playtest shows the behaviour.
internal sealed class PatchRegistry
{
    private static ManualLogSource? sharedLog;

    internal static void LogInfo(string message)
    {
        sharedLog?.LogInfo("[Extras] " + message);
    }

    internal static void LogWarning(string message)
    {
        sharedLog?.LogWarning("[Extras] " + message);
    }

    private readonly Harmony harmony;
    private readonly ManualLogSource log;
    private readonly List<string> applied = new();
    private readonly List<string> failed = new();
    private readonly List<string> deferred = new();

    internal PatchRegistry(Harmony harmony, ManualLogSource log)
    {
        this.harmony = harmony;
        this.log = log;
        sharedLog = log;
    }

    internal MethodBase? Find(string typeName, string methodName, string label, int? parameterCount = null)
    {
        var type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            failed.Add(label + ":type-missing");
            log.LogWarning("[Extras] PATCH_FAILED " + label + ": type '" + typeName + "' not found");
            return null;
        }

        var method = parameterCount == null
            ? AccessTools.Method(type, methodName)
            : type.GetMethods(AccessTools.all).FirstOrDefault(m =>
                m.Name == methodName && !m.IsGenericMethod && m.GetParameters().Length == parameterCount);
        if (method == null)
        {
            failed.Add(label + ":method-missing");
            log.LogWarning("[Extras] PATCH_FAILED " + label + ": method '" + typeName + "." + methodName + "' not found");
            return null;
        }
        return method;
    }

    internal bool Hook(MethodBase target, string label, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
    {
        try
        {
            harmony.Patch(target, prefix: prefix, postfix: postfix);
            applied.Add(label);
            log.LogInfo("[Extras] PATCH_APPLIED " + label);
            return true;
        }
        catch (Exception e)
        {
            failed.Add(label + ":" + e.GetType().Name);
            log.LogWarning("[Extras] PATCH_FAILED " + label + ": " + e.Message);
            return false;
        }
    }

    internal void ApplyAll(ExtrasConfig config)
    {
        FireLoopPatch.Apply(this, config);
        // D-186: AbilityFacePatch REMOVED - Y/B is the weapon's secondary/alt fire from core v0.7.0 (user
        // decision 2026-10-16), so the extras must not consume the face press as an ability any more.
        UiScalePatch.Apply(this, config);
        MenuScreenPatch.Apply(this, config);
    }

    // A requested behaviour that this build does not hook yet. Recorded instead of skipped silently.
    internal void Defer(string label, string reason)
    {
        deferred.Add(label + "(" + reason + ")");
        log.LogWarning("[Extras] NOT_IMPLEMENTED " + label + ": " + reason);
    }

    internal string Summary => "applied=" + applied.Count + " failed=" + failed.Count + " deferred=" + deferred.Count +
        " [" + string.Join(", ", applied) + "] failed[" + string.Join(", ", failed) + "] deferred[" +
        string.Join(", ", deferred) + "]";
}
