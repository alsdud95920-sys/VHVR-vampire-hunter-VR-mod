using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace VampireHuntersVR.Interaction;

// D-090 (TOOL-040): in-VR UI. ScreenSpaceOverlay canvases never reach the stereo eye cameras, so each root overlay canvas
// is switched to WorldSpace and placed in front of the head. While the game is paused (menus, level-up) the controller ray
// picks Button/Toggle components on those canvases and the trigger clicks them. Opt-in via HandUi; NOT headset tested.
internal sealed partial class HandAimDriver
{
    // D-108: paused panels use the full HMD pose; only non-HUD canvases during play retain heading-follow placement.
    // D-181 (user decision 2026-10-15): the paused ESC / pause menu is 3.0 m wide at 1.3 m. The old 1.9 m @ 1.6 m made its
    // small text unreadable. The extras ui-scale multiplier still applies on top of this width.
    private const float UiDistance = 1.3f, UiWidth = 3.0f, UiMaxRay = 6f, UiFollowDeadAngle = 25f, UiReanchorDistance = 0.4f;
    private const float UiLevelDistance = 1.0f, UiLevelWidth = 1.4f, UiDimMaxAlpha = 0.25f;
    // D-102: the in-game HUD canvases are locked to the head (no yaw lag) at this distance/width while the game is running.
    private const float HudDistance = 1.5f, HudWidth = 1.6f;
    private sealed class UiCanvas { public Canvas Canvas = null!; public Vector2 Size; public bool Placed; public float Yaw; public float Distance = UiDistance, Width = UiWidth; public Vector3 Anchor; public bool HeadLocked, LevelUp; public float CenterX, CenterY, Fit = 1f, LoggedX, LoggedY; public bool HasCenter, Logged; }
    // D-107: level-up content is centred by its measured bounds (not the canvas pivot) and shrunk to fit this block (metres at UiLevelDistance).
    private const float UiLevelMaxContentWidth = 1.5f, UiLevelMaxContentHeight = 0.9f;
    private readonly List<string> levelLog = new();
    private readonly Dictionary<int, UiCanvas> uiCanvases = new();
    private DateTime nextUiScan, nextUiDim;
    private bool uiWasPaused, uiFailed, uiLeftHeld, uiRightHeld;
    public bool UiEnabled;
    public bool HideHudBackdrop = true;
    public float UiVerticalOffset = HudLayoutPolicy.DefaultVerticalOffset;
    public int UiConverted, UiClicks, UiHovers;
    public string? UiLastClick, UiError;

    private void UiTick(Camera source)
    {
        if (!UiEnabled || uiFailed) return;
        try
        {
            if (DateTime.UtcNow >= nextUiScan) { nextUiScan = DateTime.UtcNow.AddSeconds(0.5); UiScan(source); HideGameCrosshair(); HideDarkHudPanels(); }
            bool paused = Time.timeScale <= 0.01f;
            if (paused && !uiWasPaused) { foreach (var c in uiCanvases.Values) { c.Placed = false; c.HasCenter = false; c.Fit = 1f; c.Logged = false; } nextUiDim = DateTime.MinValue; } // re-centre menus on the head
            if (!paused && uiWasPaused) UiBackdropRestore(); // D-105: the backdrop cull lives only while the game is paused
            uiWasPaused = paused;
            foreach (var c in uiCanvases.Values) UiPlace(c, source, paused);
            if (paused && DateTime.UtcNow >= nextUiDim)
            {
                nextUiDim = DateTime.UtcNow.AddSeconds(0.5);
                foreach (var c in uiCanvases.Values)
                    if (c.Canvas != null && c.Canvas.gameObject.activeInHierarchy) { UiDim(c); if (c.LevelUp) UiFlattenScan(c); }
                UiFlattenApply(); // measure the flattened cards, not their animated tilt
                foreach (var c in uiCanvases.Values)
                    if (c.Canvas != null && c.Canvas.gameObject.activeInHierarchy) UiMeasureLevelUp(c, source);
            }
            if (paused) UiFlattenApply();
            // D-108: the new centre/fit must reach both rendering and ray selection in THIS frame.
            foreach (var c in uiCanvases.Values) UiPlace(c, source, paused);
            if (!paused) { uiLeftHeld = false; uiRightHeld = false; UiRayDraw(left, default, default, false); UiRayDraw(right, default, default, false); return; }
            UiPointer(left, source, ref uiLeftHeld);
            UiPointer(right, source, ref uiRightHeld);
        }
        catch (System.Exception e) { uiFailed = true; UiError = e.GetType().Name + ": " + e.Message; }
    }

