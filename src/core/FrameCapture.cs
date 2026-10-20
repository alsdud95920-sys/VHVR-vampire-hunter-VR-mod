using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VampireHuntersVR.Stage0;

// M4a (D-069) — the headset-free half of M4: get the game's own camera pixels out of the running game.
//
// Read-only with respect to the game: the capture renders a *clone* of the gameplay camera (Camera.CopyFrom)
// into a RenderTexture this mod owns, and never writes to the game's own camera, its transform, its target or
// its matrices. The player keeps seeing the normal screen; the mod gets a picture it can prove it produced.
//
// Opt-in through the process environment (no file inside the game, so the install transaction is untouched):
//   VHVR_M4A_ENABLE=1 VHVR_M4A_OUT=<dir> [VHVR_M4A_INTERVAL=15] [VHVR_M4A_MAX=4]
//   [VHVR_M4A_W=1280] [VHVR_M4A_H=720] [VHVR_M4A_SCREENSHOT=1] [VHVR_M4A_CLONE=1]
// With VHVR_M4A_ENABLE unset the component is never even added, so Stage 0 behaviour is bit-for-bit unchanged.
public sealed class CaptureConfig
{
    public bool Enabled;
    public string OutputDir = "";
    public float IntervalSeconds = 15f;
    public int MaxCaptures = 4;
    public int Width = 1280;
    public int Height = 720;
    public bool Screenshot = true;
    public bool Clone = true;

    public static readonly CaptureConfig Disabled = new();

