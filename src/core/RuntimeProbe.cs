using System.Text;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VampireHuntersVR.Stage0;

// Stage 0 READ-ONLY runtime probe (v4). Runs on the Unity main thread and only reads state.
// It never writes to any Unity object, never enables XR, never changes gameplay.
// Kept from v2: (a) which Transform in the camera's ancestor chain carries the look rotation,
// (b) whether the game rewrites camera position/rotation/FOV between our Update and LateUpdate,
// (c) HUD Canvas render modes and cursor lock/visibility.
// Added in v3: (d) a read-only legacy-Input trace so each sample can be attributed to actions without
//            asking the player to hold still, (e) the identity of the MonoBehaviours on the camera and
//            its ancestors (who owns the camera), (f) de-duplicated Canvas inventory with the active
//            and ScreenSpaceOverlay canvases listed explicitly, (g) player motion/grounded state and
//            the player's direct children.
// New in v4 (all still read-only, no Unity writes): (h) an every-frame edge logger for Time.timeScale
//            and ESC key-down, so pause/level-up/death transitions are caught the moment they happen
//            instead of being missed by the 10 s sample cadence; (i) a 0.5 s active-Canvas set watcher
//            that logs only on change (added/removed names) - this is what isolates a 2-4 s death panel;
//            (j) a depth-limited dump of the player's "Player For Anim Parent" subtree with component
//            class names, to find the weapon/aim/muzzle transform (V3-3).
public sealed class RuntimeProbe : MonoBehaviour
{
    public static ManualLogSource? Logger;
    private const float SampleSeconds = 10f;
    private const int MaxSamples = 300;
    private const int ChainDepth = 8;
    private const float UiWatchSeconds = 0.5f;
    private const int MaxUiChanges = 600;
    private const int AnimTreeBudget = 40;
    private const int AnimTreeDepth = 3;

    private string lastScene = "";
    private float nextSample;
    private int samples;
    private int failures;

    // (h) every-frame transition edges: pause / level-up / death all flip timeScale and release the cursor.
    private bool timeScaleValid;
    private float lastTimeScale = 1f;

    // (i) active-Canvas set watcher; logs only when the set changes, so a short-lived panel is captured.
    private float nextUiWatch;
    private int uiChanges;
    private bool uiValid;
    private readonly List<string> lastActiveUi = new();

    // (a) look-rotation tracker: local euler of the camera plus its ancestors, accumulated between samples.
    private readonly string[] chainName = new string[ChainDepth];
    private readonly Vector3[] lastLocalEuler = new Vector3[ChainDepth];
    private readonly float[] accumYaw = new float[ChainDepth];
    private readonly float[] accumPitch = new float[ChainDepth];
    private int chainCount;
    private bool chainValid;
    private int trackedFrames;

    // (b) camera write detection between our Update snapshot and LateUpdate.
    private Camera? watched;
    private bool haveSnap;
    private Vector3 snapPos;
    private Vector3 snapEuler;
    private float snapFov;
    private int watchedFrames;
    private int lateMoved;
    private int lateRotated;
    private int lateFov;

    // (d) read-only input trace. Disabled permanently after the first error (e.g. the game using the
    // new Input System would make UnityEngine.Input throw every frame).
    private bool inputOk = true;
    private float mouseDx;
    private float mouseDy;
    private float scroll;
    private float axisH;
    private float axisV;
    private int moveFrames;
    private int fire0Frames;
    private int fire1Frames;
    private int fire0Down;
    private int fire1Down;
    private int keySpace;
    private int keyShift;
    private int keyQ;
    private int keyE;
    private int keyR;
    private int keyEsc;
    private int keyTab;
    private Vector3 lastMousePos;

    // (g) player travel since the previous sample.
    private bool havePlayerPos;
    private Vector3 lastPlayerPos;
    private float playerTravel;

    public RuntimeProbe(IntPtr pointer) : base(pointer) { }

