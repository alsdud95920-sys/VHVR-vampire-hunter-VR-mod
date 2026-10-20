using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace VhvrExtras;

// D-174: a patch plugin that adds five requested behaviours on top of the shipped VampireHuntersVR.Stage0
// plugin. It never touches the game's IL2CPP code and never rebuilds Stage0; it only hooks Stage0's own
// managed methods (HandAimDriver / the policy classes it calls). Every hook is applied through reflection so
// this assembly does not need compile-time access to Stage0's internal types.
[BepInPlugin(Guid, "Vampire Hunters VR Extras", "0.1.0")]
[BepInDependency("local.vampirehuntersvr.stage0", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class ExtrasPlugin : BasePlugin
{
    public const string Guid = "local.vampirehuntersvr.extras";

    private Harmony? harmony;
    private ExtrasConfig? settings;

    public override void Load()
    {
        settings = new ExtrasConfig(Config);
        harmony = new Harmony(Guid);

        // D-178: the native hand sampler can only be enabled while the host is still uninitialised (Host.cpp:
        // "only before the init event"). Stage0's StartOpenXrHost only adds the component - its Initialize runs on
        // the first Update, ~15 s in - so the extras' own Load is the reliable window to ask for the controller
        // poses the menu pointer needs. Everything is logged because a silent failure here means no laser.
        try
        {
            var hands = MenuScreenRig.EnableNativeHands();
            Log.LogInfo("[Extras] pre-load native hands enable result=" + hands);
        }
        catch (Exception e)
        {
            Log.LogWarning("[Extras] pre-load EnableHands failed: " + e.GetType().Name + ": " + e.Message);
        }

        var registry = new PatchRegistry(harmony, Log);
        registry.ApplyAll(settings);
        Log.LogInfo("[Extras] LOADED " + registry.Summary);
    }

    public override bool Unload()
    {
        MenuScreenPatch.Shutdown();
        harmony?.UnpatchSelf();
        Log.LogInfo("[Extras] UNLOADED (all patches removed)");
        return true;
    }
}
