namespace VampireHuntersVR.Settings;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VampireHuntersVR.Render;

// D-171: the settings program's whole brain, kept free of WinForms so the fixtures in tests/SettingsTests.cs can drive it
// headlessly. It edits exactly four keys of BepInEx\config\vhvr-openxr-host.json (EyePreset/EyeWidth/EyeHeight/EyeScale),
// leaves every other key byte-identical, refuses a value the runtime would silently clamp (never a silent downgrade), and
// keeps a timestamped backup next to the file it rewrites. D-175 adds a fifth, optional key (uiScale) for the extras
// plugin's in-VR UI size; it is only written when the caller asks for it, so an untouched file stays byte-identical.
public sealed record PictureSize(string Preset, int BaseWidth, int BaseHeight, double Scale)
{
    public double EffectiveScale => RenderResolutionPolicy.EffectiveScale(Preset, Scale);
    public (int Width, int Height) EyeSize => RenderResolutionPolicy.EyeSize(BaseWidth, BaseHeight, Preset, Scale);
    public string Preview => RenderResolutionPolicy.Describe(BaseWidth, BaseHeight, Preset, Scale);
}

public static class HostConfigTool
{
    public const string FileName = "vhvr-openxr-host.json";
    public const long MaxBytes = 64 * 1024; // the same bound the plugin refuses to exceed
    public static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    // The program is dropped somewhere in the game folder (game root, BepInEx, BepInEx\config) or run from anywhere with
    // --config. It only ever looks for the two real install layouts and never guesses a Steam library path.
    //
    // D-179: when the tool sits next to install.bat inside the unzipped bundle, install.ps1 has left a
    // `vhvr-game-root.txt` there that records the game folder it installed into (the uninstaller already relies on
    // the same file). That recorded path is an explicit, user-chosen location - not a guess - so it is tried first.
    public static string? Locate(string startDirectory)
    {
        string? recorded = FromRecordedGameRoot(startDirectory);
        if (recorded != null) return recorded;

        DirectoryInfo? current = new(startDirectory);
        for (int level = 0; level < 4 && current != null; level++, current = current.Parent)
        {
            foreach (string candidate in new[]
            {
                Path.Combine(current.FullName, FileName),
                Path.Combine(current.FullName, "BepInEx", "config", FileName),
            })
            {
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    public const string GameRootMarker = "vhvr-game-root.txt";

    private static string? FromRecordedGameRoot(string startDirectory)
    {
        try
        {
            string marker = Path.Combine(startDirectory, GameRootMarker);
            if (!File.Exists(marker) || new FileInfo(marker).Length > 4096) return null;
            string root = File.ReadAllText(marker).Trim();
            if (root.Length == 0 || !Path.IsPathRooted(root)) return null;
            string candidate = Path.Combine(root, "BepInEx", "config", FileName);
            return File.Exists(candidate) ? candidate : null;
        }
        catch (Exception)
        {
            return null; // a broken marker must never stop the normal search
        }
    }

    public static JsonObject Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("config not found: " + path, path);
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("config exceeds 64 KiB; refusing to touch it");
        JsonNode? node = JsonNode.Parse(File.ReadAllText(path));
        if (node is not JsonObject root) throw new InvalidDataException("config is not a JSON object: " + path);
        return root;
    }

    public static PictureSize Read(JsonObject root)
    {
        string preset = StringOf(root, "EyePreset") ?? RenderResolutionPolicy.Normal;
        int width = IntOf(root, "EyeWidth", 1920);
        int height = IntOf(root, "EyeHeight", 1080);
        double scale = DoubleOf(root, "EyeScale", RenderResolutionPolicy.CustomDefaultScale);
        return new PictureSize(preset, width, height, scale);
    }

    // Returns null when the values are exactly what the runtime would use; otherwise the reason it would refuse or clamp.
    public static string? Validate(PictureSize size)
    {
        if (!RenderResolutionPolicy.IsValid(size.Preset)) return "invalid eye preset: low|normal|high|custom";
        if (size.BaseWidth < RenderResolutionPolicy.MinDimension || size.BaseWidth > RenderResolutionPolicy.MaxDimension ||
            size.BaseHeight < RenderResolutionPolicy.MinDimension || size.BaseHeight > RenderResolutionPolicy.MaxDimension)
            return $"base eye size must be {RenderResolutionPolicy.MinDimension}..{RenderResolutionPolicy.MaxDimension} per axis";
        if (!RenderResolutionPolicy.IsValidScale(size.Scale))
            return string.Create(CultureInfo.InvariantCulture, $"invalid custom scale: {RenderResolutionPolicy.MinScale}..{RenderResolutionPolicy.MaxScale}");
        (int width, int height) = size.EyeSize;
        if (width <= 0 || height <= 0) return "scaled eye size is not positive";
        return null;
    }

    public static void Apply(JsonObject root, PictureSize size)
    {
        string? error = Validate(size);
        if (error != null) throw new ArgumentException(error);
        root["EyePreset"] = size.Preset;
        root["EyeWidth"] = size.BaseWidth;
        root["EyeHeight"] = size.BaseHeight;
        root["EyeScale"] = Math.Round(size.Scale, 2);
    }

    // D-175: the in-VR UI scale the extras plugin reads from this same file (key "uiScale", 1.0 = shipped size).
    // Kept separate from PictureSize so the existing picture-size fixtures and their record shape stay valid.
    public const double MinUiScale = 0.5;
    public const double MaxUiScale = 3.0;
    public const double DefaultUiScale = 1.5;

    // Returns null when the file has no uiScale key, which means "keep the plugin's own default".
    public static double? ReadUiScale(JsonObject root)
    {
        if (root["uiScale"] is not JsonValue) return null;
        double value = DoubleOf(root, "uiScale", double.NaN);
        return double.IsNaN(value) ? null : value;
    }

    public static string? ValidateUiScale(double value)
    {
        if (!double.IsFinite(value) || value < MinUiScale || value > MaxUiScale)
            return string.Create(CultureInfo.InvariantCulture, $"invalid UI scale: {MinUiScale}..{MaxUiScale} (1.00 = shipped size)");
        return null;
    }

    public static void ApplyUiScale(JsonObject root, double value)
    {
        string? error = ValidateUiScale(value);
        if (error != null) throw new ArgumentException(error);
        root["uiScale"] = Math.Round(value, 2);
    }

    // D-187: the old "Graphics quality" dial (MinGraphicsQuality/MaxGraphicsQuality/Read/Validate/Apply) is gone.
    // The host no longer reads the key, the settings program no longer shows the row, and the install default
    // no longer writes it. A legacy "GraphicsQuality" key in an old vhvr-openxr-host.json is silently dropped by
    // System.Text.Json (unknown key), so existing files keep loading.

    // D-186: per-eye render-texture MSAA (key "EyeMsaa", the same file): 1 = off, 2 / 4 / 8 = that many samples.
    // 4 is what the host hard-coded before this key existed. The runtime refuses anything outside 1/2/4/8 (logs
    // and keeps the shipped 4), so the tool refuses such a value up front instead of writing it behind the
    // user's back. Absent = the shipped 4.
    public static readonly int[] EyeMsaaChoices = { 1, 2, 4, 8 };
    public const int DefaultEyeMsaa = 4;

    public static int? ReadEyeMsaa(JsonObject root)
    {
        if (root["EyeMsaa"] is not JsonValue) return null;
        int value = IntOf(root, "EyeMsaa", int.MinValue);
        return value == int.MinValue ? null : value;
    }

    public static string? ValidateEyeMsaa(int value) =>
        Array.IndexOf(EyeMsaaChoices, value) < 0
            ? "invalid eye MSAA: use 1, 2, 4 or 8 samples (1 = off)"
            : null;

    public static void ApplyEyeMsaa(JsonObject root, int value)
    {
        string? error = ValidateEyeMsaa(value);
        if (error != null) throw new ArgumentException(error);
        root["EyeMsaa"] = value;
    }

    // D-104-iso-promotion: anisotropic filtering on the eye texture sampling (D-104). The host already reads
    // this key (OpenXrHost.cs:879); it was only ever set by the test harness (-Anisotropic on headset-handaim.ps1)
    // and by install.ps1's first-write default. 0.7.1 promotes it to the user-facing settings tool so a player
    // does not have to edit JSON by hand. Default OFF (matches D-104's opt-in policy and 0.7's install.ps1 value).
    // The key is a plain bool; System.Text.Json rejects non-bool values itself, so no Validate* method is needed.
    public const bool DefaultAnisotropic = false;

    public static bool? ReadAnisotropic(JsonObject root)
    {
        if (root["Anisotropic"] is not JsonValue) return null;
        return BoolOf(root, "Anisotropic", DefaultAnisotropic);
    }

    public static void ApplyAnisotropic(JsonObject root, bool value)
    {
        root["Anisotropic"] = value;
    }

    // D-186-FovScale: per-axis render-FOV scales applied to the four angles OpenXR reports from
    // xrLocateViews before both the managed-side projection and the compositor submit. 1.0 = pass-through
    // (the shipped value, no over-render reduction). Lower values narrow the rendered FOV so the GPU draws
    // fewer pixels (the OpenXR runtime usually reports a wider FOV than the lens displays; the extra is
    // the lens-distortion / barrel margin and is normally invisible).
    //
    // FovScaleX scales the horizontal pair (angleLeft, angleRight). FovScaleY scales the vertical pair
    // (angleUp, angleDown). Either axis can be narrowed independently. The host refuses values outside
    // 0.50-1.00 and keeps the previous value, so a bad value cannot start a sitting narrower than the
    // user asked for. The form offers 0.50-1.00 in 0.05 steps (11 values, including 1.00 = shipped).
    // An absent X or Y key means the user has not opened the form yet and the host defaults to 1.0 on
    // that axis.
    //
    // Legacy migration: a pre-Stage-3.5 file with the single "FovScale" key (no X/Y suffix) is read as
    // FovScaleX = that value, FovScaleY = 1.0 (a horizontal-only cut). The legacy key is removed on the
    // next save so a migrated file ends up with only FovScaleX + FovScaleY, and a 0.7.1 file with neither
    // key (no FovScale, no FovScaleX, no FovScaleY) reads both as 1.0.
    public const float DefaultFovScale = 1.0f;
    public static readonly float[] FovScaleChoices =
    {
        0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.75f, 0.80f, 0.85f, 0.90f, 0.95f, 1.00f
    };

    // Centralised float-from-JsonValue read. The X and Y branches go through the same float/double/long/
    // string fallback ladder; the legacy single FovScale key goes through the same path so a pre-Stage-3.5
    // file reads identically to a fresh FovScaleX write.
    private static float? ReadFovScaleKey(JsonObject root, string name)
    {
        if (root[name] is not JsonValue value) return null;
        // The key is written by ApplyFovScaleX/Y as a float, and System.Text.Json's JsonValue preserves the
        // exact CLR type (System.Single in this case). TryGetValue<double>/<long>/<string>/<bool> are
        // false for a float-stored value, so the float branch is the primary read path. An older file
        // hand-edited with a stringified number ("0.85") falls through to the string path.
        if (value.TryGetValue(out float single)) return single;
        if (value.TryGetValue(out double number)) return (float)number;
        if (value.TryGetValue(out long longNumber)) return longNumber;
        if (value.TryGetValue(out string? text) && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)) return (float)parsed;
        return null;
    }

    public static float? ReadFovScaleX(JsonObject root)
    {
        // Legacy migration: a pre-Stage-3.5 file with the single FovScale key (no X/Y suffix) reads as
        // FovScaleX = that value when no explicit FovScaleX is present. A 0.7.1 file with no fov key at
        // all returns null and falls through to the form's 1.0 default.
        return ReadFovScaleKey(root, "FovScaleX") ?? ReadFovScaleKey(root, "FovScale");
    }

    public static float? ReadFovScaleY(JsonObject root)
    {
        // Legacy migration: when only the single FovScale key is present, Y is the shipped pass-through
        // (1.0) because a single-axis scale always narrows the horizontal frustum. Y stays at the form's
        // 1.0 default when neither the FovScaleY key nor the legacy FovScale key is present.
        float? y = ReadFovScaleKey(root, "FovScaleY");
        if (y != null) return y;
        return ReadFovScaleKey(root, "FovScale") != null ? DefaultFovScale : null;
    }

    public static string? ValidateFovScale(float value) =>
        Array.IndexOf(FovScaleChoices, value) < 0
            ? "invalid FOV scale: use 0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90, 0.95 or 1.00 (1.00 = no over-render reduction)"
            : null;

    public static void ApplyFovScaleX(JsonObject root, float value)
    {
        string? error = ValidateFovScale(value);
        if (error != null) throw new ArgumentException(error);
        root["FovScaleX"] = value;
        // Any save that touches the X axis also retires the legacy single key (Stage 3.5 onwards), so a
        // migrated file ends up with FovScaleX + FovScaleY only.
        root.Remove("FovScale");
    }

    public static void ApplyFovScaleY(JsonObject root, float value)
    {
        string? error = ValidateFovScale(value);
        if (error != null) throw new ArgumentException(error);
        root["FovScaleY"] = value;
        root.Remove("FovScale");
    }

    public static string Save(string path, JsonObject root, DateTime? stamp = null)
    {
        string backup = path + "." + (stamp ?? DateTime.UtcNow).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + ".bak";
        if (File.Exists(path)) File.Copy(path, backup, overwrite: true);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, root.ToJsonString(Format) + Environment.NewLine);
        File.Move(temporary, path, overwrite: true);
        return backup;
    }

    private static string? StringOf(JsonObject root, string name) => root[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    private static int IntOf(JsonObject root, string name, int fallback) =>
        root[name] is JsonValue value && value.TryGetValue(out int number) ? number :
        root[name] is JsonValue other && other.TryGetValue(out string? text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    private static double DoubleOf(JsonObject root, string name, double fallback) =>
        root[name] is JsonValue value && value.TryGetValue(out double number) ? number :
        root[name] is JsonValue other && other.TryGetValue(out string? text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
    private static bool BoolOf(JsonObject root, string name, bool fallback) =>
        root[name] is JsonValue value && value.TryGetValue(out bool flag) ? flag : fallback;
}