    private void Update()
    {
        TrackInput();
        try { TrackFrame(); }
        catch (Exception e) { failures++; Logger?.LogError("[Probe] TRACK_FAILED (read-only): " + e.Message); }

        // (h)/(i) transition watchers run on their own cadence, independent of the 10 s sample clock.
        try { WatchTimeScale(); }
        catch (Exception e) { failures++; Logger?.LogError("[Probe] TIMESCALE_WATCH_FAILED (read-only): " + e.Message); }
        if (Time.unscaledTime >= nextUiWatch)
        {
            nextUiWatch = Time.unscaledTime + UiWatchSeconds;
            try { WatchUiSet(); }
            catch (Exception e) { failures++; Logger?.LogError("[Probe] UI_WATCH_FAILED (read-only): " + e.Message); }
        }

        if (samples >= MaxSamples || failures >= 5) return;
        string scene = SceneManager.GetActiveScene().name;
        bool changed = scene != lastScene;
        if (!changed && Time.unscaledTime < nextSample) return;
        lastScene = scene;
        nextSample = Time.unscaledTime + SampleSeconds;
        samples++;
        try { Report(scene, changed); }
        catch (Exception e) { failures++; Logger?.LogError("[Probe] SAMPLE_FAILED (read-only, gameplay untouched): " + e.Message); }
    }

    private void LateUpdate()
    {
        try { CompareLate(); } catch { }
    }

    private void TrackFrame()
    {
        var cam = Camera.main;
        if (cam != watched)
        {
            watched = cam;
            haveSnap = false;
            chainValid = false;
            chainCount = 0;
        }
        if (cam == null) { chainValid = false; return; }

        // Rebuild the ancestor chain every frame: the camera is reparented after the intro animation.
        var t = cam.transform;
        int n = 0;
        for (int i = 0; i < ChainDepth && t != null; i++) { chainName[i] = t.name; n++; t = t.parent; }
        if (n != chainCount) { chainValid = false; chainCount = n; }

        t = cam.transform;
        for (int i = 0; i < n; i++)
        {
            Vector3 e = t.localEulerAngles;
            if (chainValid)
            {
                accumYaw[i] += Mathf.Abs(Mathf.DeltaAngle(lastLocalEuler[i].y, e.y));
                accumPitch[i] += Mathf.Abs(Mathf.DeltaAngle(lastLocalEuler[i].x, e.x));
            }
            lastLocalEuler[i] = e;
            t = t.parent;
        }
        chainValid = true;
        trackedFrames++;

        // Snapshot for the LateUpdate comparison.
        snapPos = cam.transform.position;
        snapEuler = cam.transform.eulerAngles;
        snapFov = cam.fieldOfView;
        haveSnap = true;
    }

    private void CompareLate()
    {
        var cam = watched;
        if (cam == null || !haveSnap) return;
        watchedFrames++;
        var tr = cam.transform;
        if ((tr.position - snapPos).sqrMagnitude > 1e-6f) lateMoved++;
        if (Quaternion.Angle(Quaternion.Euler(snapEuler), tr.rotation) > 0.01f) lateRotated++;
        if (Mathf.Abs(cam.fieldOfView - snapFov) > 0.01f) lateFov++;
    }

