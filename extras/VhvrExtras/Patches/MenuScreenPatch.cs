using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace VhvrExtras;

// Requested behaviour 1 wiring.
//
// Two independent things:
//  1. MenuFirst (release default ON): OpenXrHost starts from Camera.main at the menu, then detects the stage
//     PlayerController and installs hand/weapon integration on transition. Keep the config aligned with the
//     Stage0 host setting; a mismatch can leave the host waiting for a gameplay camera indefinitely.
//  2. The panel rig itself: whenever the host is running and the game is not in gameplay (no player), the menu
//     image is presented as a black-background panel 1.5 m ahead with controller lasers and a pointer.
internal static class MenuScreenPatch
{
    private static readonly MenuScreenRig Rig = new();
    private static ConfigEntry<bool>? enabled;
    private static ConfigEntry<bool>? menuFirst;
    private static ConfigEntry<bool>? pointer;
    private static ConfigEntry<bool>? pointerHeadFrame;
    private static bool noHeadPoseLogged;
    private static ConfigEntry<float>? distance;
    private static ConfigEntry<float>? fov;
    private static FieldInfo? configuredField;
    private static FieldInfo? playerField;
    private static FieldInfo? sourceField;
    private static FieldInfo? eyesField;
    private static FieldInfo? doneField;
    private static FieldInfo? anchorPositionField;
    private static FieldInfo? anchorRotationField;
    private static bool warned;
    private static bool loggedMenu;

    internal static void Apply(PatchRegistry registry, ExtrasConfig config)
    {
        enabled = config.MenuScreen;
        menuFirst = config.MenuScreenFirst;
        pointer = config.MenuScreenPointer;
        pointerHeadFrame = config.MenuPointerHeadFrame;
        distance = config.MenuScreenDistance;
        fov = config.MenuScreenFov;
        // D-191: thumbstick scroll speed. No menu backdrop is created; the normal game view remains visible.
        Rig.ScrollSpeed = config.MenuScreenScrollSpeed.Value;
        Rig.PointerEnabled = config.MenuScreenPointer.Value;
        // D-201: both thumbsticks clicked re-places the menu panel in front of the head (config-gated).
        Rig.RecenterChordEnabled = config.MenuRecenterChord.Value;
        PatchRegistry.LogInfo("menu pointer frame=" + (config.MenuPointerHeadFrame.Value
            ? "head/HMD (D-201 default; same frame as the panel and the eye cameras)"
            : "game camera (legacy 0.9.4 behaviour; ray skews ~90deg in the menu)") +
            "; recenter chord=" + config.MenuRecenterChord.Value);

        if (!enabled.Value)
        {
            registry.Defer("menu.panel", "disabled in config");
            return;
        }

        // D-178: register this hook regardless of the legacy MenuFirst flag, because the native hand sampler must be
        // enabled BEFORE the host's init render event (Host.cpp: "only before the init event") or the menu pointer
        // has no controller poses at all.
        var start = registry.Find("VampireHuntersVR.Stage0.Plugin", "StartOpenXrHost", "menu.first");
        if (start != null)
        {
            registry.Hook(start, "Plugin.StartOpenXrHost->menu-first",
                prefix: new HarmonyMethod(typeof(MenuScreenPatch), nameof(StartOpenXrHostPrefix)));
        }

        var update = registry.Find("VampireHuntersVR.Stage0.OpenXrHost", "Update", "menu.panel");
        if (update?.DeclaringType == null)
        {
            return;
        }

        var type = update.DeclaringType;
        configuredField = AccessTools.Field(type, "configured");
        playerField = AccessTools.Field(type, "player");
        sourceField = AccessTools.Field(type, "source");
        eyesField = AccessTools.Field(type, "eyes");
        doneField = AccessTools.Field(type, "done");
        anchorPositionField = AccessTools.Field(type, "anchorPosition");
        anchorRotationField = AccessTools.Field(type, "anchorRotation");
        if (configuredField == null || sourceField == null || eyesField == null)
        {
            registry.Defer("menu.panel", "host fields not found (configured/source/eyes)");
            return;
        }

        registry.Hook(update, "OpenXrHost.Update->menu-panel",
            postfix: new HarmonyMethod(typeof(MenuScreenPatch), nameof(UpdatePostfix)));
    }

    // The extras plugin pre-load already enables native hands while the host is uninitialised. Keep this hook
    // as a startup receipt only; mutating OpenXrHost.Config here is too early because StartOpenXrHost loads JSON
    // in its body after the prefix returns. Stage0's own MenuFirst=true path selects Camera.main and handles handoff.
    private static void StartOpenXrHostPrefix()
    {
        PatchRegistry.LogInfo("menu first startup: configured=" + (menuFirst?.Value == true) +
            "; host config is loaded by StartOpenXrHost body, Stage0 owns camera selection");
    }

    internal static void Shutdown()
    {
        Rig.Dispose();
    }

