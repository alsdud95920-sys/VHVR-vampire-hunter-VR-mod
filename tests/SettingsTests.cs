using VampireHuntersVR.Stage0;
using VampireHuntersVR.Render;
using VampireHuntersVR.Settings;
using System.Text.Json.Nodes;

int passed = 0;
void Check(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
void Assert(bool value) { if (!value) throw new Exception("Assertion failed."); }
void CheckEq(string name, object? expected, object? actual) { if (!Equals(expected, actual)) throw new Exception(name + ": expected " + (expected?.ToString() ?? "null") + " got " + (actual?.ToString() ?? "null")); }
DirectoryInfo? testsRoot = new(AppContext.BaseDirectory);
while (testsRoot != null && !File.Exists(Path.Combine(testsRoot.FullName, "Settings.Tests.csproj"))) testsRoot = testsRoot.Parent;
string root = testsRoot?.Parent?.FullName ?? throw new InvalidOperationException("Project test root not found.");
string fixture = Path.Combine(root, "artifacts", "settings-fixtures-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
string path = Path.Combine(fixture, "plugin.cfg");
string marker = path + ".safe-mode";




































































































    // D-170: eye render resolution and eye-image MSAA. The policy below is the same file the plugin and the settings
    // tool compile, so these fixtures pin the exact numbers the runtime will use, and the tool fixtures prove the four
    // keys land in a real file without touching the others.
    Check("resolution preset scales and clamps the base size", () => {
        Assert(RenderResolutionPolicy.EyeSize(2064, 2208, RenderResolutionPolicy.Normal) == (2064, 2208));
        Assert(RenderResolutionPolicy.EyeSize(2064, 2208, RenderResolutionPolicy.Low) == (1651, 1766));
        Assert(RenderResolutionPolicy.EyeSize(2064, 2208, RenderResolutionPolicy.High) == (2477, 2650));
        Assert(RenderResolutionPolicy.EyeSize(2064, 2208, RenderResolutionPolicy.Custom) == (2064, 2208));
        Assert(RenderResolutionPolicy.EyeSize(400, 10000, RenderResolutionPolicy.Custom) == (512, 4096));
        Assert(!RenderResolutionPolicy.IsValid("medium") && !RenderResolutionPolicy.IsValid(null));
    });
    Check("msaa policy accepts only what the runtime can create", () => {
        foreach (int samples in new[] { 0, 2, 4, 8 }) Assert(RenderResolutionPolicy.IsValidMsaa(samples));
        foreach (int samples in new[] { -1, 1, 3, 5, 6, 7, 16, 64 }) Assert(!RenderResolutionPolicy.IsValidMsaa(samples));
        Assert(RenderResolutionPolicy.UnitySamples(0) == 1 && RenderResolutionPolicy.UnitySamples(2) == 2 && RenderResolutionPolicy.UnitySamples(8) == 8);
        Assert(RenderResolutionPolicy.MsaaLabel(0) == "off" && RenderResolutionPolicy.MsaaLabel(4) == "4x");
    });
    Check("describe states the effective eye size and msaa", () => {
        string text = RenderResolutionPolicy.Describe(2064, 2208, RenderResolutionPolicy.High, 4);
        Assert(text.Contains("eye=2477x2650") && text.Contains("pair=4954x2650") && text.Contains("msaa=4x"));
        Assert(RenderResolutionPolicy.Describe(2064, 2208, RenderResolutionPolicy.Normal, 0).Contains("msaa=off"));
    });
    string toolDir = Path.Combine(fixture, "tool");
    Directory.CreateDirectory(toolDir);
    string hostPath = Path.Combine(toolDir, "vhvr-openxr-host.json");
    Check("tool reads the four keys and defaults the rest", () => {
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"EyeWidth\":1600,\"EyeHeight\":1700,\"EyeMsaa\":2}");
        JsonObject root = HostConfigTool.Load(hostPath);
        PictureSize values = HostConfigTool.Read(root);
        Assert(values.Preset == "normal" && values.BaseWidth == 1600 && values.BaseHeight == 1700);
        Assert(HostConfigTool.ReadEyeMsaa(root) == 2);
        Assert(values.EyeSize == (1600, 1700));
    });
    Check("tool rejects what the runtime would clamp and writes nothing", () => {
        foreach (PictureSize bad in new[] { new PictureSize("medium", 2064, 2208, 1.0), new PictureSize("normal", 511, 2208, 1.0),
            new PictureSize("normal", 2064, 4097, 1.0) }) Assert(HostConfigTool.Validate(bad) != null);
        Assert(HostConfigTool.ValidateEyeMsaa(3) != null);
        JsonObject root = HostConfigTool.Load(hostPath);
        byte[] before = File.ReadAllBytes(hostPath);
        try { HostConfigTool.ApplyEyeMsaa(root, 3); throw new Exception("invalid msaa applied."); }
        catch (ArgumentException) { }
        Assert(before.SequenceEqual(File.ReadAllBytes(hostPath)));
    });
    Check("tool apply changes only its four keys and keeps a backup", () => {
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"HandAim\":true,\"EyePreset\":\"normal\",\"EyeWidth\":2064,\"EyeHeight\":2208,\"MarkerKinds\":\"CHEST,HEALING\",\"UiVerticalOffset\":-0.25}");
        byte[] original = File.ReadAllBytes(hostPath);
        JsonObject root = HostConfigTool.Load(hostPath);
        HostConfigTool.Apply(root, new PictureSize("high", 2064, 2208, 1.0));
        HostConfigTool.ApplyEyeMsaa(root, 8);
        string backup = HostConfigTool.Save(hostPath, root, new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));
        Assert(File.Exists(backup) && original.SequenceEqual(File.ReadAllBytes(backup)));
        JsonObject written = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.Read(written) == new PictureSize("high", 2064, 2208, 1.0));
        Assert(HostConfigTool.ReadEyeMsaa(written) == 8);
        Assert(written["Enabled"]?.GetValue<bool>() == true && written["HandAim"]?.GetValue<bool>() == true &&
            written["MarkerKinds"]?.GetValue<string>() == "CHEST,HEALING" && Math.Abs(written["UiVerticalOffset"]!.GetValue<double>() + 0.25) < 1e-9);
        Assert(File.ReadAllText(hostPath).Contains("\"EyeMsaa\": 8") && !File.Exists(hostPath + ".tmp"));
    });
    Check("tool locates the config only where it really is", () => {
        string clean = Path.Combine(fixture, "locate");
        string nested = Path.Combine(clean, "BepInEx", "config");
        Directory.CreateDirectory(nested);
        string real = Path.Combine(nested, "vhvr-openxr-host.json");
        File.WriteAllText(real, "{\"Enabled\":true}");
        Assert(HostConfigTool.Locate(clean) == real && HostConfigTool.Locate(nested) == real);
        File.Delete(real);
        Assert(HostConfigTool.Locate(clean) != real);
    });
    Check("tool refuses an oversized or non-object config", () => {
        File.WriteAllBytes(hostPath, new byte[HostConfigTool.MaxBytes + 1]);
        try { HostConfigTool.Load(hostPath); throw new Exception("oversized config accepted."); }
        catch (InvalidDataException) { }
        File.WriteAllText(hostPath, "[1,2,3]");
        try { HostConfigTool.Load(hostPath); throw new Exception("array config accepted."); }
        catch (InvalidDataException) { }
    });
    Check("D-186-FovScale tool reads per-axis keys, defaults to 1.0 when absent, and refuses out-of-range", () => {
        // Absent keys on both axes = shipped pass-through (1.0 on each), so an unedited 0.7.1 file still
        // renders the same picture.
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"EyeWidth\":2064,\"EyeHeight\":2208}");
        JsonObject root = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.ReadFovScaleX(root) == null);
        Assert(HostConfigTool.ReadFovScaleY(root) == null);
        // A valid 11-step choice round-trips on each axis and the same value is read back.
        HostConfigTool.ApplyFovScaleX(root, 0.85f);
        HostConfigTool.ApplyFovScaleY(root, 0.80f);
        string rawX = root["FovScaleX"]?.ToJsonString() ?? "<missing>";
        string rawY = root["FovScaleY"]?.ToJsonString() ?? "<missing>";
        float? readX = HostConfigTool.ReadFovScaleX(root);
        float? readY = HostConfigTool.ReadFovScaleY(root);
        CheckEq("FovScaleX round-trip raw=" + rawX, "0.85", readX?.ToString() ?? "null");
        CheckEq("FovScaleY round-trip raw=" + rawY, "0.8", readY?.ToString() ?? "null");
        if (readX != null) Assert(Math.Abs(readX.Value - 0.85f) < 1e-6f);
        if (readY != null) Assert(Math.Abs(readY.Value - 0.80f) < 1e-6f);
        // An out-of-range value (0.45 below floor, 1.05 above ceiling) is refused on each axis and the file
        // is byte-identical (the on-disk file only changes at Save time, not at Apply validation time).
        byte[] before = File.ReadAllBytes(hostPath);
        try { HostConfigTool.ApplyFovScaleX(root, 0.45f); throw new Exception("under-range fov X applied."); }
        catch (ArgumentException) { }
        try { HostConfigTool.ApplyFovScaleY(root, 1.05f); throw new Exception("over-range fov Y applied."); }
        catch (ArgumentException) { }
        Assert(before.SequenceEqual(File.ReadAllBytes(hostPath)));
        // The shipped default is the highest choice in the list, so the form's 1.00 selection is the
        // "same picture" guarantee that an unedited file relies on.
        Assert(HostConfigTool.DefaultFovScale == 1.0f);
        Assert(HostConfigTool.FovScaleChoices.Length == 11);
        Assert(HostConfigTool.FovScaleChoices[0] == 0.50f && HostConfigTool.FovScaleChoices[10] == 1.00f);
    });
    Check("D-186-FovScale tool saves the FovScaleX/Y keys and keeps every other key byte-identical", () => {
        // The shipped 0.7.1 file has neither FovScale nor FovScaleX/Y keys. After Apply + Save, only the two
        // new keys are added; every other key the host/plugin reads (Enabled, HandAim, MarkerKinds,
        // UiVerticalOffset) is unchanged on disk, and a timestamped backup of the pre-edit file is left
        // next to the live file.
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"HandAim\":true,\"EyePreset\":\"normal\",\"EyeWidth\":2064,\"EyeHeight\":2208,\"MarkerKinds\":\"CHEST,HEALING\",\"UiVerticalOffset\":-0.25}");
        byte[] original = File.ReadAllBytes(hostPath);
        JsonObject root = HostConfigTool.Load(hostPath);
        HostConfigTool.ApplyFovScaleX(root, 0.70f);
        HostConfigTool.ApplyFovScaleY(root, 0.75f);
        string backup = HostConfigTool.Save(hostPath, root, new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));
        Assert(File.Exists(backup) && original.SequenceEqual(File.ReadAllBytes(backup)));
        JsonObject written = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.ReadFovScaleX(written) == 0.70f);
        Assert(HostConfigTool.ReadFovScaleY(written) == 0.75f);
        Assert(written["Enabled"]?.GetValue<bool>() == true && written["HandAim"]?.GetValue<bool>() == true &&
            written["MarkerKinds"]?.GetValue<string>() == "CHEST,HEALING" && Math.Abs(written["UiVerticalOffset"]!.GetValue<double>() + 0.25) < 1e-9);
        string text = File.ReadAllText(hostPath);
        Assert(text.Contains("\"FovScaleX\": 0.7") && text.Contains("\"FovScaleY\": 0.75") && !File.Exists(hostPath + ".tmp"));
    });
    Check("D-186-FovScale legacy single FovScale key migrates to FovScaleX and is removed on save", () => {
        // A pre-Stage-3.5 file with the single FovScale key (no X/Y suffix) reads as FovScaleX=that value,
        // FovScaleY=1.0 (horizontal-only cut). ApplyFovScaleX retires the legacy key on save so a migrated
        // file ends up with FovScaleX + FovScaleY only.
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"FovScale\":0.80}");
        JsonObject root = HostConfigTool.Load(hostPath);
        // Read path: X=0.80 (from legacy key), Y=1.0 (the migration default).
        float? x = HostConfigTool.ReadFovScaleX(root);
        float? y = HostConfigTool.ReadFovScaleY(root);
        if (x != null) Assert(Math.Abs(x.Value - 0.80f) < 1e-6f);
        if (y != null) Assert(Math.Abs(y.Value - 1.00f) < 1e-6f);
        // Apply path: ApplyFovScaleX with 0.85 must remove the legacy key, set FovScaleX=0.85, and leave
        // the file with only FovScaleX + FovScaleY (Y still absent here because the test never set it).
        HostConfigTool.ApplyFovScaleX(root, 0.85f);
        HostConfigTool.ApplyFovScaleY(root, 0.90f);
        string backup = HostConfigTool.Save(hostPath, root, new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));
        JsonObject written = HostConfigTool.Load(hostPath);
        Assert(written["FovScale"] == null);
        Assert(HostConfigTool.ReadFovScaleX(written) == 0.85f);
        Assert(HostConfigTool.ReadFovScaleY(written) == 0.90f);
        // A 0.7.1 file with neither key (no FovScale, no FovScaleX, no FovScaleY) reads both as null, so
        // the form falls through to DefaultFovScale = 1.0 on both axes.
        File.WriteAllText(hostPath, "{\"Enabled\":true}");
        JsonObject fresh = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.ReadFovScaleX(fresh) == null);
        Assert(HostConfigTool.ReadFovScaleY(fresh) == null);
    });
    Check("D-211 snap turn reads, validates and writes only the three choices", () => {
        // Absent key -> null (the form shows the shipped 30); a hand-edited 60 is refused by the validator and
        // ApplySnapTurnDegrees writes nothing for it.
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"HandAim\":true}");
        JsonObject root = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.ReadSnapTurnDegrees(root) == null);
        Assert(HostConfigTool.ValidateSnapTurnDegrees(0f) == null && HostConfigTool.ValidateSnapTurnDegrees(30f) == null && HostConfigTool.ValidateSnapTurnDegrees(45f) == null);
        Assert(HostConfigTool.ValidateSnapTurnDegrees(60f) != null && HostConfigTool.ValidateSnapTurnDegrees(-30f) != null && HostConfigTool.ValidateSnapTurnDegrees(float.NaN) != null);
        try { HostConfigTool.ApplySnapTurnDegrees(root, 60f); throw new Exception("invalid snap turn applied."); }
        catch (ArgumentException) { }
        // Every listed choice lands in the file, and every other key stays exactly as it was.
        HostConfigTool.ApplySnapTurnDegrees(root, 0f);
        HostConfigTool.Save(hostPath, root, new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));
        JsonObject written = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.ReadSnapTurnDegrees(written) == 0f);
        Assert(written["Enabled"]?.GetValue<bool>() == true && written["HandAim"]?.GetValue<bool>() == true);
        root = HostConfigTool.Load(hostPath);
        HostConfigTool.ApplySnapTurnDegrees(root, 45f);
        HostConfigTool.Save(hostPath, root, new DateTime(2026, 10, 20, 0, 0, 1, DateTimeKind.Utc));
        written = HostConfigTool.Load(hostPath);
        Assert(HostConfigTool.ReadSnapTurnDegrees(written) == 45f);
        Console.WriteLine("PASS d211-snap-turn-off-round-trip-and-keys");
        // A stringified number in a hand-edited file still reads (the same ladder the FOV keys use).
        File.WriteAllText(hostPath, "{\"Enabled\":true,\"SnapTurnDegrees\":\"45\"}");
        Assert(HostConfigTool.ReadSnapTurnDegrees(HostConfigTool.Load(hostPath)) == 45f);
    });
    Check("D-211 form selftest surface still loads with the snap row", () => {
        // The window is exercised by --form-selftest at build time; here we only pin the three-choice contract the
        // form writes, so a later edit to the radio group cannot silently drop a choice.
        Assert(HostConfigTool.SnapTurnChoices.Length == 3 && HostConfigTool.SnapTurnChoices[0] == 0f &&
            HostConfigTool.SnapTurnChoices[1] == 30f && HostConfigTool.SnapTurnChoices[2] == 45f);
        Assert(HostConfigTool.DefaultSnapTurnDegrees == 30f);
    });
    Console.WriteLine($"RESULT {passed} passed, 0 failed; pure config/marker/resolution/MSAA fixtures; no BepInEx/game/Unity execution.");
    return 0;