    private static string V(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";

    // (d) Read-only input trace. Accumulates per frame between samples so a plain play session is
    // self-labelling; any exception disables it once instead of failing the whole probe.
    private void TrackInput()
    {
        if (!inputOk) return;
        try
        {
            mouseDx += Mathf.Abs(Input.GetAxisRaw("Mouse X"));
            mouseDy += Mathf.Abs(Input.GetAxisRaw("Mouse Y"));
            // mouseScrollDelta instead of the "Mouse ScrollWheel" axis: a missing axis would throw.
            scroll += Mathf.Abs(Input.mouseScrollDelta.y);
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            axisH += Mathf.Abs(h);
            axisV += Mathf.Abs(v);
            if (Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f) moveFrames++;
            if (Input.GetMouseButton(0)) fire0Frames++;
            if (Input.GetMouseButton(1)) fire1Frames++;
            if (Input.GetMouseButtonDown(0)) fire0Down++;
            if (Input.GetMouseButtonDown(1)) fire1Down++;
            if (Input.GetKey(KeyCode.Space)) keySpace++;
            if (Input.GetKey(KeyCode.LeftShift)) keyShift++;
            if (Input.GetKey(KeyCode.Q)) keyQ++;
            if (Input.GetKey(KeyCode.E)) keyE++;
            if (Input.GetKey(KeyCode.R)) keyR++;
            if (Input.GetKey(KeyCode.Escape)) keyEsc++;
            // (h) an ESC edge is logged immediately: it is the only evidence branch the samples can miss.
            if (Input.GetKeyDown(KeyCode.Escape))
                Logger?.LogInfo($"[Probe] ESC_DOWN t={Time.unscaledTime:F1} frame={Time.frameCount} " +
                    $"timeScale={Time.timeScale:F2} cursor={Cursor.lockState}/{Cursor.visible} " +
                    $"scene='{SceneManager.GetActiveScene().name}' ui={UiSignature()}");
            if (Input.GetKey(KeyCode.Tab)) keyTab++;
            lastMousePos = Input.mousePosition;
        }
        catch (Exception e)
        {
            inputOk = false;
            Logger?.LogWarning("[Probe] input trace disabled after first error (read-only; gameplay untouched): " + e.Message);
        }
    }

    // (e) Who owns the camera: list the MonoBehaviours on the camera object and up to three ancestors.
    private void ReportComponents(string label, Transform t)
    {
        try
        {
            var monos = t.gameObject.GetComponents<MonoBehaviour>();
            var names = new List<string>();
            for (int i = 0; i < monos.Length && names.Count < 16; i++)
            {
                var m = monos[i];
                if (m == null) continue;
                names.Add(ClassName(m) + (m.enabled ? "" : "(off)"));
            }
            Logger?.LogInfo($"[Probe]  comps {label} '{t.name}' path='{Path(t)}' monoCount={monos.Length} [{string.Join(",", names)}]");
        }
        catch (Exception e)
        {
            Logger?.LogWarning("[Probe] component dump failed (read-only): " + e.Message);
        }
    }

    // Il2CppInterop hands back the requested base type for GetComponents<MonoBehaviour>(), so the managed
    // GetType().Name is always "MonoBehaviour". Read the real IL2CPP class name instead; fall back to the
    // managed name if the native lookup is unavailable.
    private static string ClassName(MonoBehaviour m)
    {
        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(m.Pointer);
            string? ns = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(klass));
            string? name = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass));
            if (!string.IsNullOrEmpty(name)) return string.IsNullOrEmpty(ns) ? name! : ns + "." + name;
        }
        catch (Exception)
        {
            // Fall through to the managed name; never fail the sample for a diagnostic label.
        }
        return m.GetType().Name;
    }

    // (g) Movement state and the direct children, so weapon/HUD parents can be identified later.
    private void ReportPlayerDetail(PlayerController p)
    {
        try
        {
            var go = p.gameObject;
            var rb = go.GetComponent<Rigidbody>();
            Logger?.LogInfo(rb == null
                ? "[Probe]  motion rigidbody=none"
                : $"[Probe]  motion rigidbody vel={V(rb.velocity)} angVel={V(rb.angularVelocity)} kinematic={rb.isKinematic} gravity={rb.useGravity}");
            var cc = go.GetComponent<CharacterController>();
            Logger?.LogInfo(cc == null
                ? "[Probe]  motion charController=none"
                : $"[Probe]  motion charController vel={V(cc.velocity)} grounded={cc.isGrounded} height={cc.height:F2} radius={cc.radius:F2}");
        }
        catch (Exception e)
        {
            Logger?.LogWarning("[Probe] player motion read failed (read-only): " + e.Message);
        }

        try
        {
            var t = p.transform;
            var kids = new List<string>();
            for (int i = 0; i < t.childCount && i < 14; i++)
            {
                var c = t.GetChild(i);
                if (c != null) kids.Add(c.name);
            }
            Logger?.LogInfo($"[Probe]  playerChildren n={t.childCount} [{string.Join(",", kids)}]");
            float angle = watched == null ? -1f : Vector3.Angle(t.forward, watched.transform.forward);
            Logger?.LogInfo($"[Probe]  playerFwd={V(t.forward)} camFwd={V(watched == null ? Vector3.zero : watched.transform.forward)} fwdAngle={angle:F1}");
        }
        catch (Exception e)
        {
            Logger?.LogWarning("[Probe] player child read failed (read-only): " + e.Message);
        }

        ReportAnimTree(p);
    }

    // (j) V3-3: the weapon / aim / muzzle transforms. v3 found 'Player For Anim Parent' as the aim parent
    // but never saw below it, so walk that subtree (depth 3, max 40 nodes) and name the real components.
    private void ReportAnimTree(PlayerController p)
    {
        try
        {
            var root = p.transform;
            Transform? anim = null;
            var other = new List<string>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c == null) continue;
                if (anim == null && c.name.IndexOf("Anim", StringComparison.OrdinalIgnoreCase) >= 0) { anim = c; continue; }
                if (c.name.StartsWith("Prefab_", StringComparison.OrdinalIgnoreCase)) other.Add(c.name);
            }
            Logger?.LogInfo($"[Probe]  playerWeaponishChildren=[{string.Join(", ", other)}] (Prefab_* direct children)");
            if (anim == null)
            {
                Logger?.LogInfo("[Probe]  animTree (no Anim-named child of the player root)");
                return;
            }
            int budget = AnimTreeBudget;
            Logger?.LogInfo($"[Probe]  animTree root='{anim.name}' children={anim.childCount} path='{Path(anim)}' depth<={AnimTreeDepth} budget={AnimTreeBudget}");
            DumpAnimNode(anim, 1, budget);
            if (budget <= 0) Logger?.LogInfo($"[Probe]  animTree truncated at {AnimTreeBudget} nodes");
        }
        catch (Exception e)
        {
            Logger?.LogWarning("[Probe] anim tree read failed (read-only): " + e.Message);
        }
    }

    private void DumpAnimNode(Transform node, int depth, int budget)
    {
        if (depth > AnimTreeDepth) return;
        for (int i = 0; i < node.childCount; i++)
        {
            if (budget <= 0) return;
            var c = node.GetChild(i);
            if (c == null) continue;
            budget--;
            Logger?.LogInfo($"[Probe]   tree d{depth} '{c.name}' children={c.childCount} localRot={V(c.localEulerAngles)} " +
                $"localPos={V(c.localPosition)} localScale={V(c.localScale)} comps=[{CompNames(c)}]");
            DumpAnimNode(c, depth + 1, budget);
        }
    }

    private string CompNames(Transform t)
    {
        var names = new List<string>();
        var monos = t.gameObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length && names.Count < 8; i++)
        {
            var m = monos[i];
            if (m == null) continue;
            names.Add(ClassName(m) + (m.enabled ? "" : "(off)"));
        }
        return string.Join(",", names);
    }

    private void ReportInput()
    {
        Logger?.LogInfo($"[Probe] input ok={inputOk} mouseDx={mouseDx:F1} mouseDy={mouseDy:F1} scroll={scroll:F2} " +
            $"axisH={axisH:F1} axisV={axisV:F1} moveFrames={moveFrames} " +
            $"fire0={fire0Frames}f/{fire0Down}down fire1={fire1Frames}f/{fire1Down}down " +
            $"keys space={keySpace} shift={keyShift} q={keyQ} e={keyE} r={keyR} esc={keyEsc} tab={keyTab} " +
            $"mousePos={V(lastMousePos)} (frames or totals since the previous sample)");
    }

    private void Report(string scene, bool changed)
    {
        var sb = new StringBuilder();
        sb.Append("[Probe] ").Append(changed ? "SCENE_CHANGED " : "SAMPLE ").Append("scene='").Append(scene)
          .Append("' t=").Append(Time.unscaledTime.ToString("F1"))
          .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height)
          .Append(" xrLoadedDevice='").Append(UnityEngine.XR.XRSettings.loadedDeviceName).Append("'")
          .Append(" xrEnabled=").Append(UnityEngine.XR.XRSettings.enabled);
        Logger?.LogInfo(sb.ToString());

        var main = Camera.main;
        Logger?.LogInfo("[Probe] Camera.main=" + (main == null ? "null" : $"'{main.name}' path='{Path(main.transform)}'"));
        var cameras = Camera.allCameras;
        Logger?.LogInfo("[Probe] activeCameras=" + cameras.Length);
        foreach (var c in cameras)
        {
            var t = c.transform;
            Logger?.LogInfo($"[Probe]  cam '{c.name}' path='{Path(t)}' enabled={c.enabled} depth={c.depth:F1} fov={c.fieldOfView:F1} ortho={c.orthographic} " +
                $"tag={c.tag} mask={c.cullingMask} targetTex={(c.targetTexture == null ? "none" : c.targetTexture.name)} " +
                $"stereoTarget={c.stereoTargetEye} pos={V(t.position)} euler={V(t.eulerAngles)} parent='{(t.parent == null ? "" : t.parent.name)}'");
        }

        // (e) who owns the camera: MonoBehaviours on the camera object and its first three ancestors.
        var compT = main == null ? null : main.transform;
        for (int i = 0; i < 4 && compT != null; i++)
        {
            ReportComponents(i == 0 ? "cam" : "camAnc" + i, compT);
            compT = compT.parent;
        }

        // (a) look-rotation source: which chain node actually turned since the last sample.
        Logger?.LogInfo($"[Probe] lookChain n={chainCount} trackedFrames={trackedFrames} (accumulated since last sample)");
        for (int i = 0; i < chainCount; i++)
            Logger?.LogInfo($"[Probe]  chain[{i}] '{chainName[i]}' localEuler={V(lastLocalEuler[i])} dYaw={accumYaw[i]:F1} dPitch={accumPitch[i]:F1}");

        // (b) does the game touch the camera between the probe's Update and LateUpdate?
        Logger?.LogInfo($"[Probe] camWrites watchedFrames={watchedFrames} moved={lateMoved} rotated={lateRotated} fovChanged={lateFov}");

        // (c) HUD canvas render modes and cursor state.
        ReportCanvases();
        Logger?.LogInfo($"[Probe] cursor lockState={Cursor.lockState} visible={Cursor.visible} timeScale={Time.timeScale:F2}");

        // (d) read-only input trace: which sample contains which action.
        ReportInput();

        var players = Resources.FindObjectsOfTypeAll<PlayerController>();
        Logger?.LogInfo("[Probe] PlayerController instances=" + players.Length);
        foreach (var p in players)
        {
            if (p == null) continue;
            var t = p.transform;
            Logger?.LogInfo($"[Probe]  player '{p.name}' path='{Path(t)}' activeInHierarchy={p.gameObject.activeInHierarchy} pos={V(t.position)} euler={V(t.eulerAngles)}");
            Logger?.LogInfo($"[Probe]   fp_camera={(p.fp_camera == null ? "null" : "'" + p.fp_camera.name + "' path='" + Path(p.fp_camera.transform) + "'")} " +
                $"camera_transform={(p.camera_transform == null ? "null" : "'" + p.camera_transform.name + "' path='" + Path(p.camera_transform.transform) + "'")} " +
                $"camera_parent={(p.camera_parent == null ? "null" : "'" + p.camera_parent.name + "'")}");
            if (p.gameObject.activeInHierarchy)
            {
                Vector3 pos = t.position;
                if (havePlayerPos) playerTravel += Vector3.Distance(pos, lastPlayerPos);
                lastPlayerPos = pos;
                havePlayerPos = true;
                Logger?.LogInfo($"[Probe]  playerTravel={playerTravel:F2} m since previous sample; worldPos={V(pos)}");
                ReportComponents("player", t);
                ReportPlayerDetail(p);
                break;
            }
        }
        var inputs = Resources.FindObjectsOfTypeAll<PlayerInputController>();
        Logger?.LogInfo("[Probe] PlayerInputController instances=" + inputs.Length);

        Logger?.LogInfo($"[Probe] frame frameCount={Time.frameCount} deltaTime={Time.deltaTime:F4} unscaledDelta={Time.unscaledDeltaTime:F4} " +
            $"fullscreen={Screen.fullScreen} vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate}");

        ResetCounters();
    }

    // (h) Level-up, pause and death all stop time and release the cursor, so the exact moment of the edge
    // plus the UI set active at that moment is what tells the three apart. Edge-triggered, every frame.
    private void WatchTimeScale()
    {
        float ts = Time.timeScale;
        if (!timeScaleValid) { timeScaleValid = true; lastTimeScale = ts; return; }
        if (Mathf.Abs(ts - lastTimeScale) < 0.001f) return;
        Logger?.LogInfo($"[Probe] TIMESCALE {lastTimeScale:F2}->{ts:F2} t={Time.unscaledTime:F1} frame={Time.frameCount} " +
            $"scene='{SceneManager.GetActiveScene().name}' cursor={Cursor.lockState}/{Cursor.visible} ui={UiSignature()}");
        lastTimeScale = ts;
        // Re-baseline the UI set so the next UI watch does not log the same set twice.
        uiValid = false;
    }

    // (i) Active-Canvas set watcher: a full scan is far too heavy per frame, but 2 Hz is enough to catch a
    // 2-4 s death/defeat panel. Logs only when the set changes, so the log stays bounded.
    private void WatchUiSet()
    {
        var current = ActiveUiLabels();
        if (!uiValid)
        {
            uiValid = true;
            lastActiveUi.Clear();
            lastActiveUi.AddRange(current);
            return;
        }
        var added = new List<string>();
        var removed = new List<string>();
        foreach (var name in current) if (!lastActiveUi.Contains(name)) added.Add(name);
        foreach (var name in lastActiveUi) if (!current.Contains(name)) removed.Add(name);
        if (added.Count == 0 && removed.Count == 0) return;
        lastActiveUi.Clear();
        lastActiveUi.AddRange(current);
        if (uiChanges >= MaxUiChanges) return;
        uiChanges++;
        Logger?.LogInfo($"[Probe] UICHANGE t={Time.unscaledTime:F1} scene='{SceneManager.GetActiveScene().name}' " +
            $"timeScale={Time.timeScale:F2} cursor={Cursor.lockState}/{Cursor.visible} active={current.Count} " +
            $"added=[{string.Join(", ", added)}] removed=[{string.Join(", ", removed)}]");
        Logger?.LogInfo("[Probe]  uiSet " + UiSignature());
    }

    private List<string> ActiveUiLabels()
    {
        var labels = new List<string>();
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var c in canvases)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            string label = $"{c.name}#{c.sortingOrder}";
            if (!labels.Contains(label)) labels.Add(label);
        }
        labels.Sort(StringComparer.Ordinal);
        return labels;
    }

    private string UiSignature()
    {
        try
        {
            var labels = ActiveUiLabels();
            const int cap = 14;
            if (labels.Count <= cap) return labels.Count + "=[" + string.Join(", ", labels) + "]";
            return labels.Count + "=[" + string.Join(", ", labels.GetRange(0, cap)) + ", +" + (labels.Count - cap) + " more]";
        }
        catch (Exception e) { return "unavailable:" + e.GetType().Name; }
    }

    private void ReportCanvases()
    {
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        var byMode = new Dictionary<string, int>();
        var paths = new List<string>();
        var activeList = new List<string>();
        var overlays = new List<string>();
        int activeCount = 0;
        foreach (var c in canvases)
        {
            if (c == null) continue;
            string mode = c.renderMode.ToString();
            byMode.TryGetValue(mode, out int modeCount);
            byMode[mode] = modeCount + 1;
            bool isActive = c.gameObject.activeInHierarchy;
            if (isActive) activeCount++;

            string path = Path(c.transform);
            bool duplicate = false;
            foreach (var seen in paths) { if (seen == path) { duplicate = true; break; } }
            if (duplicate) continue;
            paths.Add(path);

            if (isActive && activeList.Count < 20)
                activeList.Add($"activeCanvas '{c.name}' renderMode={c.renderMode} sort={c.sortingOrder} worldCamera={(c.worldCamera == null ? "none" : c.worldCamera.name)} scale={c.scaleFactor:F2} path='{path}'");
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && overlays.Count < 15)
                overlays.Add($"overlayCanvas '{c.name}' sort={c.sortingOrder} scale={c.scaleFactor:F2} active={isActive} path='{path}'");
        }

        var modeHistogram = new StringBuilder();
        foreach (var kv in byMode)
        {
            if (modeHistogram.Length > 0) modeHistogram.Append(',');
            modeHistogram.Append(kv.Key).Append(':').Append(kv.Value);
        }
        Logger?.LogInfo($"[Probe] canvases total={canvases.Length} distinctPaths={paths.Count} active={activeCount} modes=[{modeHistogram}]");
        foreach (var o in overlays) Logger?.LogInfo("[Probe]  " + o);
        foreach (var a in activeList) Logger?.LogInfo("[Probe]  " + a);
        if (activeList.Count == 0) Logger?.LogInfo("[Probe]  activeCanvas (none)");
    }

    private void ResetCounters()
    {
        for (int i = 0; i < ChainDepth; i++) { accumYaw[i] = 0f; accumPitch[i] = 0f; }
        watchedFrames = lateMoved = lateRotated = lateFov = 0;
        mouseDx = mouseDy = scroll = axisH = axisV = 0f;
        moveFrames = fire0Frames = fire1Frames = fire0Down = fire1Down = 0;
        keySpace = keyShift = keyQ = keyE = keyR = keyEsc = keyTab = 0;
        playerTravel = 0f;
    }

    private static string Path(Transform t)
    {
        var parts = new List<string>();
        for (int i = 0; t != null && i < 12; i++) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }
}