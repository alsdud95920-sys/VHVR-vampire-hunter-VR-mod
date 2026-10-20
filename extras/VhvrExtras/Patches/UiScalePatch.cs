using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace VhvrExtras;

// Requested behaviour 3: make the in-VR UI 1.5x and let the settings tool choose the multiplier.
//
// Target: VampireHuntersVR.Interaction.HandAimDriver.UiScan(Camera source) - the shipped build converts every
// root overlay canvas to world space once and sizes it by a target world width (UiCanvas.Width, default 1.9 m,
// 1.4 m level-up, 1.6 m head-locked). Scaling that width scales the whole HUD, so this postfix multiplies the
// width of each newly converted canvas and leaves the placement/pointer maths untouched.
internal static class UiScalePatch
{
    private static ConfigEntry<float>? scale;
    private static ConfigEntry<float>? pausedScale;
    private static FieldInfo? canvasesField;
    private static FieldInfo? widthField;
    private static readonly HashSet<int> Scaled = new();

    // Must equal UiWidth in src\interaction\HandUi.cs (the ESC / pause menu width in metres, user decision D-181).
    private const float PausedMenuWidthMeters = 3.0f;

    internal static void Apply(PatchRegistry registry, ExtrasConfig config)
    {
        var target = registry.Find("VampireHuntersVR.Interaction.HandAimDriver", "UiScan", "ui.scale");
        if (target?.DeclaringType == null)
        {
            return;
        }

        canvasesField = AccessTools.Field(target.DeclaringType, "uiCanvases");
        if (canvasesField == null)
        {
            registry.Defer("ui.scale", "uiCanvases field not found");
            return;
        }

        scale = config.UiScale;
        pausedScale = config.UiScalePaused;
        var fromJson = NativeSettingsJson.TryReadUiScale();
        if (fromJson is { } value)
        {
            scale.Value = value;
            PatchRegistry.LogInfo("ui.scale from vhvr-openxr-host.json = " + value.ToString("0.###"));
        }

        registry.Hook(target, "HandAimDriver.UiScan->ui-scale",
            postfix: new HarmonyMethod(typeof(UiScalePatch), nameof(Postfix)));
    }

    private static void Postfix(object __instance)
    {
        var multiplier = scale?.Value ?? 1f;
        if (!UiScalePolicy.IsUsable(multiplier) || Math.Abs(multiplier - 1f) < 0.001f)
        {
            return;
        }

        if (canvasesField?.GetValue(__instance) is not IDictionary canvases)
        {
            return;
        }

        foreach (DictionaryEntry entry in canvases)
        {
            var canvas = entry.Value;
            if (canvas == null)
            {
                continue;
            }

            var id = canvas.GetHashCode();
            if (Scaled.Contains(id))
            {
                continue;
            }

            widthField ??= AccessTools.Field(canvas.GetType(), "Width");
            if (widthField?.GetValue(canvas) is not float width)
            {
                continue;
            }

            // D-181: the paused ESC / pause menu uses the shipped default width, which the user fixed at an absolute
            // 3.0 m (UiWidth in HandUi.cs). D-192: it now has its own category multiplier (config [Ui] ScalePaused,
            // default 1.0 = unchanged; the user's recommendation is 2.0) so just the menu can be made bigger without
            // inflating the HUD/level-up canvases.
            if (Math.Abs(width - PausedMenuWidthMeters) < 0.001f)
            {
                var pausedMultiplier = pausedScale?.Value ?? 1f;
                if (UiScalePolicy.IsUsable(pausedMultiplier) && Math.Abs(pausedMultiplier - 1f) >= 0.001f)
                {
                    widthField.SetValue(canvas, UiScalePolicy.ScaleWidth(width, pausedMultiplier));
                }
                Scaled.Add(id);
                continue;
            }

            widthField.SetValue(canvas, UiScalePolicy.ScaleWidth(width, multiplier));
            Scaled.Add(id);
        }
    }

    internal static void Reset()
    {
        Scaled.Clear();
    }
}