    private static void UpdatePostfix(object __instance)
    {
        if (enabled is not { Value: true })
        {
            return;
        }

        try
        {
            var configured = configuredField?.GetValue(__instance) is true;
            var done = doneField?.GetValue(__instance) is true;
            var source = sourceField?.GetValue(__instance) as Camera;
            var eyes = eyesField?.GetValue(__instance) as Camera[];
            var player = playerField?.GetValue(__instance);
            if (!configured || done || source == null || eyes == null || eyes.Length == 0 || eyes[0] == null)
            {
                TearDown();
                return;
            }

            if (player != null)
            {
                if (Rig.Built)
                {
                    Rig.Dispose();
                    PatchRegistry.LogInfo("menu ui: gameplay started, the game's canvases were handed back");
                }
                return;
            }

            if (!Rig.Built && !Rig.Build(source, eyes[0]))
            {
                return;
            }

            if (!loggedMenu)
            {
                loggedMenu = true;
                PatchRegistry.LogInfo("menu ui active: world-space canvases at distance=" + (distance?.Value ?? 1.5f) +
                    "m fov=" + (fov?.Value ?? 40f));
            }

            // D-201: place the panel from the LIVE head pose in the HMD frame - never from `eyes[0].transform`.
            // Camera.CopyFrom sets an eye camera's transform to the SOURCE camera's pose (Unity docs), so a Tick
            // that ran before the host's first eye-pose application froze the panel at ArenaCamera's pose. Measured
            // on the 0.9.4 install (2026-10-18 19:57): "menu ui: panel frozen at world (74.29, 34.00, 43.75)" ==
            // anchorPosition + 2 m * ArenaCamera.forward (Unity yaw -90) while host.json already reported the HMD
            // frame (view row 2 = (0, 0, -1)). The panel therefore sat ~90 degrees off the player's view, and the
            // menu laser (composed with the game camera's rotation) pointed away from it.
            // D-178 still stands: the panel is placed ONCE and stays where it was placed.
            if (MenuScreenRig.TryGetLiveHeadPose(out var headLocal, out var headRotation))
            {
                var anchor = AnchorOrigin(__instance);
                var headPosition = new Vector3(headLocal.x + anchor[0], headLocal.y + anchor[1], headLocal.z + anchor[2]);
                Rig.Tick(headPosition, headRotation, distance?.Value ?? 1.5f, fov?.Value ?? 40f);
                if (Rig.ConsumeRecenterRequest())
                {
                    Rig.Recenter(headPosition, headRotation, distance?.Value ?? 1.5f, fov?.Value ?? 40f);
                }
            }
            else if (!noHeadPoseLogged)
            {
                // No located frame yet: defer placement rather than freeze a stale pose (the D-201 fault).
                noHeadPoseLogged = true;
                PatchRegistry.LogWarning("menu ui: no live eye sample yet; panel placement deferred (D-201)");
            }

            // D-201 (supersedes the D-196 note that used to sit here): the menu ray AND the menu panel must be built
            // in the same frame as the rendered view. Since D-195 the eye cameras carry only the HMD pose, so the
            // view's frame is the head/tracking frame; composing the ray with `source.transform.rotation`
            // (= ArenaCamera in the menu, measured Unity yaw -90 on 2026-10-18) turned it 90 degrees away from the
            // panel. D-196 restored that legacy rotation to "match the gameplay path", which only made the ray
            // consistent with the *then* frozen (also 90-degree-off) panel: both were wrong together, and the player
            // saw a panel and a laser that did not match where they were looking.
            // PointerHeadFrame=true (default) uses the identity/head frame, so the ray leaves the controller in the
            // direction the controller actually points. PointerHeadFrame=false reproduces the 0.9.4 behaviour for a
            // single in-headset A/B, no rebuild needed.
            Rig.UpdatePointers(AnchorOrigin(__instance), PointerAnchor(__instance));
        }
        catch (Exception e)
        {
            if (!warned)
            {
                warned = true;
                PatchRegistry.LogWarning("menu ui tick failed: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }

    private static void TearDown()
    {
        noHeadPoseLogged = false;
        if (!Rig.Built)
        {
            return;
        }
        Rig.Dispose();
    }

    private static float[] AnchorOrigin(object instance)
    {
        if (anchorPositionField?.GetValue(instance) is Vector3 position)
        {
            return new[] { position.x, position.y, position.z };
        }
        return new[] { 0f, 0f, 0f };
    }

    private static float[] AnchorRotation(object instance)
    {
        if (anchorRotationField?.GetValue(instance) is Quaternion rotation)
        {
            return new[] { rotation.x, rotation.y, rotation.z, rotation.w };
        }
        return new[] { 0f, 0f, 0f, 1f };
    }

    // D-201: the menu pointer's frame. headFrame=true (default) = the head/tracking frame the panel and the eye
    // cameras use; false = the legacy game-camera frame, kept for one in-headset A/B (config PointerHeadFrame).
    private static float[] PointerAnchor(object instance)
    {
        return PoseMathPolicy.MenuPointerAnchor(pointerHeadFrame?.Value != false, AnchorRotation(instance));
    }
}
