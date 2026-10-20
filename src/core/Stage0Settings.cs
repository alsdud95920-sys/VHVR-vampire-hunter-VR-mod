using System.Security.Cryptography;
using System.Text;

namespace VampireHuntersVR.Stage0;

public sealed record Stage0Settings(bool DiagnosticsEnabled, bool SafeMode, bool MarkerPresent, string[] Warnings)
{
    public bool EffectiveSafeMode => SafeMode || MarkerPresent;
}

// Supports only this plugin's two BepInEx bool entries; not a general TOML parser.
public static class Stage0SettingsParser
{
    public const int MaxBytes = 64 * 1024;

    public static Stage0Settings Parse(string text, bool markerPresent)
    {
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        string section = "";
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1].Trim(); continue; }
            int equals = line.IndexOf('=');
            if (equals <= 0) { warnings.Add("Malformed config line ignored."); continue; }
            string key = line[..equals].Trim();
            bool known = (section == "Features" && key == "DiagnosticsEnabled") || (section == "Safety" && key == "SafeMode");
            if (!known) continue;
            if (!seen.Add(key)) { values[key] = true; warnings.Add($"Duplicate {key}; default=true."); continue; }
            if (!bool.TryParse(line[(equals + 1)..].Trim(), out bool value))
            {
                values[key] = true; warnings.Add($"Invalid {key}; default=true.");
            }
            else values[key] = value;
        }
        foreach (string key in new[] { "DiagnosticsEnabled", "SafeMode" })
            if (!seen.Contains(key)) { values[key] = true; warnings.Add($"Missing {key}; default=true."); }
        return new(values["DiagnosticsEnabled"], values["SafeMode"], markerPresent, warnings.ToArray());
    }
}

public sealed record Stage0SettingsRead(Stage0Settings Settings, string Fingerprint);

public static class Stage0SettingsFile
{
    public static Stage0SettingsRead Read(string path, string markerPath)
    {
        bool marker = File.Exists(markerPath) || Directory.Exists(markerPath);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > Stage0SettingsParser.MaxBytes) throw new InvalidDataException("Config exceeds 64 KiB.");
            var bytes = new byte[(int)stream.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw new EndOfStreamException("Config changed while reading.");
                offset += read;
            }
            string text = new UTF8Encoding(false, true).GetString(bytes);
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            return new(Stage0SettingsParser.Parse(text, marker), hash + ":marker=" + marker);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or DecoderFallbackException)
        {
            // A failed read cannot keep a prior false safe-mode value active.
            return new(new(true, true, marker, new[] { "Config unavailable/invalid; defaults=true: " + e.Message }),
                "unavailable:" + e.GetType().Name + ":marker=" + marker);
        }
    }
}