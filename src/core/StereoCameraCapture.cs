using System.Globalization;
using System.Text.Json;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using VampireHuntersVR.Render;

namespace VampireHuntersVR.Stage0;

// M4b prerequisite only: runtime-pose snapshot -> game camera pair. NO in-game OpenXR session yet.
public sealed class StereoCaptureConfig
{
    public bool Enabled { get; set; }
    public string Output { get; set; } = "";
    public string RuntimeReceipt { get; set; } = "";
    public int Width { get; set; } = 960;
    public int Height { get; set; } = 1008;
    public float UnitsPerMeter { get; set; } = 1;
    public float WarmupSeconds { get; set; } = 15;
}

public sealed class StereoCameraCapture : MonoBehaviour
{
    public static ManualLogSource? Logger;
    public static StereoCaptureConfig Config = new();
    private Camera[] eyes = new Camera[2];
    private RenderTexture[] targets = new RenderTexture[2];
    private Texture2D[] readers = new Texture2D[2];
    private RenderTexture[] bridgeTargets = new RenderTexture[2];
    private Texture2D[] external = new Texture2D[2];
    private bool copied;
    private int copyIssuedFrame = -1;
    private int copiedReadFrame = -1;
    private Camera? source;
    private Vector3 anchorPosition;
    private Quaternion anchorRotation;
    private JsonDocument? profile;
    private string run = "";
    private int rung;
    private int readFrame = -1;
    private float next;
    private bool done;
    private readonly List<object> measurements = new();
    private float[][] viewMatrices = new float[2][];
    private float[][] projections = new float[2][];
    private float[][] positions = new float[2][];
    private float[][] orientations = new float[2][];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] Rungs = { "baseline", "lean-right-10cm", "yaw-right-10deg" };

    private void Start()
    {
        try
        {
            if (Config.Width < 128 || Config.Height < 128 || Config.Width > 2048 || Config.Height > 2048 ||
                !float.IsFinite(Config.WarmupSeconds) || Config.WarmupSeconds < 5 || Config.WarmupSeconds > 60 ||
                !float.IsFinite(Config.UnitsPerMeter) || Config.UnitsPerMeter <= 0 || Config.UnitsPerMeter > 100 ||
                !Path.IsPathRooted(Config.Output) || !Path.IsPathRooted(Config.RuntimeReceipt))
                throw new ArgumentException("invalid stereo diagnostic config");
            profile = JsonDocument.Parse(File.ReadAllText(Config.RuntimeReceipt));
            var root = profile.RootElement;
            if (root.GetProperty("Verdict").GetString() != "PASS" || root.GetProperty("RuntimeName").GetString() != "Meta XR Simulator" ||
                root.GetProperty("FirstSubmittedEyeViews").GetArrayLength() != 2)
                throw new ArgumentException("requires successful simulator receipt with two exact eye views");
            for (int eye = 0; eye < 2; eye++)
            {
                var item = root.GetProperty("FirstSubmittedEyeViews")[eye];
                if (item.GetProperty("Eye").GetInt32() != eye || (item.GetProperty("ViewStateFlags").GetInt32() & 3) != 3)
                    throw new ArgumentException("invalid runtime eye identity/pose flags");
            }
            run = Path.Combine(Config.Output, "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(run);
            File.Copy(Config.RuntimeReceipt, Path.Combine(run, "runtime-profile.json"));
            next = Time.unscaledTime + Config.WarmupSeconds;
            Logger?.LogInfo("[STEREO_CAPTURE] START snapshot-only; inGameOpenXrHost=false; originalCameraUnmodified=true; out=" + run);
        }
        catch (Exception e) { Fail(e); }
    }

    private static float[] Values(JsonElement e) => e.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    private static float[] Q(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
    private static Matrix4x4 M(float[] m)
    {
        var matrix = new Matrix4x4();
        for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) matrix[r, c] = m[r * 4 + c];
        return matrix;
    }

    private void Update()
    {
        if (done) return;
        try
        {
            if (readFrame >= 0)
            {
                if (Time.frameCount < readFrame) return;
                if (!copied)
                {
                    for (int eye = 0; eye < 2; eye++)
                    {
                        // Copy the same rendered frame into an externally wrapped native target. Freeze the
                        // clone so later CPU comparisons cannot accidentally compare different scene frames.
                        eyes[eye].enabled = false;
                        Graphics.CopyTexture(targets[eye], external[eye]);
                    }
                    copied = true;
                    copyIssuedFrame = Time.frameCount;
                    readFrame = Time.frameCount + 2;
                    return;
                }
                copiedReadFrame = Time.frameCount;
                FinishPair();
                return;
            }
            if (Time.unscaledTime < next) return;
            PreparePair();
        }
        catch (Exception e) { Fail(e); }
    }


    private void PreparePair()
    {
        if (rung == 0)
        {
            source = Camera.main;
            if (source == null) throw new InvalidOperationException("no main camera after bounded warmup");
            anchorPosition = source.transform.position;
            anchorRotation = source.transform.rotation;
        }
        if (source == null || profile == null) throw new InvalidOperationException("source/profile lost");
        Quaternion virtualRotation = rung == 2 ? Quaternion.Euler(0, 10, 0) : Quaternion.identity;
        Vector3 virtualTranslation = rung == 1 ? new Vector3(0.1f * Config.UnitsPerMeter, 0, 0) : Vector3.zero;
        for (int eye = 0; eye < 2; eye++)
        {
            var item = profile.RootElement.GetProperty("FirstSubmittedEyeViews")[eye];
            float[] xyz = Values(item.GetProperty("Position"));
            float[] quat = Values(item.GetProperty("Orientation"));
            float[] fov = Values(item.GetProperty("Fov"));
            if (xyz.Length != 3 || quat.Length != 4 || fov.Length != 4) throw new ArgumentException("profile dimensions");
            var local = StereoMath.UnityPosition(xyz[0], xyz[1], xyz[2], Config.UnitsPerMeter);
            var orientation = StereoMath.UnityOrientation(quat[0], quat[1], quat[2], quat[3]);
            Vector3 pos = anchorPosition + anchorRotation * (virtualTranslation + virtualRotation * new Vector3(local[0], local[1], local[2]));
            Quaternion rot = anchorRotation * virtualRotation * new Quaternion(orientation[0], orientation[1], orientation[2], orientation[3]);
            positions[eye] = V(pos); orientations[eye] = Q(rot);
            viewMatrices[eye] = StereoMath.UnityView(positions[eye], orientations[eye]);
            projections[eye] = StereoMath.Projection(fov[0], fov[1], fov[2], fov[3], source.nearClipPlane, source.farClipPlane);
            if (eyes[eye] == null)
            {
                var go = new GameObject("VHVR_SnapshotEye_" + eye); DontDestroyOnLoad(go);
                eyes[eye] = go.AddComponent<Camera>();
                targets[eye] = new RenderTexture(Config.Width, Config.Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, useMipMap = false };
                targets[eye].Create();
                readers[eye] = new Texture2D(Config.Width, Config.Height, TextureFormat.RGBA32, false);
                bridgeTargets[eye] = new RenderTexture(Config.Width, Config.Height, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1, useMipMap = false };
                if (!bridgeTargets[eye].Create()) throw new InvalidOperationException("external bridge target creation failed");
                // The destination is ours, not OpenXR's. Matching dimensions/colourspace and one mip/sample.
                IntPtr native = bridgeTargets[eye].GetNativeTexturePtr();
                if (native == IntPtr.Zero) throw new InvalidOperationException("null native bridge pointer");
                external[eye] = Texture2D.CreateExternalTexture(Config.Width, Config.Height, TextureFormat.RGBA32, false, !bridgeTargets[eye].sRGB, native);
            }
            eyes[eye].CopyFrom(source);
            eyes[eye].name = "VHVR_SnapshotEye_" + eye;
            eyes[eye].targetTexture = targets[eye];
            eyes[eye].transform.SetPositionAndRotation(pos, rot);
            // Assert independently against Unity's transform-derived view before overriding the clone matrix.
            eyes[eye].ResetWorldToCameraMatrix();
            Matrix4x4 nativeView = eyes[eye].worldToCameraMatrix;
            Matrix4x4 expectedView = M(viewMatrices[eye]);
            for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++)
                if (MathF.Abs(nativeView[row, col] - expectedView[row, col]) > 0.001f)
                    throw new InvalidOperationException("STEREO_ASSERT view differs from Unity transform");
            eyes[eye].worldToCameraMatrix = expectedView;
            eyes[eye].projectionMatrix = M(projections[eye]);
            eyes[eye].enabled = true;
        }
        copied = false;
        readFrame = Time.frameCount + 8;
        Logger?.LogInfo("[STEREO_CAPTURE] PREPARE " + Rungs[rung] + "; eyeSource=gameCamera; runtimePoseSource=snapshot; scene=" + SceneManager.GetActiveScene().name);
    }


    private void FinishPair()
    {
        readFrame = -1;
        var eyeEvidence = new List<object>();
        for (int eye = 0; eye < 2; eye++)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = targets[eye];
                readers[eye].ReadPixels(new Rect(0, 0, Config.Width, Config.Height), 0, 0, false);
                readers[eye].Apply(false, false);
            }
            finally { RenderTexture.active = previous; }
            var pixels = readers[eye].GetPixels32();
            byte[] raw = new byte[pixels.Length * 4];
            int nonBlack = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                raw[i * 4] = pixels[i].r; raw[i * 4 + 1] = pixels[i].g;
                raw[i * 4 + 2] = pixels[i].b; raw[i * 4 + 3] = pixels[i].a;
                if (pixels[i].r > 8 || pixels[i].g > 8 || pixels[i].b > 8) nonBlack++;
            }
            string file = Path.Combine(run, Rungs[rung] + "-eye" + eye + ".raw");
            File.WriteAllBytes(file, raw);
            int mismatched = 0;
            previous = RenderTexture.active;
            try
            {
                RenderTexture.active = bridgeTargets[eye];
                readers[eye].ReadPixels(new Rect(0, 0, Config.Width, Config.Height), 0, 0, false);
                readers[eye].Apply(false, false);
            }
            finally { RenderTexture.active = previous; }
            var bridgePixels = readers[eye].GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r != bridgePixels[i].r || pixels[i].g != bridgePixels[i].g || pixels[i].b != bridgePixels[i].b)
                    mismatched++;
            if (mismatched != 0) throw new InvalidOperationException("STEREO_EXTERNAL_COPY_ASSERT mismatched RGB pixels=" + mismatched);
            eyes[eye].enabled = false;
            float[] forward = StereoMath.Rotate(Q(anchorRotation), new[] { 0f, 0f, 2f * Config.UnitsPerMeter });
            float[] point = { anchorPosition.x + forward[0], anchorPosition.y + forward[1], anchorPosition.z + forward[2] };
            eyeEvidence.Add(new
            {
                eye, rawFile = file, rawBytes = raw.Length, nonBlackFraction = (double)nonBlack / pixels.Length,
                externalCopy = new { destinationOwner = "mod-owned-not-runtime", mismatchedRgbPixels = mismatched, copyIssuedFrame, readbackFrame = copiedReadFrame,
                    sourceSrgb = targets[eye].sRGB, destinationSrgb = bridgeTargets[eye].sRGB, synchronousReadbackUsed = true,
                    openXrGpuReleaseSynchronizationProven = false },
                position = positions[eye], orientation = orientations[eye], view = viewMatrices[eye], projection = projections[eye],
                analyticForwardPointNdc = StereoMath.Project(viewMatrices[eye], projections[eye], point),
                textureNativePointer = targets[eye].GetNativeTexturePtr().ToInt64().ToString("X", CultureInfo.InvariantCulture)
            });
            if (nonBlack == 0) throw new InvalidOperationException("all-black eye after eight render warmup frames: " + eye);
        }
        var measurement = new
        {
            stage = "M4b-prerequisite", inGameOpenXrHost = false, runtimePoseSource = "snapshot",
            virtualMotionSource = "diagnostic-local-offset-not-live-runtime-motion", eyeSource = "gameCamera",
            verdict = "CAPTURED_NOT_STEREO_VERIFIED", rung = Rungs[rung], frame = Time.frameCount,
            scene = SceneManager.GetActiveScene().name, sourceCamera = source?.name,
            sourceCameraOriginalPosition = V(anchorPosition), sourceCameraOriginalRotation = Q(anchorRotation),
            unitsPerMeter = Config.UnitsPerMeter, worldScaleCalibrated = false,
            width = Config.Width, height = Config.Height, rawLayout = "RGBA8 bottom-up", eyes = eyeEvidence
        };
        measurements.Add(measurement);
        File.WriteAllText(Path.Combine(run, Rungs[rung] + ".json"), JsonSerializer.Serialize(measurement, JsonOptions));
        Logger?.LogInfo("[STEREO_CAPTURE] PAIR_SAVED " + Rungs[rung] + "; no host/stereo gate claim");
        rung++;
        if (rung == Rungs.Length)
        {
            File.WriteAllText(Path.Combine(run, "summary.json"), JsonSerializer.Serialize(new
            {
                verdict = "CAPTURED_NOT_STEREO_VERIFIED", inGameOpenXrHost = false, measurements,
                note = "Scene pixels and exact pose/projection evidence; analyze content/disparity/motion before claiming camera stereo. Does not close M4b."
            }, JsonOptions));
            done = true; enabled = false;
            Cleanup();
            Logger?.LogInfo("[STEREO_CAPTURE] DONE pairs=3; inGameOpenXrHost=false; out=" + run);
        }
        else next = Time.unscaledTime + 2;
    }

    [HideFromIl2Cpp]
    private void Fail(Exception error)
    {
        done = true; enabled = false;
        Logger?.LogError("[STEREO_CAPTURE] FAILED; flat gameplay unaffected: " + error);
        try
        {
            if (run.Length > 0) File.WriteAllText(Path.Combine(run, "failure.json"), JsonSerializer.Serialize(new { verdict = "FAIL", error = error.ToString() }, JsonOptions));
            Cleanup();
        }
        catch (Exception e) { Logger?.LogWarning("[STEREO_CAPTURE] cleanup failed: " + e.Message); }
    }
    private void Cleanup()
    {
        for (int eye = 0; eye < 2; eye++)
        {
            if (eyes[eye] != null) { eyes[eye].enabled = false; eyes[eye].targetTexture = null; Destroy(eyes[eye].gameObject); eyes[eye] = null!; }
            if (targets[eye] != null) { targets[eye].Release(); Destroy(targets[eye]); targets[eye] = null!; }
            if (readers[eye] != null) { Destroy(readers[eye]); readers[eye] = null!; }
            if (external[eye] != null) { Destroy(external[eye]); external[eye] = null!; }
            if (bridgeTargets[eye] != null) { bridgeTargets[eye].Release(); Destroy(bridgeTargets[eye]); bridgeTargets[eye] = null!; }
        }
        profile?.Dispose(); profile = null;
    }
    private void OnDestroy() { Cleanup(); }
}