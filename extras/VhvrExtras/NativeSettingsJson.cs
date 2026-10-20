using System.Text.Json;
using BepInEx;

namespace VhvrExtras;

// The settings tool (src\settings\VhvrSettings) already owns reading/writing vhvr-openxr-host.json with a
// backup and an atomic replace, so new knobs live in that file instead of a second config file. The BepInEx cfg
// remains the default, and an unusable value is rejected here (the tool is responsible for validating input),
// never silently clamped.
internal static class NativeSettingsJson
{
    private const string FileName = "vhvr-openxr-host.json";

    internal static IEnumerable<string> CandidatePaths()
    {
        var root = Paths.GameRootPath;
        yield return Path.Combine(root, FileName);
        yield return Path.Combine(root, "BepInEx", "config", FileName);
    }

    internal static float? TryReadUiScale()
    {
        foreach (var path in CandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("uiScale", out var value) ||
                    value.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var scale = value.GetSingle();
                return UiScalePolicy.IsUsable(scale) ? scale : null;
            }
            catch (Exception)
            {
                // A malformed json is the tool's problem to report; the plugin keeps the cfg value.
            }
        }
        return null;
    }
}