    private void UiScan(Camera source)
    {
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null || !canvas.isRootCanvas) continue;
            int id = canvas.GetInstanceID();
            if (uiCanvases.ContainsKey(id)) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue; // every root overlay canvas: pause and game-over menus are not player children
            float scale = Mathf.Max(0.01f, canvas.scaleFactor);
            var size = new Vector2(Screen.width / scale, Screen.height / scale);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = source;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var entry = new UiCanvas { Canvas = canvas, Size = size };
            string path = UiPathOf(canvas.transform);
            if (path.Contains("Level Up")) { entry.Distance = UiLevelDistance; entry.Width = UiLevelWidth; entry.LevelUp = HudLayoutPolicy.IsLevelUp(path); }
            entry.HeadLocked = HudLayoutPolicy.IsHeadLocked(path);
            if (entry.HeadLocked) { entry.Distance = HudDistance; entry.Width = HudWidth; HudLockedCanvases++; }
            rect.localScale = Vector3.one * (entry.Width / size.x);
            uiCanvases[id] = entry;
            UiConverted++;
        }
    }

    // D-100: the game's own crosshair is a head-locked HUD element (ComboAndCrosshairs canvas) while the shots now follow the hand muzzle,
    // so it pointed somewhere the bullets did not go (video 1010). The per-hand reticle spheres replace it. Only Graphics under a
    // Crosshair component are culled at the CanvasRenderer, so the combo counter in the same canvas stays visible.
    public int CrosshairHidden;
    private void HideGameCrosshair()
    {
        foreach (var crosshair in Resources.FindObjectsOfTypeAll<Crosshair>())
        {
            if (crosshair == null) continue;
            foreach (var g in crosshair.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || g.canvasRenderer == null || g.canvasRenderer.cull) continue;
                g.canvasRenderer.cull = true;
                CrosshairHidden++;
            }
        }
    }

    // D-112: keep D-102/D-106 heuristics and also match the measured Bottom Shadow sprite exactly.
    // Texture alpha is independent of Graphic tint; suppress this known background persistently,
    // not opaque HUD icons/minimap. Restore its original tint alpha on driver release.
    public int HudLockedCanvases, HudDarkHidden;
    private readonly List<Graphic> hudDarkCulled = new();
    private readonly Dictionary<int, (Graphic Graphic, float Alpha, bool Culled)> hudBackdropOriginal = new();

    // Canvas layout may reset cull. Alpha suppression persists; both states are restored on release.
    private void ApplyHudBackdropSuppression()
    {
        foreach (var entry in hudBackdropOriginal.Values)
        {
            var g = entry.Graphic;
            if (g == null || g.canvasRenderer == null) continue;
            var color = g.color; color.a = 0f; g.color = color;
            g.canvasRenderer.cull = true;
        }
    }
    private readonly HashSet<int> hudDarkSeen = new();
    private readonly List<string> hudDarkLog = new();
    private void HideDarkHudPanels()
    {
        if (!HideHudBackdrop) return;
        ApplyHudBackdropSuppression();
        foreach (var c in uiCanvases.Values)
        {
            if (!c.HeadLocked || c.Canvas == null) continue;
            float canvasW = c.Size.x * c.Canvas.transform.lossyScale.x, canvasH = c.Size.y * c.Canvas.transform.lossyScale.y;
            if (!(canvasW > 0f) || !(canvasH > 0f)) continue;
            foreach (var g in c.Canvas.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || g.canvasRenderer == null || g.GetComponent<Selectable>() != null) continue;
                var image = g.TryCast<Image>();
                if (image == null && g.TryCast<RawImage>() == null) continue;
                Color col = g.color;
                // D-106: every Image/RawImage that is not fully invisible is a candidate (colour no longer decides), so the receipt shows the real panel.
                if (!(col.a >= HudTranslucentPanelPolicy.MinAlpha)) continue;
                var rt = g.GetComponent<RectTransform>();
                if (rt == null) continue;
                float wf = rt.rect.width * rt.lossyScale.x / canvasW, hf = rt.rect.height * rt.lossyScale.y / canvasH;
                bool filled = image != null && image.type == Image.Type.Filled;
                string path = UiPathOf(g.transform);
                string art = image != null ? (image.sprite != null ? image.sprite.name : "no-sprite") : "raw";
                bool knownShadow = HudTranslucentPanelPolicy.IsKnownBottomShadow(path, art, filled);
                bool hide = knownShadow || HudDarkPanelPolicy.ShouldHide(col.r, col.g, col.b, col.a, wf, hf, filled) || HudTranslucentPanelPolicy.ShouldHide(col.a, wf, hf, filled);
                if (hudDarkSeen.Add(g.GetInstanceID()) && hudDarkLog.Count < 80)
                {
                    hudDarkLog.Add(UiPathOf(g.transform) + " rgba=" + col.r.ToString("0.00") + "," + col.g.ToString("0.00") + "," + col.b.ToString("0.00") + "," + col.a.ToString("0.00") + " w=" + wf.ToString("0.00") + " h=" + hf.ToString("0.00") + " art=" + art + (filled ? " filled" : "") + (hide ? " HIDDEN" : " kept"));
                }
                if (!hide) continue;
                // Exact sprite rule must work even when tint alpha is 1 or layout resets cull.
                if (knownShadow && !hudBackdropOriginal.ContainsKey(g.GetInstanceID()))
                    hudBackdropOriginal.Add(g.GetInstanceID(), (g, col.a, g.canvasRenderer.cull));
                if (!hudDarkCulled.Contains(g)) { hudDarkCulled.Add(g); HudDarkHidden++; }
                if (knownShadow) { col.a = 0f; g.color = col; }
                g.canvasRenderer.cull = true;
            }
        }
    }

    private static string UiPathOf(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    // Cap the alpha of near-full-canvas dark Images (the pause/level-up dimmer) so the scene stays visible in VR.
    // D-105: a full-canvas dark backdrop is now HIDDEN (CanvasRenderer cull, undone when the game resumes) instead of only dimmed;
    // every wide candidate (kept or hidden) is recorded in the receipt (ui.backdropCandidates) so a miss is fixable from data.
    private int uiDimmed;
    public int BackdropHidden;
    private readonly List<Graphic> uiBackdropCulled = new();
    private readonly HashSet<int> uiBackdropSeen = new();
    private readonly List<string> uiBackdropLog = new();
    private void UiDim(UiCanvas c)
    {
        if (c.Canvas == null) return;
        float canvasWidth = c.Size.x * c.Canvas.transform.lossyScale.x, canvasHeight = c.Size.y * c.Canvas.transform.lossyScale.y;
        foreach (var g in c.Canvas.GetComponentsInChildren<Graphic>(true))
        {
            if (g == null || g.GetComponent<Selectable>() != null) continue;
            var rt = g.GetComponent<RectTransform>();
            if (rt == null || rt.rect.width * rt.lossyScale.x < canvasWidth * 0.85f) continue;
            Color col = g.color;
            float wf = rt.rect.width * rt.lossyScale.x / Mathf.Max(1e-4f, canvasWidth), hf = rt.rect.height * rt.lossyScale.y / Mathf.Max(1e-4f, canvasHeight);
            var image = g.TryCast<Image>();
            bool filled = image != null && image.type == Image.Type.Filled;
            bool hide = UiBackdropPolicy.ShouldHide(col.r, col.g, col.b, col.a, wf, hf, filled);
            if (uiBackdropSeen.Add(g.GetInstanceID()) && uiBackdropLog.Count < 40)
                uiBackdropLog.Add(UiPathOf(g.transform) + " rgba=" + col.r.ToString("0.00") + "," + col.g.ToString("0.00") + "," + col.b.ToString("0.00") + "," + col.a.ToString("0.00") + " w=" + wf.ToString("0.00") + " h=" + hf.ToString("0.00") + (filled ? " filled" : "") + (hide ? " HIDDEN" : " kept"));
            if (hide)
            {
                if (g.canvasRenderer != null && !g.canvasRenderer.cull) { g.canvasRenderer.cull = true; uiBackdropCulled.Add(g); BackdropHidden++; }
                continue;
            }
            if (col.a <= UiDimMaxAlpha || Mathf.Max(col.r, Mathf.Max(col.g, col.b)) > 0.35f) continue;
            col.a = UiDimMaxAlpha;
            g.color = col;
            uiDimmed++;
        }
    }

    private void UiBackdropRestore()
    {
        foreach (var g in uiBackdropCulled) if (g != null && g.canvasRenderer != null) g.canvasRenderer.cull = false;
        uiBackdropCulled.Clear();
        UiFlattenRestore();
    }

    // D-105: reward cards turned around the vertical axis and became unreadable (user, 2026-10-10). The cause is NOT proven from the
    // assembly alone: the game has a LookAtCamera component (billboard toward the game camera, which is not the headset) and a
    // LevelUPAnimation, and any child RectTransform may carry a yaw. While the game is paused, every LookAtCamera under a Level Up
    // canvas is disabled and every child RectTransform whose world rotation differs from its canvas by more than 1 degree is forced
    // back to the canvas rotation each frame; both are undone on resume. The list of what was found is in the receipt
    // (ui.flattened) so the real offender can be confirmed after the next sitting.
    public int FlattenFound, FlattenBillboards;
    private readonly List<RectTransform> flatRects = new();
    private readonly List<Quaternion> flatOriginal = new();
    private readonly List<Canvas> flatCanvases = new();
    private readonly List<Behaviour> flatBillboards = new();
    private readonly List<string> flatLog = new();
    private readonly HashSet<int> flatSeen = new();
    private void UiFlattenScan(UiCanvas c)
    {
        if (c.Canvas == null) return;
        foreach (var look in c.Canvas.GetComponentsInChildren<LookAtCamera>(true))
        {
            if (look == null || !look.enabled) continue;
            look.enabled = false;
            flatBillboards.Add(look);
            FlattenBillboards++;
            if (flatLog.Count < 40) flatLog.Add("LookAtCamera disabled: " + UiPathOf(look.transform));
        }
        var canvasRot = c.Canvas.transform.rotation;
        foreach (var rt in c.Canvas.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt == null || ReferenceEquals(rt.gameObject, c.Canvas.gameObject)) continue;
            float angle = Quaternion.Angle(rt.rotation, canvasRot);
            if (angle <= 1f || !flatSeen.Add(rt.GetInstanceID())) continue;
            flatRects.Add(rt); flatOriginal.Add(rt.localRotation); flatCanvases.Add(c.Canvas);
            FlattenFound++;
            if (flatLog.Count < 40) flatLog.Add(UiPathOf(rt) + " angleToCanvas=" + angle.ToString("0.0") + " localEuler=" + rt.localEulerAngles.ToString("F0"));
        }
    }

    // D-108: measure every paused canvas (including the pause/relic screen in the screenshot), not only Level Up.
    // Big backdrops, culled and invisible graphics are ignored. Runs every 0.5 s; ui.levelPlacement keeps the bounds.
    private void UiMeasureLevelUp(UiCanvas c, Camera source)
    {
        if (c.Canvas == null) return;
        var rect = c.Canvas.GetComponent<RectTransform>();
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        int used = 0;
        foreach (var g in c.Canvas.GetComponentsInChildren<Graphic>(false))
        {
            if (g == null || g.canvasRenderer == null || g.canvasRenderer.cull || g.color.a < 0.02f) continue;
            var rt = g.GetComponent<RectTransform>();
            if (rt == null) continue;
            var r = rt.rect;
            float gMinX = float.MaxValue, gMinY = float.MaxValue, gMaxX = float.MinValue, gMaxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 l = rect.InverseTransformPoint(rt.TransformPoint(new Vector3(i % 2 == 0 ? r.xMin : r.xMax, i < 2 ? r.yMin : r.yMax, 0f)));
                gMinX = Mathf.Min(gMinX, l.x); gMaxX = Mathf.Max(gMaxX, l.x); gMinY = Mathf.Min(gMinY, l.y); gMaxY = Mathf.Max(gMaxY, l.y);
            }
            if (gMaxX - gMinX >= c.Size.x * 0.85f) continue;
            minX = Mathf.Min(minX, gMinX); maxX = Mathf.Max(maxX, gMaxX); minY = Mathf.Min(minY, gMinY); maxY = Mathf.Max(maxY, gMaxY);
            used++;
        }
        if (used == 0 || !UiContentCenterPolicy.TryMeasure(minX, minY, maxX, maxY, c.Size.x, c.Size.y, out float cx, out float cy)) return;
        float unit = c.Width / c.Size.x;
        float fit = UiContentCenterPolicy.FitScale((maxX - minX) * unit, (maxY - minY) * unit, UiLevelMaxContentWidth, UiLevelMaxContentHeight);
        c.CenterX = cx; c.CenterY = cy; c.Fit = fit; c.HasCenter = true;
        // Same-frame placement, even when the bounded diagnostic log has filled up.
        UiPlace(c, source, true);
        if (levelLog.Count >= 30 || (c.Logged && Mathf.Abs(cx - c.LoggedX) < c.Size.x * 0.02f && Mathf.Abs(cy - c.LoggedY) < c.Size.y * 0.02f)) return;
        c.Logged = true; c.LoggedX = cx; c.LoggedY = cy;
        Vector3 head = hasHead ? headPos : source.transform.position;
        Quaternion headQ = hasHead ? headRot : source.transform.rotation;
        Vector3 toContent = rect.TransformPoint(new Vector3(cx, cy, 0f)) - head;
        float angle = toContent.sqrMagnitude > 1e-6f ? Vector3.Angle(headQ * Vector3.forward, toContent) : 0f;
        levelLog.Add(UiPathOf(c.Canvas.transform) + " graphics=" + used + " pivot=" + rect.pivot.ToString("F2") + " anchorMin=" + rect.anchorMin.ToString("F2") + " anchorMax=" + rect.anchorMax.ToString("F2")
            + " size=" + c.Size.ToString("F0") + " boundsX=" + minX.ToString("F0") + ".." + maxX.ToString("F0") + " boundsY=" + minY.ToString("F0") + ".." + maxY.ToString("F0")
            + " centre=" + cx.ToString("F0") + "," + cy.ToString("F0") + " fit=" + fit.ToString("0.00") + " angleToGazeAfter=" + angle.ToString("0.0"));
    }

    private void UiFlattenApply()
    {
        for (int i = 0; i < flatRects.Count; i++)
        {
            var rt = flatRects[i];
            if (rt == null || flatCanvases[i] == null || !rt.gameObject.activeInHierarchy) continue;
            rt.rotation = flatCanvases[i].transform.rotation;
        }
    }

    private void UiFlattenRestore()
    {
        for (int i = 0; i < flatRects.Count; i++) if (flatRects[i] != null) flatRects[i].localRotation = flatOriginal[i];
        foreach (var b in flatBillboards) if (b != null) b.enabled = true;
        flatRects.Clear(); flatOriginal.Clear(); flatCanvases.Clear(); flatBillboards.Clear(); flatSeen.Clear();
    }

    // Headset pose in world space (set every frame by the host from the stereo eye cameras). The game camera is NOT
    // used for UI placement: it follows the mouse/stick look, which made the menus swim.
    private bool hasHead;
    private Vector3 headPos;
    private Quaternion headRot = Quaternion.identity;
    public void SetHead(Vector3 position, Quaternion rotation) { headPos = position; headRot = rotation; hasHead = true; }

    // D-108: LateUpdate order is not guaranteed relative to game animations/layout. Reapply immediately before
    // eye culling AND after canvas layout. No input/click is repeated here; both eyes use the same midpoint HMD pose.
    private static HandAimDriver? uiRenderActive;
    private Harmony? uiRenderHarmony;
    private Camera? uiSource, uiEyeLeft, uiEyeRight;
    public int UiRenderPasses, UiRenderCorrections;
    public float UiMaxDriftBefore, UiMaxErrorAfter;
    public string? UiRenderError;

    public void InstallUiRender(Camera source, Camera eyeLeft, Camera eyeRight)
    {
        if (!UiEnabled) return;
        try
        {
            uiSource = source; uiEyeLeft = eyeLeft; uiEyeRight = eyeRight;
            uiRenderHarmony = new Harmony("vhvr.handui.render");
            uiRenderHarmony.Patch(AccessTools.Method(typeof(Camera), "FireOnPreCull"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HandAimDriver), nameof(UiBeforeEye))));
            uiRenderHarmony.Patch(AccessTools.Method(typeof(Canvas), "SendWillRenderCanvases"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HandAimDriver), nameof(UiAfterLayout))));
            uiRenderActive = this;
        }
        catch (System.Exception e)
        {
            UiRenderError = "render placement unavailable; LateUpdate only: " + e.GetType().Name + ": " + e.Message;
            try { uiRenderHarmony?.UnpatchSelf(); } catch (System.Exception) { }
            uiRenderHarmony = null;
        }
    }

    private static void UiBeforeEye(Camera __0)
    {
        // D-184: mirror the working build (All.utf8.cs) exactly - marker placement runs on every
        // ui-source/eye render (before drawing), UI placement only for the two eye cameras.
        var driver = uiRenderActive;
        if (driver == null) return;
        if (__0 == driver.uiSource || __0 == driver.uiEyeLeft || __0 == driver.uiEyeRight)
        {
            driver.MarkersPlace();
            if (__0 == driver.uiEyeLeft || __0 == driver.uiEyeRight) driver.UiRenderPlace();
        }
    }

    private static void UiAfterLayout()
    {
        var driver = uiRenderActive;
        if (driver == null) return;
        driver.UiRenderPlace();
        driver.MarkersPlace();
    }

    private void UiRenderPlace()
    {
        if (!UiEnabled || uiFailed || !hasHead || uiSource == null) return;
        try
        {
            bool paused = Time.timeScale <= 0.01f;
            UiRenderPasses++;
            foreach (var c in uiCanvases.Values)
            {
                if (c.Canvas == null || !c.Canvas.gameObject.activeInHierarchy || !HudLayoutPolicy.UseHeadPose(c.HeadLocked, paused)) continue;
                var rect = c.Canvas.GetComponent<RectTransform>();
                Vector2 centre = UiCentre(c, paused);
                Vector3 target = UiHeadTarget(headPos, headRot, c.Distance);
                float before = Vector3.Distance(rect.TransformPoint(new Vector3(centre.x, centre.y, 0f)), target);
                UiMaxDriftBefore = Mathf.Max(UiMaxDriftBefore, before);
                if (before > 0.005f) UiRenderCorrections++;
                UiPlace(c, uiSource, paused);
                float after = Vector3.Distance(rect.TransformPoint(new Vector3(centre.x, centre.y, 0f)), target);
                UiMaxErrorAfter = Mathf.Max(UiMaxErrorAfter, after);
            }
            if (paused) UiFlattenApply();
            if (HideHudBackdrop) ApplyHudBackdropSuppression();
        }
        catch (System.Exception e) { UiRenderError ??= e.GetType().Name + ": " + e.Message; }
    }

    private static Vector2 UiCentre(UiCanvas c, bool paused)
    {
        if (paused && c.HasCenter) return new Vector2(c.CenterX, c.CenterY);
        var r = c.Canvas.GetComponent<RectTransform>().rect;
        return new Vector2(UiContentCenterPolicy.RectCenter(r.xMin, r.xMax), UiContentCenterPolicy.RectCenter(r.yMin, r.yMax));
    }

    // One target for tick placement, both-eye correction and controller ray selection.
    private Vector3 UiHeadTarget(Vector3 position, Quaternion rotation, float distance) =>
        position + rotation * new Vector3(0f, HudLayoutPolicy.ClampVerticalOffset(UiVerticalOffset), distance);

    // D-108: HMD pose and rectangle/content centre for every paused panel and the running HUD.
    private void UiPlace(UiCanvas c, Camera source, bool paused)
    {
        if (c.Canvas == null) return;
        c.Canvas.renderMode = RenderMode.WorldSpace;
        Vector3 pos = hasHead ? headPos : source.transform.position;
        Quaternion rot = hasHead ? headRot : source.transform.rotation;
        if (HudLayoutPolicy.UseHeadPose(c.HeadLocked, paused))
        {
            // HUD and every paused menu share the same full HMD pose, with no yaw dead zone.
            var hudRect = c.Canvas.GetComponent<RectTransform>();
            var hudParent = hudRect.parent;
            Vector3 hps = hudParent != null ? hudParent.lossyScale : Vector3.one;
            float hudWorld = c.Width / c.Size.x * (paused ? c.Fit : 1f);
            hudRect.localScale = new Vector3(hudWorld / Mathf.Max(1e-4f, Mathf.Abs(hps.x)), hudWorld / Mathf.Max(1e-4f, Mathf.Abs(hps.y)), hudWorld / Mathf.Max(1e-4f, Mathf.Abs(hps.z)));
            hudRect.position = UiHeadTarget(pos, rot, c.Distance);
            hudRect.rotation = rot;
            // D-108: even before visible bounds exist, centre the rectangle, NEVER its lower-left pivot.
            Vector2 centre = UiCentre(c, paused);
            hudRect.position = UiHeadTarget(pos, rot, c.Distance) - hudRect.TransformVector(new Vector3(centre.x, centre.y, 0f));
            c.Placed = false; // the next pause re-centres the menu on the head
            return;
        }
        float target = YawOf(rot);
        float dead = UiFollowDeadAngle;
        if (!c.Placed || (pos - c.Anchor).sqrMagnitude > UiReanchorDistance * UiReanchorDistance) { c.Yaw = target; c.Anchor = pos; }
        else
        {
            float diff = Mathf.DeltaAngle(c.Yaw, target);
            if (Mathf.Abs(diff) > dead) c.Yaw = Mathf.MoveTowardsAngle(c.Yaw, target, (Mathf.Abs(diff) - dead) * Mathf.Min(1f, Time.unscaledDeltaTime * 4f));
        }
        Quaternion facing = Quaternion.Euler(0f, c.Yaw, 0f);
        var rect = c.Canvas.GetComponent<RectTransform>();
        // World-space size: compensate any non-unit parent scale (Level Up canvases are children of the player prefab).
        var parent = rect.parent;
        Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
        float world = c.Width / c.Size.x;
        rect.localScale = new Vector3(world / Mathf.Max(1e-4f, Mathf.Abs(ps.x)), world / Mathf.Max(1e-4f, Mathf.Abs(ps.y)), world / Mathf.Max(1e-4f, Mathf.Abs(ps.z)));
        rect.position = pos + facing * Vector3.forward * c.Distance;
        rect.rotation = facing;
        c.Placed = true;
    }
    private void UiPointer(HandSlot slot, Camera source, ref bool held)
    {
        bool want = slot.SampleActive && slot.Trigger >= 0.6f;
        bool edge = want && !held;
        if (!want && slot.Trigger <= 0.35f) held = false;
        if (want) held = true;
        if (slot.State != HandAimState.Driving && slot.State != HandAimState.HoldingLastPose && !slot.SampleActive) { UiRayDraw(slot, default, default, false); return; }
        if (!slot.HasAimRay) { UiRayDraw(slot, default, default, false); return; }
        // D-105: candidates are all canvases the ray crosses INSIDE their rect; a canvas with a pressable element under the point wins
        // (UiRayPolicy). The old code took the nearest infinite plane, which parked the ray on an empty side panel (fp3).
        var cands = new List<UiCanvas>(); var dist = new List<float>(); var inside = new List<bool>(); var pressable = new List<bool>();
        var points = new List<Vector3>(); var hits = new List<Selectable?>();
        foreach (var c in uiCanvases.Values)
        {
            if (c.Canvas == null || !c.Canvas.gameObject.activeInHierarchy) continue;
            var t = c.Canvas.transform;
            var plane = new Plane(-t.forward, t.position);
            var ray = new Ray(slot.AimOrigin, slot.AimForward);
            if (!plane.Raycast(ray, out float d) || d <= 0.05f || d >= UiMaxRay) continue;
            Vector3 point = ray.GetPoint(d);
            var crect = c.Canvas.GetComponent<RectTransform>();
            bool within = crect != null && crect.rect.Contains(crect.InverseTransformPoint(point));
            Selectable? under = within ? UiHit(c.Canvas, point) : null;
            cands.Add(c); dist.Add(d); inside.Add(within); pressable.Add(under != null); points.Add(point); hits.Add(under);
        }
        int pick = UiRayPolicy.Choose(dist, inside, pressable);
        if (pick < 0) { UiRayDraw(slot, slot.AimOrigin, slot.AimOrigin + slot.AimForward * 2.5f, true, false); return; }
        UiRayDraw(slot, slot.AimOrigin, points[pick], true, true);
        Selectable? hit = hits[pick];
        if (hit != null) UiHover(hit);
        if (hit == null || !edge) return;
        UiClick(hit);
    }

    // D-095: visible pointer ray (thin cylinder) plus a dot on the canvas while a menu is open. Same unlit material as the reticle.
    private void UiRayDraw(HandSlot slot, Vector3 from, Vector3 to, bool visible, bool dot = false)
    {
        if (!visible)
        {
            if (slot.UiRay != null && slot.UiRay.activeSelf) slot.UiRay.SetActive(false);
            if (slot.UiDot != null && slot.UiDot.activeSelf) slot.UiDot.SetActive(false);
            return;
        }
        if (slot.UiRay == null || slot.UiDot == null)
        {
            if (!EnsureReticleMaterial()) return;
            Color color = ReferenceEquals(slot, left) ? LeftColor : RightColor;
            slot.UiRay = UiMakePart(PrimitiveType.Cylinder, "VHVR_UiRay_" + slot.Name, color);
            slot.UiDot = UiMakePart(PrimitiveType.Sphere, "VHVR_UiDot_" + slot.Name, color);
        }
        Vector3 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f) { UiRayDraw(slot, from, to, false); return; }
        Vector3 dir = delta / len;
        var rt = slot.UiRay.transform;
        rt.position = from + delta * 0.5f;
        rt.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        rt.localScale = new Vector3(0.006f, len * 0.5f, 0.006f);
        if (!slot.UiRay.activeSelf) slot.UiRay.SetActive(true);
        slot.UiDot.transform.position = to;
        slot.UiDot.transform.localScale = Vector3.one * 0.03f;
        if (slot.UiDot.activeSelf != dot) slot.UiDot.SetActive(dot);
    }

    private GameObject UiMakePart(PrimitiveType type, string name, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(reticleMaterial);
        renderer.sharedMaterial.color = color;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private GameObject? uiHovered;
    private void UiHover(Selectable hit)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null || ReferenceEquals(uiHovered, hit.gameObject)) return;
        es.SetSelectedGameObject(hit.gameObject);
        uiHovered = hit.gameObject; UiHovers++;
    }

    private static Selectable? UiHit(Canvas canvas, Vector3 world)
    {
        Selectable? found = null; int depth = -1;
        foreach (var s in canvas.GetComponentsInChildren<Selectable>(false))
        {
            if (s == null || !s.IsInteractable() || !s.gameObject.activeInHierarchy) continue;
            var rt = s.GetComponent<RectTransform>();
            if (rt == null || !rt.rect.Contains(rt.InverseTransformPoint(world))) continue;
            int d = 0; for (var p = s.transform; p != null; p = p.parent) d++;
            if (d > depth) { depth = d; found = s; }
        }
        return found;
    }

    private void UiClick(Selectable hit)
    {
        var button = hit.TryCast<Button>();
        if (button != null) button.onClick.Invoke();
        else if (hit.TryCast<Toggle>() == null)
        {
            // Level-up cards are not plain Buttons: select the card and let the game confirm it through its own Submit (pad A).
            UiHover(hit);
            uiSouthUntil = Time.unscaledTime + 0.2f;
        }
        else
        {
            var toggle = hit.TryCast<Toggle>();
            if (toggle == null) return;
            toggle.isOn = !toggle.isOn;
        }
        UiClicks++; UiLastClick = hit.gameObject.name;
    }

    // Hand the canvases back to the game as plain overlays.
    private void UiRelease()
    {
        if (ReferenceEquals(uiRenderActive, this)) uiRenderActive = null;
        try { uiRenderHarmony?.UnpatchSelf(); } catch (System.Exception) { }
        uiRenderHarmony = null; uiSource = null; uiEyeLeft = null; uiEyeRight = null;
        UiBackdropRestore();
        foreach (var entry in hudBackdropOriginal.Values)
        {
            if (entry.Graphic == null) continue;
            var color = entry.Graphic.color; color.a = entry.Alpha; entry.Graphic.color = color;
        }
        foreach (var g in hudDarkCulled) if (g != null && g.canvasRenderer != null) g.canvasRenderer.cull = false;
        foreach (var entry in hudBackdropOriginal.Values)
            if (entry.Graphic != null && entry.Graphic.canvasRenderer != null) entry.Graphic.canvasRenderer.cull = entry.Culled;
        hudBackdropOriginal.Clear();
        hudDarkCulled.Clear();
        foreach (var c in uiCanvases.Values)
        {
            if (c.Canvas == null) continue;
            c.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        uiCanvases.Clear();
    }
}