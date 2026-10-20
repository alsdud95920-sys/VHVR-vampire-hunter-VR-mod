namespace VampireHuntersVR.Settings;

using System.Globalization;
using System.Text.Json.Nodes;
using VampireHuntersVR.Render;

// D-171: the same executable is both the window a user double-clicks and a headless command line, so every claim it
// makes about a config can be checked from a script without opening a window:
//   VhvrSettings.exe                                   open the window (auto-locates BepInEx\config\vhvr-openxr-host.json)
//   VhvrSettings.exe --config <path> --print           show the picture size the config asks for
//   VhvrSettings.exe --config <path> --validate        exit 0 = the runtime would accept it, 1 = it would refuse
//   VhvrSettings.exe --config <path> --preset high --apply    write (with a timestamped backup)
//   VhvrSettings.exe --config <path> --preset custom --scale 1.5 --apply
//   VhvrSettings.exe --aniso | --no-aniso --apply       opt in/out of 16x anisotropic filtering on eye textures
//   VhvrSettings.exe --selftest [dir]                  round-trip a scratch config and prove the keys land
//   VhvrSettings.exe --form-selftest [config]          open the real window for 400 ms and report what it loaded
internal static class Program
{
    private const int Ok = 0, Usage = 1, Failed = 2;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SettingsForm(null));
                return Ok;
            }
            return CommandLine(args);
        }
        catch (Exception e)
        {
            Write("ERROR " + e.GetType().Name + ": " + e.Message);
            return Failed;
        }
    }

    private static int CommandLine(string[] args)
    {
        string? path = null, preset = null, scratch = null;
        double? scale = null;
        double? uiScale = null;
        int? eyeMsaa = null;
        bool? aniso = null;
        double? fovScaleX = null;
        double? fovScaleY = null;
        bool print = false, apply = false, validate = false, help = false, selfTest = false, formSelfTest = false;
        for (int index = 0; index < args.Length; index++)
        {
            string flag = args[index];
            string Next()
            {
                if (index + 1 >= args.Length) throw new ArgumentException("missing value for " + flag);
                return args[++index];
            }
            switch (flag)
            {
                case "--config": path = Next(); break;
                case "--preset": preset = Next(); break;
                case "--scale": scale = ParseDouble(Next(), flag); break;
                case "--ui-scale": uiScale = ParseDouble(Next(), flag); break;
                case "--msaa": eyeMsaa = (int)ParseDouble(Next(), flag); break;
                case "--aniso": aniso = true; break;
                case "--no-aniso": aniso = false; break;
                // D-186-FovScale: per-axis render-FOV scales (1.0 = pass-through). Refused outside 0.50-1.00
                // (in 0.05 steps); a bad value is reported as INVALID and the file is not written. Either
                // axis can be set independently; omitting one leaves the file's current value on that axis.
                case "--fov-scale-x": fovScaleX = ParseDouble(Next(), flag); break;
                case "--fov-scale-y": fovScaleY = ParseDouble(Next(), flag); break;
                case "--print": print = true; break;
                case "--apply": apply = true; break;
                case "--validate": validate = true; break;
                case "--selftest": selfTest = true; if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)) scratch = Next(); break;
                case "--form-selftest": formSelfTest = true; if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)) scratch = Next(); break;
                case "--help": case "-h": help = true; break;
                default: throw new ArgumentException("unknown argument: " + flag);
            }
        }
        if (help)
        {
            Write("VhvrSettings: picture size (EyePreset/EyeWidth/EyeHeight/EyeScale) for vhvr-openxr-host.json.");
            Write("  --config <path>   config file (default: auto-locate BepInEx\\config\\vhvr-openxr-host.json)");
            Write("  --print | --validate | --apply | --selftest [dir] | --form-selftest [config]");
            Write("  --preset low|normal|high|custom   (0.8 / 1.0 / 1.2 of the base; custom uses --scale)");
            Write("  --scale 0.50..2.00                (custom only; --apply writes a timestamped .bak next to the file)");
            Write("  --ui-scale 0.50..3.00             (in-VR UI size for the extras plugin; 1.00 = shipped, default 1.50)");
            Write("  --msaa 1|2|4|8                    (per-eye MSAA samples for the eye textures; 1 = off, 4 = the shipped default)");
            Write("  --aniso | --no-aniso             (16x anisotropic filtering on eye textures; off = shipped)");
            Write("  --fov-scale-x 0.50..1.00 in 0.05  (render-FOV scale X / horizontal; 1.00 = shipped pass-through)");
            Write("  --fov-scale-y 0.50..1.00 in 0.05  (render-FOV scale Y / vertical;   1.00 = shipped pass-through)");
            return Ok;
        }
        if (apply && (print || validate)) throw new ArgumentException("--apply cannot be combined with --print/--validate");
        if (selfTest) return SelfTest(scratch ?? Path.Combine(Path.GetTempPath(), "vhvr-settings-selftest"));
        if (formSelfTest) return FormSelfTest(path ?? HostConfigTool.Locate(AppContext.BaseDirectory) ?? scratch);

        path ??= HostConfigTool.Locate(AppContext.BaseDirectory);
        if (path == null) { Write("ERROR no " + HostConfigTool.FileName + " found near " + AppContext.BaseDirectory + "; pass --config <path>"); return Usage; }
        JsonObject root = HostConfigTool.Load(path);
        PictureSize current = HostConfigTool.Read(root);
        PictureSize wanted = new(preset ?? current.Preset, current.BaseWidth, current.BaseHeight, scale ?? current.Scale);
        string? error = HostConfigTool.Validate(wanted);
        if (error != null) { Write("INVALID " + path + ": " + error); return Usage; }
        if (uiScale is double requestedUiScale)
        {
            string? uiError = HostConfigTool.ValidateUiScale(requestedUiScale);
            if (uiError != null) { Write("INVALID " + path + ": " + uiError); return Usage; }
        }
        if (eyeMsaa is int requestedMsaa)
        {
            string? msaaError = HostConfigTool.ValidateEyeMsaa(requestedMsaa);
            if (msaaError != null) { Write("INVALID " + path + ": " + msaaError); return Usage; }
        }
        if (fovScaleX is double requestedFovX)
        {
            string? fovError = HostConfigTool.ValidateFovScale((float)requestedFovX);
            if (fovError != null) { Write("INVALID " + path + ": " + fovError); return Usage; }
        }
        if (fovScaleY is double requestedFovY)
        {
            string? fovError = HostConfigTool.ValidateFovScale((float)requestedFovY);
            if (fovError != null) { Write("INVALID " + path + ": " + fovError); return Usage; }
        }
        Write("CONFIG " + path);
        Write("CURRENT " + current.Preview);
        double? currentUiScale = HostConfigTool.ReadUiScale(root);
        Write("CURRENT_UISCALE " + (currentUiScale is double cus ? cus.ToString("0.00", CultureInfo.InvariantCulture) : "(absent: plugin default)"));
        int? currentMsaa = HostConfigTool.ReadEyeMsaa(root);
        Write("CURRENT_MSAA " + (currentMsaa is int cm ? cm.ToString(CultureInfo.InvariantCulture) : "(absent: the shipped 4)"));
        bool? currentAniso = HostConfigTool.ReadAnisotropic(root);
        Write("CURRENT_ANISO " + (currentAniso is bool ca ? (ca ? "16x" : "off") : "(absent: the shipped off)"));
        // D-186-FovScale: per-axis render-FOV scales. A pre-Stage-3.5 file with the single FovScale key
        // reads FovScaleX=that value, FovScaleY=1.0 (horizontal-only cut); a 0.7.1 file with no FovScale
        // key reports (absent) on both axes (the host defaults to 1.0 in that case).
        float? currentFovX = HostConfigTool.ReadFovScaleX(root);
        Write("CURRENT_FOV_X " + (currentFovX is float cfx ? cfx.ToString("0.00", CultureInfo.InvariantCulture) : "(absent: the shipped 1.00)"));
        float? currentFovY = HostConfigTool.ReadFovScaleY(root);
        Write("CURRENT_FOV_Y " + (currentFovY is float cfy ? cfy.ToString("0.00", CultureInfo.InvariantCulture) : "(absent: the shipped 1.00)"));
        if (print || validate)
        {
            if (wanted != current) Write("WANTED " + wanted.Preview);
            (int width, int height) = wanted.EyeSize;
            (int bothWidth, int bothHeight) = RenderResolutionPolicy.BothEyes(width, height);
            Write((validate ? "VALID " : "OK ") + "eyepreset=" + wanted.Preset + " eyescale=" + wanted.EffectiveScale.ToString("0.00", CultureInfo.InvariantCulture) +
                " eye=" + width + "x" + height + " both=" + bothWidth + "x" + bothHeight +
                " uiscale=" + ((uiScale ?? currentUiScale) is double shownUi ? shownUi.ToString("0.00", CultureInfo.InvariantCulture) : "(absent)") +
                " msaa=" + ((eyeMsaa ?? currentMsaa) is int shownMsaa ? shownMsaa.ToString(CultureInfo.InvariantCulture) : "(absent)") +
                " aniso=" + ((aniso ?? currentAniso) is bool shownA ? (shownA ? "16x" : "off") : "(absent)") +
                " fovX=" + (((fovScaleX ?? (double?)currentFovX) is double dx) ? ((float)dx).ToString("0.00", CultureInfo.InvariantCulture) : "(absent)") +
                " fovY=" + (((fovScaleY ?? (double?)currentFovY) is double dy) ? ((float)dy).ToString("0.00", CultureInfo.InvariantCulture) : "(absent)"));
            return Ok;
        }
        HostConfigTool.Apply(root, wanted);
        if (uiScale is double newUiScale) HostConfigTool.ApplyUiScale(root, newUiScale);
        if (eyeMsaa is int newMsaa) HostConfigTool.ApplyEyeMsaa(root, newMsaa);
        if (aniso is bool newAniso) HostConfigTool.ApplyAnisotropic(root, newAniso);
        if (fovScaleX is double newFovX) HostConfigTool.ApplyFovScaleX(root, (float)newFovX);
        if (fovScaleY is double newFovY) HostConfigTool.ApplyFovScaleY(root, (float)newFovY);
        string backup = HostConfigTool.Save(path, root);
        Write("APPLIED " + wanted.Preview);
        Write("BACKUP " + backup);
        Write("NOTE the plugin reads this file once at startup; restart the game for it to take effect.");
        return Ok;
    }

    // D-171: opens the real window once (400 ms) with a config, so the form path is exercised without a human: control
    // construction, DPI mode, config load, preview text. Never writes anything; it only reports what the window loaded.
    private static int FormSelfTest(string? configPath)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new SettingsForm(configPath);
        bool closed = false;
        // The snapshot must be taken while the window is alive: Form.Close disposes the controls, after which
        // Controls.Count is 0 and a label reports an empty Text - reading them afterwards would fake a failed form.
        (bool Loaded, int Controls, string? Preview, bool Visible, string Status) snapshot = default;
        List<string> clipped = new();
        var timer = new System.Windows.Forms.Timer { Interval = 400 };
        timer.Tick += (_, _) => { snapshot = (form.FormLoaded, form.ControlCount, form.LoadedPreview, form.Visible, form.LoadedStatus); clipped = form.ClippedControls(); closed = true; timer.Stop(); form.Close(); };
        form.Shown += (_, _) => timer.Start();
        Application.Run(form);
        // D-187: the form lost the "Quality" NumericUpDown + its label, so the new lower bound is 13 (was 15 in 0.6).
        // D-186-FovScale: the per-axis preview text is "Render FOV scale X x0.85 Y x0.80 (1.00 = shipped; ...)" -
        // one combined line with both axes - so we look for "X x" and "Y x" in the preview instead of two
        // separate labels. The "Y x" check catches the case where the Y axis is wired up but X is not (and
        // vice versa), without depending on the exact wording of the surrounding text.
        bool ok = closed && snapshot.Loaded && snapshot.Visible && snapshot.Controls >= 14 && clipped.Count == 0 && snapshot.Preview?.Contains("Both eyes") == true && snapshot.Preview?.Contains("MSAA") == true && snapshot.Preview?.Contains("Render FOV scale X x") == true && snapshot.Preview?.Contains(" Y x") == true;
        Write("SELFTEST " + (ok ? "PASS" : "FAIL") + " form shown=" + closed + " visible=" + snapshot.Visible + " controls=" + snapshot.Controls +
            " loaded=" + snapshot.Loaded + " clipped=" + clipped.Count + (clipped.Count > 0 ? " [" + string.Join(" | ", clipped) + "]" : "") +
            " preview=" + (snapshot.Preview ?? "").Replace("\n", " | ") +
            " status=" + (snapshot.Status ?? "").Replace("\n", " | "));
        return ok ? Ok : Failed;
    }

    private static int SelfTest(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, HostConfigTool.FileName);
        JsonObject seed = new()
        {
            ["Enabled"] = true, ["HandAim"] = true, ["EyePreset"] = "normal", ["EyeWidth"] = 1920, ["EyeHeight"] = 1080,
            ["MarkerKinds"] = "CHEST,HEALING,POWER_UP,BOSS_CHEST", ["UiVerticalOffset"] = -0.25,
            // D-104-iso-promotion: 0.7.1's selftest proves Anisotropic round-trips the same way EyeMsaa already does.
            ["Anisotropic"] = false,
        };
        File.WriteAllText(path, seed.ToJsonString(HostConfigTool.Format));
        PictureSize wanted = new("high", 1920, 1080, 1.0);
        string? error = HostConfigTool.Validate(wanted);
        if (error != null) { Write("SELFTEST FAIL validation: " + error); return Failed; }
        JsonObject loaded = HostConfigTool.Load(path);
        HostConfigTool.Apply(loaded, wanted);
        HostConfigTool.ApplyUiScale(loaded, HostConfigTool.DefaultUiScale);
        HostConfigTool.ApplyEyeMsaa(loaded, HostConfigTool.DefaultEyeMsaa);
        // D-104-iso-promotion: round-trip the bool key off -> on -> off so the keys check sees the seed's value
        // again, while still exercising the "any bool" path through both Apply and the read-back in ReadAnisotropic.
        HostConfigTool.ApplyAnisotropic(loaded, false);
        HostConfigTool.ApplyAnisotropic(loaded, true);
        HostConfigTool.ApplyAnisotropic(loaded, false);
        // D-186-FovScale: round-trip 0.85 -> 1.0 on both axes so the keys check sees the shipped default
        // again, while still exercising the "any float" path through both Apply and the read-back in
        // ReadFovScaleX/Y. The shipped default (1.0 on both axes) is the "unedited file renders the same
        // picture" guarantee. A separate legacy test in SettingsTests.cs proves the single FovScale key
        // migrates correctly to FovScaleX (1.0 on Y) and is removed on save.
        HostConfigTool.ApplyFovScaleX(loaded, 0.85f);
        HostConfigTool.ApplyFovScaleY(loaded, 0.80f);
        HostConfigTool.ApplyFovScaleX(loaded, 1.0f);
        HostConfigTool.ApplyFovScaleY(loaded, 1.0f);
        // D-211: round-trip the three-choice snap turn 45 -> 30 so the keys check sees the shipped default again
        // while still proving Apply/Read handle a real choice.
        HostConfigTool.ApplySnapTurnDegrees(loaded, 45f);
        HostConfigTool.ApplySnapTurnDegrees(loaded, HostConfigTool.DefaultSnapTurnDegrees);
        string backup = HostConfigTool.Save(path, loaded);
        JsonObject written = HostConfigTool.Load(path);
        PictureSize readBack = HostConfigTool.Read(written);
        bool keys = readBack == wanted && written["EyePreset"]?.GetValue<string>() == "high" && written["EyeScale"]?.GetValue<double>() == 1.0 &&
            written["Enabled"]?.GetValue<bool>() == true && written["MarkerKinds"]?.GetValue<string>() == "CHEST,HEALING,POWER_UP,BOSS_CHEST" && File.Exists(backup) &&
            HostConfigTool.ReadUiScale(written) == HostConfigTool.DefaultUiScale &&
            HostConfigTool.ReadEyeMsaa(written) == HostConfigTool.DefaultEyeMsaa &&
            // D-104-iso-promotion: the seed's bool value lands back here after the off/on/off round-trip.
            written["Anisotropic"]?.GetValue<bool>() == false && HostConfigTool.ReadAnisotropic(written) == HostConfigTool.DefaultAnisotropic &&
            // D-186-FovScale: the shipped default (1.0) lands back on both axes after the 0.85/0.80 -> 1.0
            // round-trip, and the legacy single FovScale key was retired by ApplyFovScaleX.
            HostConfigTool.ReadFovScaleX(written) == 1.0f && HostConfigTool.ReadFovScaleY(written) == 1.0f && written["FovScale"] == null &&
            // D-211: the snap-turn key lands on the shipped 30 after the 45 -> 30 round-trip.
            HostConfigTool.ReadSnapTurnDegrees(written) == HostConfigTool.DefaultSnapTurnDegrees;
        Write("SELFTEST " + (keys ? "PASS" : "FAIL") + " " + readBack.Preview + " uiscale=" + HostConfigTool.ReadUiScale(written) +
            " msaa=" + HostConfigTool.ReadEyeMsaa(written) + " aniso=" + (HostConfigTool.ReadAnisotropic(written) is bool ra ? (ra ? "16x" : "off") : "(absent)") +
            " fovX=" + (HostConfigTool.ReadFovScaleX(written) is float rfx ? rfx.ToString("0.00", CultureInfo.InvariantCulture) : "(absent)") +
            " fovY=" + (HostConfigTool.ReadFovScaleY(written) is float rfy ? rfy.ToString("0.00", CultureInfo.InvariantCulture) : "(absent)") +
            " legacy=" + (written["FovScale"] == null ? "retired" : "kept") +
            " snap=" + (HostConfigTool.ReadSnapTurnDegrees(written) is float rs ? rs.ToString("0.#", CultureInfo.InvariantCulture) : "(absent)") +
            " backup=" + File.Exists(backup));
        return keys ? Ok : Failed;
    }

    private static double ParseDouble(string text, string flag) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : throw new ArgumentException(flag + " needs a number, got '" + text + "'");

    private static void Write(string line)
    {
        try { Console.Out.WriteLine(line); } catch (IOException) { } catch (ObjectDisposedException) { }
    }
}
