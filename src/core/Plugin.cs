using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;

namespace VampireHuntersVR.Stage0;

// Diagnostics preserve the original flat camera/input. Explicit project-local SP5 flags additionally
// opt into offscreen camera capture or the simulator-only same-device OpenXR diagnostic host.
[BepInPlugin("local.vampirehuntersvr.stage0", "Vampire Hunters VR Stage 0", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    private ConfigEntry<bool>? enabled;
    private ConfigEntry<bool>? safeMode;
    private Timer? reloadTimer;
    private string fingerprint = "";
    private Stage0Settings settings = new(true, true, false, Array.Empty<string>());
    private bool loaded;
    private readonly object sync = new();

    public override void Load()
    {
        // Do not rewrite invalid/user-edited configuration while binding/applying defaults.
        bool existed = File.Exists(Config.ConfigFilePath);
        Config.SaveOnConfigSet = false;
        enabled = Config.Bind("Features", "DiagnosticsEnabled", true,
            "Stage 0 diagnostic logs only. No gameplay hooks exist in this build.");
        safeMode = Config.Bind("Safety", "SafeMode", true,
            "Keep flat gameplay. SP5 offscreen simulator diagnostics require a separate opt-in config; marker disables them.");
        if (!existed) Config.Save();
        ApplySettings(Stage0SettingsFile.Read(Config.ConfigFilePath, Config.ConfigFilePath + ".safe-mode"));
        Log.LogInfo("[Stage0] ENTRY_EXECUTED; stereo=NOT_IMPLEMENTED; head=NOT_IMPLEMENTED; controllers=NOT_IMPLEMENTED");
        Log.LogInfo("[Stage0] This is loader-entry evidence only, NOT a confirmed camera hook or VR smoke pass.");
        loaded = true;
        ReportState();
        StartProbe();
        StartOverlay();
        StartCapture();
        StartStereoCapture();
        StartOpenXrHost();
        reloadTimer = new Timer(_ => CheckReload(), null, 1000, 1000);
    }

    private void StartProbe()
    {
        if (enabled?.Value != true) return;
        try
        {
            RuntimeProbe.Logger = Log;
            ClassInjector.RegisterTypeInIl2Cpp<RuntimeProbe>();
            AddComponent<RuntimeProbe>();
            Log.LogInfo("[Stage0] PROBE_STARTED probe=v4 read-only; samples scene/camera/player/input/UI state every 10 s (max 300), " +
                "logs Time.timeScale/ESC transitions every frame and active-UI set changes every 0.5 s, " +
                "and dumps the player's Anim subtree (depth 3); no Unity writes; input trace auto-disables on error");
        }
        catch (Exception e)
        {
            Log.LogError("[Stage0] PROBE_START_FAILED; gameplay unaffected: " + e);
        }
    }

    // D-044 visible diagnostic. Registered unconditionally so the validated DiagnosticsEnabled flag can
    // turn the label on and off at runtime (PLAYTEST-0 criteria 3 and 5); it only ever draws.
    private void StartOverlay()
    {
        try
        {
            DiagnosticOverlay.Logger = Log;
            DiagnosticOverlay.Status = "starting";
            DiagnosticOverlay.Visible = enabled?.Value == true;
            ClassInjector.RegisterTypeInIl2Cpp<DiagnosticOverlay>();
            AddComponent<DiagnosticOverlay>();
            Log.LogInfo("[Stage0] OVERLAY_REGISTERED visible=" + DiagnosticOverlay.Visible +
                "; IMGUI box top-left; read-only, no XR, no camera/input/renderer write");
        }
        catch (Exception e)
        {
            Log.LogError("[Stage0] OVERLAY_START_FAILED; gameplay unaffected: " + e);
        }
    }

    // M4a (D-069): the headset-free camera capture. Enabled only through the process environment, so the set
    // of files installed into the game does not change and Stage 0 behaviour is identical when it is not asked
    // for. The capture renders a clone of the gameplay camera; the game's own camera is never modified.
    private void StartCapture()
    {
        try
        {
            var config = CaptureConfig.Load();
            if (!config.Valid)
            {
                Log.LogInfo("[M4A] M4A_DISABLED (VHVR_M4A_ENABLE not set); no capture component added");
                return;
            }
            FrameCapture.Logger = Log;
            FrameCapture.Config = config;
            ClassInjector.RegisterTypeInIl2Cpp<FrameCapture>();
            AddComponent<FrameCapture>();
            Log.LogInfo($"[M4A] M4A_REQUESTED out='{config.OutputDir}' interval={config.IntervalSeconds:F0}s " +
                $"max={config.MaxCaptures} size={config.Width}x{config.Height} screenshot={config.Screenshot} clone={config.Clone}");
        }
        catch (Exception e)
        {
            Log.LogError("[M4A] M4A_START_FAILED; gameplay unaffected: " + e);
        }
    }

    private void StartStereoCapture()
    {
        try
        {
            string file = Path.Combine(Paths.ConfigPath, "vhvr-stereo-capture.json");
            if (!File.Exists(file)) return;
            if (new FileInfo(file).Length > Stage0SettingsParser.MaxBytes) throw new InvalidDataException("stereo config exceeds 64 KiB");
            var config = System.Text.Json.JsonSerializer.Deserialize<StereoCaptureConfig>(File.ReadAllText(file));
            if (config?.Enabled != true || settings.MarkerPresent || enabled?.Value != true) return;
            // This only renders mod-owned cameras offscreen; SafeMode's flat gameplay remains preserved.
            StereoCameraCapture.Config = config;
            StereoCameraCapture.Logger = Log;
            ClassInjector.RegisterTypeInIl2Cpp<StereoCameraCapture>();
            AddComponent<StereoCameraCapture>();
        }
        catch (Exception e) { Log.LogError("[STEREO_CAPTURE] INIT_FAILED; flat game unaffected: " + e); }
    }

    private void StartOpenXrHost()
    {
        try
        {
            string path=Path.Combine(Paths.ConfigPath,"vhvr-openxr-host.json");
            if(!File.Exists(path)||settings.MarkerPresent||enabled?.Value!=true)return;
            if(new FileInfo(path).Length>Stage0SettingsParser.MaxBytes)throw new InvalidDataException("host config oversized");
            var config=System.Text.Json.JsonSerializer.Deserialize<OpenXrHostConfig>(File.ReadAllText(path));
            if(config?.Enabled!=true)return;
            OpenXrHost.Config=config;OpenXrHost.Logger=Log;
            ClassInjector.RegisterTypeInIl2Cpp<OpenXrHost>();AddComponent<OpenXrHost>();
        }
        catch(Exception e){Log.LogError("[OPENXR_HOST] INIT_FAILED: "+e);}
    }

    private void CheckReload()
    {
        lock (sync)
        {
            if (!loaded) return;
            try
            {
                var current = Stage0SettingsFile.Read(Config.ConfigFilePath, Config.ConfigFilePath + ".safe-mode");
                if (current.Fingerprint == fingerprint) return;
                // Pure config snapshot; no Unity objects touched on the timer thread.
                ApplySettings(current);
                Log.LogInfo("[Stage0] CONFIG_RELOADED; content/marker fingerprint changed");
                ReportState();
            }
            catch (Exception e)
            {
                Log.LogError("[Stage0] CONFIG_RELOAD_FAILED; gameplay remains flat: " + e);
            }
        }
    }

    private void ApplySettings(Stage0SettingsRead current)
    {
        settings = current.Settings;
        fingerprint = current.Fingerprint;
        if (enabled != null) enabled.Value = settings.DiagnosticsEnabled;
        if (safeMode != null) safeMode.Value = settings.SafeMode;
        // Label follows the same validated value; ReportState() repeats it for the disabled path.
        DiagnosticOverlay.Visible = settings.DiagnosticsEnabled;
        foreach (string warning in settings.Warnings) Log.LogWarning("[Stage0] CONFIG_VALIDATION: " + warning);
    }

    private void ReportState()
    {
        DiagnosticOverlay.Visible = enabled?.Value == true;
        DiagnosticOverlay.Status = $"diag={DiagnosticOverlay.Visible}; safeMode={settings.SafeMode}; marker={settings.MarkerPresent}; gameplay=FLAT_UNMODIFIED";
        if (enabled?.Value != true) return;
        Log.LogInfo($"[Stage0] DiagnosticsEnabled=true; SafeMode={settings.SafeMode}; marker={settings.MarkerPresent}; effectiveSafeMode={settings.EffectiveSafeMode}; gameplay=FLAT_UNMODIFIED");
        if (!settings.EffectiveSafeMode)
            Log.LogWarning("[Stage0] SafeMode=false cannot enable VR in this diagnostic build; no XR/hooks implemented.");
    }

    public override bool Unload()
    {
        lock (sync) { loaded = false; reloadTimer?.Dispose(); reloadTimer = null; }
        return true;
    }
}