    // The environment wins (it is per-process and leaves no file behind). If it is absent, a small file under
    // BepInEx\config is read instead - that is the path used when Steam launches the game, because Steam does
    // not pass our environment through. Format: one "key: value" per line, keys enable/out/interval/max/
    // width/height/clone/screenshot.
    public static CaptureConfig Load()
    {
        if (Environment.GetEnvironmentVariable("VHVR_M4A_ENABLE") != null) return FromEnvironment();
        var c = new CaptureConfig();
        string file = Path.Combine(BepInEx.Paths.ConfigPath, "vhvr-m4a.json");
        if (!File.Exists(file)) return c;
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim().TrimEnd(',');
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
            if (line == "{" || line == "}") continue;
            int split = line.IndexOf(':');
            if (split < 0) split = line.IndexOf('=');
            if (split <= 0) continue;
            string key = line[..split].Trim().Trim('"').ToLowerInvariant();
            string value = line[(split + 1)..].Trim().Trim('"');
            switch (key)
            {
                case "enable": c.Enabled = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "out": c.OutputDir = value; break;
                case "interval": c.IntervalSeconds = ParseFloat(value, c.IntervalSeconds); break;
                case "max": c.MaxCaptures = ParseInt(value, c.MaxCaptures); break;
                case "width": c.Width = ParseInt(value, c.Width); break;
                case "height": c.Height = ParseInt(value, c.Height); break;
                case "clone": c.Clone = value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                case "screenshot": c.Screenshot = value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
            }
        }
        c.Clamp();
        return c;
    }

    private static int ParseInt(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static float ParseFloat(string s, float fallback) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

    private void Clamp()
    {
        if (IntervalSeconds < 1f) IntervalSeconds = 1f;
        if (MaxCaptures < 1) MaxCaptures = 1;
        if (Width < 64) Width = 64;
        if (Height < 64) Height = 64;
    }

    public static CaptureConfig FromEnvironment()
    {
        var c = new CaptureConfig();
        c.Enabled = Read("VHVR_M4A_ENABLE") == "1";
        c.OutputDir = Read("VHVR_M4A_OUT");
        c.IntervalSeconds = ReadFloat("VHVR_M4A_INTERVAL", c.IntervalSeconds);
        c.MaxCaptures = ReadInt("VHVR_M4A_MAX", c.MaxCaptures);
        c.Width = ReadInt("VHVR_M4A_W", c.Width);
        c.Height = ReadInt("VHVR_M4A_H", c.Height);
        c.Screenshot = Read("VHVR_M4A_SCREENSHOT") != "0";
        c.Clone = Read("VHVR_M4A_CLONE") != "0";
        c.Clamp();
        return c;
    }

    public bool Valid => Enabled && OutputDir.Length > 0;

    private static string Read(string name) => Environment.GetEnvironmentVariable(name) ?? "";

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Read(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static float ReadFloat(string name, float fallback) =>
        float.TryParse(Read(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
}

public sealed class FrameCapture : MonoBehaviour
{
    public static ManualLogSource? Logger;
    public static CaptureConfig Config = CaptureConfig.Disabled;

    private const int WarmupFrames = 4;

    private float nextCapture;
    private int index;
    private bool done;
    private bool started;
    private string runDir = "";

    private Camera? source;
    private Camera? clone;
    private RenderTexture? target;
    private Texture2D? readback;
    private int readAtFrame = -1;

    private void Start()
    {
        try
        {
            runDir = Path.Combine(Config.OutputDir, "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(runDir);
            nextCapture = Time.unscaledTime + 3f;
            started = true;
            Logger?.LogInfo($"[M4A] M4A_START out='{runDir}' interval={Config.IntervalSeconds:F0}s max={Config.MaxCaptures} " +
                $"size={Config.Width}x{Config.Height} screenshot={Config.Screenshot} clone={Config.Clone} " +
                $"scene='{SceneManager.GetActiveScene().name}'");
        }
        catch (Exception e)
        {
            done = true;
            Logger?.LogError("[M4A] M4A_START_FAILED: " + e);
        }
    }

    private void Update()
    {
        if (!started || done) return;
        try
        {
            if (readAtFrame >= 0)
            {
                if (Time.frameCount >= readAtFrame) FinishCapture();
                return;
            }
            if (Time.unscaledTime < nextCapture) return;
            nextCapture = Time.unscaledTime + Config.IntervalSeconds;
            BeginCapture();
        }
        catch (Exception e)
        {
            done = true;
            Logger?.LogError("[M4A] M4A_CAPTURE_FAILED; game untouched, capture stopped: " + e);
        }
    }

    private void BeginCapture()
    {
        if (done) return;
        index++;
        if (index > Config.MaxCaptures) { Complete(); return; }

        source = Camera.main;
        string picked = source != null ? "Camera.main" : "none";
        if (source == null)
        {
            source = FindFallbackCamera();
            picked = source != null ? "fallback(most enabled screen camera by depth)" : "none";
            }
            Logger?.LogInfo($"[M4A] M4A_CAPTURE {index}/{Config.MaxCaptures} scene='{SceneManager.GetActiveScene().name}' " +
                $"frame={Time.frameCount} t={Time.unscaledTime:F1} camera={picked}");
            if (source == null)
            {
                Logger?.LogWarning("[M4A] M4A_NO_CAMERA: no usable camera this frame; retrying on the next tick");
                index--;
                return;
            }
            DescribeCamera(source);
            ReportChain(source);
            if (Config.Screenshot)
                Logger?.LogWarning("[M4A] M4A_SCREENSHOT_UNAVAILABLE: UnityEngine.ScreenCapture is not present in this build's interop (stripped by IL2CPP); the clone RenderTexture is the capture path. This is a build fact, recorded rather than worked around.");
            if (Config.Clone) PrepareClone();
            readAtFrame = Time.frameCount + 4;
        }

        // D-133: a mod-owned clone camera that renders the game camera into a private RenderTexture. The game's own
        // camera is never modified; the clone is enabled only for the read frame and destroyed with this component.
        private void PrepareClone()
        {
            if (source == null) return;
            if (target == null || target.width != Config.Width || target.height != Config.Height)
            {
                if (clone != null) clone.targetTexture = null;
                if (target != null) { target.Release(); Destroy(target); }
                target = new RenderTexture(Config.Width, Config.Height, 24, RenderTextureFormat.Default)
                {
                    name = "VHVR_M4A_Target",
                    antiAliasing = 1,
                    useMipMap = false
                };
                target.Create();
                if (clone != null) clone.targetTexture = target;
            }
            if (clone == null)
            {
                var go = new GameObject("VHVR_M4A_Camera");
                go.transform.SetParent(null, false);
                DontDestroyOnLoad(go);
                clone = go.AddComponent<Camera>();
                clone.enabled = false;
                Logger?.LogInfo("[M4A] M4A_CLONE_CREATED (mod-owned camera; the game's own camera is not modified)");
            }
            clone.CopyFrom(source);
            clone.enabled = true;
            clone.name = "VHVR_M4A_Camera";
            clone.targetTexture = target;
            clone.clearFlags = source.clearFlags;
            clone.backgroundColor = source.backgroundColor;
            clone.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            clone.worldToCameraMatrix = source.worldToCameraMatrix;
            clone.projectionMatrix = source.projectionMatrix;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Logger?.LogInfo($"[M4A] M4A_CLONE_READY rt={target.width}x{target.height} depth={clone.depth.ToString(inv)} " +
                $"cullingMask=0x{clone.cullingMask.ToString("X8", inv)} clearFlags={clone.clearFlags} copyFov={clone.fieldOfView.ToString("F2", inv)} " +
                $"sourceTargetTexture={(source.targetTexture == null ? "screen" : source.targetTexture.name)}");
        }




































































    private void FinishCapture()
    {
        readAtFrame = -1;
        string rawFile = Path.Combine(runDir, $"clone-{index:00}.raw");
        string stats = "clone=not_rendered";
        string rawBytes = "-1";
        try
        {
            if (target != null && readback == null)
                readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            if (target != null && readback != null)
            {
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
                    readback.Apply(false, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                var px = readback.GetPixels32();               // Unity order: row 0 is the BOTTOM row
                var bytes = new byte[px.Length * 4];
                long sumR = 0, sumG = 0, sumB = 0;
                int nonBlack = 0;
                var seen = new HashSet<int>();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    bytes[i * 4 + 0] = c.r;
                    bytes[i * 4 + 1] = c.g;
                    bytes[i * 4 + 2] = c.b;
                    bytes[i * 4 + 3] = c.a;
                    sumR += c.r; sumG += c.g; sumB += c.b;
                    if (c.r > 8 || c.g > 8 || c.b > 8) nonBlack++;
                    if (seen.Count < 4096) seen.Add((c.r << 16) | (c.g << 8) | c.b);
                }
                File.WriteAllBytes(rawFile, bytes);
                rawBytes = bytes.Length.ToString(CultureInfo.InvariantCulture);
                stats = $"mean=({sumR / (double)px.Length:F1},{sumG / (double)px.Length:F1},{sumB / (double)px.Length:F1}) " +
                    $"nonBlackPct={100.0 * nonBlack / px.Length:F1} distinctColors>={seen.Count}";
                Logger?.LogInfo($"[M4A] M4A_CLONE_PIXELS {target.width}x{target.height} {stats} raw='{rawFile}' bytes={bytes.Length}");
            }
            else
            {
                Logger?.LogWarning("[M4A] M4A_CLONE_READBACK_SKIPPED: no render target (clone disabled or creation failed)");
            }
        }
        catch (Exception e)
        {
            Logger?.LogError("[M4A] M4A_CLONE_READBACK_FAILED: " + e);
        }

        WriteReceipt(stats, rawFile, rawBytes);
        if (index >= Config.MaxCaptures) Complete();
    }

    private void WriteReceipt(string stats, string rawFile, string rawBytes)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"generatedUtc\": \"{DateTime.UtcNow:O}\",\n");
            sb.Append("  \"stage\": \"M4a\",\n");
            sb.Append($"  \"scene\": \"{Esc(SceneManager.GetActiveScene().name)}\",\n");
            sb.Append($"  \"frame\": {Time.frameCount}, \"time\": {Time.unscaledTime.ToString("F3", CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"index\": {index}, \"runDir\": \"{Esc(runDir)}\",\n");
            sb.Append($"  \"renderTexture\": \"{Config.Width}x{Config.Height}\",\n");
            sb.Append($"  \"cloneStats\": \"{Esc(stats)}\",\n");
            sb.Append($"  \"rawFile\": \"{Esc(rawFile)}\", \"rawBytes\": {rawBytes},\n");
            sb.Append("  \"rawLayout\": \"RGBA8, row 0 = bottom (Unity order)\",\n");
            sb.Append($"  \"screen\": {{ \"width\": {Screen.width}, \"height\": {Screen.height}, \"fullScreen\": {(Screen.fullScreen ? "true" : "false")} }},\n");
            if (source != null) AppendCameraJson(sb);
            sb.Append("  \"note\": \"M4a headset-free capture: the mod rendered a clone of the gameplay camera into its own RenderTexture \" +\n");
            sb.Append("            \"and read the pixels back. The game's own camera was not modified.\"\n");
            sb.Append("}\n");
            string file = Path.Combine(runDir, $"capture-{index:00}.json");
            File.WriteAllText(file, sb.ToString());
            Logger?.LogInfo($"[M4A] M4A_RECEIPT file='{file}'");
        }
        catch (Exception e)
        {
            Logger?.LogError("[M4A] M4A_RECEIPT_FAILED: " + e);
        }
    }

    private void AppendCameraJson(StringBuilder sb)
    {
        var c = source;
        if (c == null) return;
        var ci = CultureInfo.InvariantCulture;
        sb.Append("  \"camera\": {\n");
        sb.Append($"    \"name\": \"{Esc(c.name)}\", \"path\": \"{Esc(TransformPath(c.transform))}\",\n");
        sb.Append($"    \"depth\": {c.depth.ToString("F2", ci)}, \"fov\": {c.fieldOfView.ToString("F3", ci)},\n");
        sb.Append($"    \"aspect\": {c.aspect.ToString("F4", ci)}, \"near\": {c.nearClipPlane.ToString("F4", ci)}, \"far\": {c.farClipPlane.ToString("F1", ci)},\n");
        sb.Append($"    \"cullingMask\": {(uint)c.cullingMask}, \"clearFlags\": \"{c.clearFlags}\", \"orthographic\": {(c.orthographic ? "true" : "false")},\n");
        sb.Append($"    \"targetTexture\": \"{(c.targetTexture == null ? "screen" : Esc(c.targetTexture.name))}\",\n");
        sb.Append("    \"pixelRect\": [").Append(c.pixelRect.x.ToString("F1", ci)).Append(", ").Append(c.pixelRect.y.ToString("F1", ci))
          .Append(", ").Append(c.pixelRect.width.ToString("F1", ci)).Append(", ").Append(c.pixelRect.height.ToString("F1", ci)).Append("],\n");
        sb.Append("    \"worldToCameraMatrix\": ").Append(Matrix(c.worldToCameraMatrix)).Append(",\n");
        sb.Append("    \"projectionMatrix\": ").Append(Matrix(c.projectionMatrix)).Append(",\n");
        sb.Append("    \"position\": ").Append(Vector(c.transform.position)).Append(",\n");
        sb.Append("    \"euler\": ").Append(Vector(c.transform.eulerAngles)).Append(",\n");
        sb.Append($"    \"isCameraMain\": {(c == Camera.main ? "true" : "false")},\n");
        sb.Append($"    \"playerControllerCamera\": \"{Esc(PcCameraCheck(c))}\"\n");
        sb.Append("  },\n");
    }

    private static string TransformPath(Transform t)
    {
        var names = new List<string>();
        for (int i = 0; i < 12 && t != null; i++)
        {
            names.Add(t.name);
            t = t.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static string Matrix(Matrix4x4 m)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder("[");
        for (int i = 0; i < 4; i++)
        {
            sb.Append('[');
            for (int j = 0; j < 4; j++)
            {
                if (j > 0) sb.Append(", ");
                sb.Append(m[i, j].ToString("G9", inv));
            }
            sb.Append(i < 3 ? "], " : "]");
        }
        return sb.Append(']').ToString();
    }

    private static string Vector(Vector3 v)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return $"[{v.x.ToString("G9", inv)}, {v.y.ToString("G9", inv)}, {v.z.ToString("G9", inv)}]";
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ClassName(MonoBehaviour m)
    {
        try { return m.GetType().Name; }
        catch { return "unknown"; }
    }

    private static Camera? FindFallbackCamera()
    {
        Camera? best = null;
        foreach (var c in Camera.allCameras)
        {
            if (c != null && c.enabled && c.targetTexture == null && !c.orthographic &&
                (best == null || c.depth > best.depth))
                best = c;
        }
        return best;
    }

    private void DescribeCamera(Camera cam)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var rect = cam.pixelRect;
        Logger?.LogInfo($"[M4A] M4A_CAMERA name='{cam.name}' path='{TransformPath(cam.transform)}' depth={cam.depth.ToString("F2", inv)} " +
            $"fov={cam.fieldOfView.ToString("F3", inv)} aspect={cam.aspect.ToString("F4", inv)} " +
            $"near={cam.nearClipPlane.ToString("F4", inv)} far={cam.farClipPlane.ToString("F1", inv)} " +
            $"cullingMask=0x{((uint)cam.cullingMask).ToString("X8", inv)} clearFlags={cam.clearFlags} ortho={cam.orthographic} " +
            $"target={(cam.targetTexture == null ? "screen" : cam.targetTexture.name)} " +
            $"pixelRect=({rect.x.ToString("F0", inv)},{rect.y.ToString("F0", inv)},{rect.width.ToString("F0", inv)},{rect.height.ToString("F0", inv)}) " +
            $"screen={Screen.width}x{Screen.height}");
        Logger?.LogInfo($"[M4A] M4A_MATRIX worldToCamera={Matrix(cam.worldToCameraMatrix)}");
        Logger?.LogInfo($"[M4A] M4A_MATRIX projection={Matrix(cam.projectionMatrix)}");
        Logger?.LogInfo($"[M4A] M4A_POSE position={Vector(cam.transform.position)} euler={Vector(cam.transform.eulerAngles)}");
    }

    private void ReportChain(Camera cam)
    {
        try
        {
            var t = cam.transform;
            for (int level = 0; t != null && level < 6; level++)
            {
                var monos = t.gameObject.GetComponents<MonoBehaviour>();
                var names = new List<string>();
                foreach (var m in monos)
                {
                    if (names.Count >= 14) break;
                    if (m == null) continue;















































































                    names.Add(ClassName(m) + (m.enabled ? "" : "(off)"));
                }
                Logger?.LogInfo($"[M4A] M4A_CHAIN level={level} go='{t.name}' active={t.gameObject.activeInHierarchy} " +
                    $"monoCount={monos.Length} [{string.Join(", ", names)}]");
                t = t.parent;
            }
        }
        catch (Exception e)
        {
            Logger?.LogWarning("[M4A] M4A_CHAIN_FAILED: " + e.Message);
        }
    }

    // Reads PlayerController.fp_camera by name through the runtime class pointer and compares it with the camera
    // we captured. The raw pointer comparison is deliberate: constructing a Camera from an untrusted pointer
    // could crash the game, and equality is all the pin needs.
    private static string PcCameraCheck(Camera cam)
    {
        try
        {
            var pcs = Resources.FindObjectsOfTypeAll<PlayerController>();
            if (pcs == null || pcs.Length == 0) return "no_player_controller_found";
            var pc = pcs[0];
            if (pc == null) return "player_controller_null";
            IntPtr klass = Il2CppClassPointerStore<PlayerController>.NativeClassPtr;
            if (klass == IntPtr.Zero) return "no_class_pointer";
            IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(klass, "fp_camera");
            if (field == IntPtr.Zero) return "field_not_found:fp_camera";
            uint offset = IL2CPP.il2cpp_field_get_offset(field);
            if (offset == 0 || offset > 4096) return "implausible_offset:" + offset;
            IntPtr value = Marshal.ReadIntPtr(pc.Pointer + (int)offset);
            IntPtr main = cam.Pointer;
            return value == main
                ? "match(PlayerController.fp_camera == captured camera)"
                : $"mismatch(pc=0x{value.ToInt64():X} captured=0x{main.ToInt64():X})";
        }
        catch (Exception e)
        {
            return "unavailable:" + e.GetType().Name + ":" + e.Message;
        }
    }

    private void Complete()
    {
        if (done) return;
        done = true;
        // Belt and braces: stop Unity calling Update again, so a lost flag cannot repeat the completion
        // (the first run logged M4A_DONE three extra times, TOOL-014).
        enabled = false;
        Logger?.LogInfo($"[M4A] M4A_DONE captures={index} dir='{runDir}' " +
            "results=see M4A_CLONE_PIXELS lines above");
        if (target != null) target.Release();
    }

    private void OnDestroy()
    {
        try
        {
            if (target != null) { target.Release(); Destroy(target); target = null; }
            if (readback != null) { Destroy(readback); readback = null; }
            if (clone != null) { Destroy(clone.gameObject); clone = null; }
        }
        catch (Exception e)
        {
            Logger?.LogWarning("[M4A] M4A_CLEANUP_FAILED: " + e.Message);
        }
    }

}