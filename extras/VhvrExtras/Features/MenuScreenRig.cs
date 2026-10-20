using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VhvrExtras;

// D-178 (user decision 2026-10-15): the game's root ScreenSpaceOverlay canvases are converted to WORLD SPACE and
// parked in front of the head, so both eye cameras draw the world AND the menu UI together. No render texture, no
// extra camera, no eye masking, no shaders. This file also carries a small controller pointer: the canvases get a
// GraphicRaycaster and use the eye camera as their event camera, so a controller ray can be converted to a screen
// point and dispatched through the game's own EventSystem.
internal sealed class MenuScreenRig : IDisposable
{
    private sealed class RetargetedCanvas
    {
        internal Canvas Canvas = null!;
        internal RenderMode Mode;
        internal Camera? WorldCamera;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal Vector3 Scale;
        internal Vector2 Pivot;
        internal int SortingOrder;
        internal GraphicRaycaster? AddedRaycaster;
    }

    private const string Dll = "VhvrNativeHost.dll";

    static MenuScreenRig()
    {
        NativeHandsAbi.AssertLayout();
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ReadHands(out NativeHandsStatus status);

    // D-178: the shipped host only calls EnableHands when it installs the hand integration (gameplay). The menu
    // pointer needs controller poses, so the extras enables the native hand sampler for the menu itself.
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int EnableHands(int enabled);

    // D-186: delivery evidence for the arm-ready haptic. The native pump applies queued pulses on its render
    // thread and bumps HandsStatus.Reserved (= hapticApplied) once per applied pulse (Host.cpp: hands.reserved=
    // hapticApplied), so reading this counter before/after a request proves whether the controller really buzzed.
    // -1 = the hands snapshot is unavailable, which is itself useful evidence.
    internal static int HapticAppliedCount()
    {
        try
        {
            return ReadHands(out var status) == 0 ? status.Reserved : -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    // Mirrors VampireHuntersVR.Stage0.NativeEyePose / NativeHostStatus (the host asserts 44 / 136 bytes).
    [StructLayout(LayoutKind.Sequential)]
    private struct EyePose
    {
        public float X;
        public float Y;
        public float Z;
        public float Qx;
        public float Qy;
        public float Qz;
        public float Qw;
        public float Left;
        public float Right;
        public float Up;
        public float Down;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HostStatus
    {
        public int State;
        public int Width;
        public int Height;
        public int Frames;
        public int GpuCompleted;
        public uint MainThread;
        public uint RenderThread;
        public int AdapterMatched;
        public int Stopping;
        public int Ended;
        public double HoldSeconds;
        public EyePose Left;
        public EyePose Right;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ReadStatus(out HostStatus status);

    // D-178: the native hand sampler can only be enabled before the host's init render event, so the extras asks for
    // it from the Plugin.StartOpenXrHost prefix.
    internal static int EnableNativeHands()
    {
        try
        {
            return EnableHands(1);
        }
        catch (Exception e)
        {
            PatchRegistry.LogWarning("EnableHands failed: " + e.GetType().Name + ": " + e.Message);
            return -1;
        }
    }

    // D-178: the host submits LIVE-LOCATED views, so the eye camera transforms stay fixed and only the native eye
    // pose actually follows the headset. A panel placed from the camera transform sticks to the world.
    internal static bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        try
        {
            ReadStatus(out var status);
            var eye = status.Left;
            var q = new Quaternion(eye.Qx, eye.Qy, eye.Qz, eye.Qw);
            if (!float.IsFinite(q.x) || !float.IsFinite(q.y) || !float.IsFinite(q.z) || !float.IsFinite(q.w))
            {
                return false;
            }
            position = new Vector3(eye.X, eye.Y, eye.Z);
            rotation = q;
            var quaternionUsable = Mathf.Abs(q.x) + Mathf.Abs(q.y) + Mathf.Abs(q.z) + Mathf.Abs(q.w) > 0.5f;
            var positionUsable = position.sqrMagnitude > 1e-6f;
            return quaternionUsable || positionUsable;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // D-201: the menu panel must be placed from the LIVE head pose, never from `eyes[0].transform`.
    // Camera.CopyFrom sets the eye camera's TRANSFORM to the source camera's (Unity docs: "It will also set this
    // camera's transform to match the other camera"), so a Tick that lands before the host's first eye-pose
    // application freezes the panel at the game camera's pose. Measured on the 0.9.4 install (2026-10-18 19:57):
    // "menu ui: panel frozen at world (74.29, 34.00, 43.75)" == anchorPosition + 2 m * ArenaCamera.forward
    // (Unity yaw -90) while host.json already reported the HMD frame (view row 2 = (0, 0, -1)). Over the 2 m
    // panel distance that is the 90 degrees the user saw.
    //
    // Returns the head pose in Unity axes RELATIVE to the tracking origin (add OpenXrHost's anchorPosition, which
    // is exactly what the host's eye composition does: eye world position = anchorPosition + UnityPosition(pose)).
    // False until the native host has produced at least one located frame, so the caller simply defers placement
    // instead of freezing a wrong pose.
    internal static bool TryGetLiveHeadPose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        try
        {
            ReadStatus(out var status);
            if (status.Frames <= 0)
            {
                return false;
            }

            var pose = PoseMathPolicy.UnityPosition(status.Left.X, status.Left.Y, status.Left.Z);
            if (!PoseMathPolicy.IsFinite(pose))
            {
                return false;
            }

            if (!PoseMathPolicy.TryUnityOrientation(status.Left.Qx, status.Left.Qy, status.Left.Qz, status.Left.Qw,
                    out var unity))
            {
                return false;
            }

            position = new Vector3(pose[0], pose[1], pose[2]);
            rotation = new Quaternion(unity[0], unity[1], unity[2], unity[3]);
            return true;
        }
        catch (Exception)
        {
            // The native host is not up yet (or was torn down): treat it as "no live pose", never as a pose.
            return false;
        }
    }
    private readonly List<RetargetedCanvas> canvases = new();
    private readonly LineRenderer?[] lasers = new LineRenderer?[2];
    private Material? laserMaterial;
    private PointerEventData? pointer;
    private readonly bool[] pointerWasDown = new bool[2];
    private readonly GameObject?[] hovered = new GameObject?[2];
    private readonly GameObject?[] pressed = new GameObject?[2];
    private readonly bool[] laserLogged = new bool[2];
    private Camera? eventCamera;
    private bool restoreNeeded;
    private bool placed;
    private bool handsEnabled;
    private bool handsUnavailableLogged;

    internal bool Built { get; private set; }
    internal float WorldWidthMeters { get; private set; }
    internal float WorldHeightMeters { get; private set; }
    internal Vector3 PanelCenter { get; private set; }
    internal Vector3 PanelNormal { get; private set; }
    internal Vector3 PanelRight { get; private set; }
    internal Vector3 PanelUp { get; private set; }
    internal int PointerHoverCount { get; private set; }
    internal int PointerClicks { get; private set; }
    internal int ScrollTicks { get; private set; }
    internal string? PointerError { get; private set; }
    // D-192: thumbstick scroll speed (scroll events per frame at full stick deflection); set from config.
    internal float ScrollSpeed = 3f;
    // The menu must not add a backdrop: the game world remains visible behind the interactive menu panel.
    // Menu pointer is independently configurable; keep it explicit so a stale/partial config cannot silently hide rays.
    internal bool PointerEnabled = true;
    // D-201: the panel is placed from the live head pose; these numbers go into the log so a receipt can prove
    // the panel faces the head (headPanelDeltaDeg ~ 0) instead of the game camera (the 0.9.4 fault: 90).
    internal float HeadYawDegrees { get; private set; }
    internal float PanelYawDegrees { get; private set; }
    internal float PanelHeadAngleDegrees { get; private set; }
    // D-201: recenter chord (both thumbsticks clicked at once) - re-places the panel in front of the current head.
    internal bool RecenterChordEnabled = true;
    private bool recenterChordHeld;
    private bool recenterRequested;

    private void BuildLasers()
    {
        try
        {
            laserMaterial = new Material(FindLaserShader());
            // D-191: the laser must be OBVIOUS in the headset (the user reported "선이 안 보인다"). Solid green,
            // fully opaque, and the line is ~2x thicker than the original 8 mm hairline.
            laserMaterial.color = new Color(0.2f, 1f, 0.4f, 1f);
            for (var hand = 0; hand < 2; hand++)
            {
                var lineObject = new GameObject("VHVR_Extras_Laser" + hand);
                var line = lineObject.AddComponent<LineRenderer>();
                line.material = laserMaterial;
                line.positionCount = 2;
                line.startWidth = 0.016f;
                line.endWidth = 0.008f;
                line.startColor = new Color(0.2f, 1f, 0.4f, 1f);
                line.endColor = new Color(1f, 1f, 1f, 1f);
                line.useWorldSpace = true;
                line.enabled = false;
                lasers[hand] = line;
            }
        }
        catch (Exception e)
        {
            PointerError = "laser: " + e.GetType().Name + ": " + e.Message;
            PatchRegistry.LogWarning("menu ui laser build failed: " + PointerError);
        }
    }

    private static Shader FindLaserShader()
    {
        foreach (var name in new[] { "Unlit/Color", "Sprites/Default", "UI/Default", "Hidden/Internal-Colored" })
        {
            var shader = Shader.Find(name);
            if (shader != null)
            {
                return shader;
            }
        }
        foreach (var shader in Resources.FindObjectsOfTypeAll<Shader>())
        {
            if (shader != null)
            {
                return shader;
            }
        }
        throw new InvalidOperationException("no shader for the menu laser");
    }

    // Parks every converted canvas in front of the head and remembers the panel frame for the pointer. Called from
    // the host's Update postfix, i.e. before the eye cameras render, so the UI is always where the head is looking.
    internal void Tick(Vector3 headPosition, Quaternion headRotation, float distanceMeters, float horizontalFovDeg)
    {
        if (!Built || canvases.Count == 0)
        {
            return;
        }

        // D-178 (user decision 2026-10-15): place the panel ONCE and then leave it alone. The host's eye camera is
        // itself head-driven, so re-placing every frame made the menu follow the headset. The user wants a fixed
        // spot in the world, so the first frame's placement is frozen.
        if (placed)
        {
            return;
        }

        var forward = headRotation * Vector3.forward;
        var width = PanelGeometryPolicy.Width(distanceMeters, horizontalFovDeg);
        if (width <= 0f)
        {
            return;
        }

        // D-178 fix: a canvas shows its front on the local -Z side, so the canvas must be turned AWAY from the head
        // (the previous LookRotation(-forward) showed the player the back of the panel, i.e. mirrored text).
        var rotation = Quaternion.LookRotation(forward, headRotation * Vector3.up);
        var center = headPosition + forward * distanceMeters;

        for (var i = 0; i < canvases.Count; i++)
        {
            var entry = canvases[i];
            var canvas = entry.Canvas;
            if (canvas == null)
            {
                continue;
            }

            var rect = canvas.transform as RectTransform;
            var canvasWidth = rect != null && rect.sizeDelta.x > 1f ? rect.sizeDelta.x : Screen.width;
            canvas.transform.position = center + forward * (0.004f * i);
            canvas.transform.rotation = rotation;
            var scale = width / Mathf.Max(1f, canvasWidth);
            canvas.transform.localScale = new Vector3(scale, scale, scale);
        }

        var front = canvases[canvases.Count - 1];
        var frontRect = front.Canvas != null ? front.Canvas.transform as RectTransform : null;
        var aspect = frontRect != null && frontRect.sizeDelta.x > 1f
            ? frontRect.sizeDelta.y / frontRect.sizeDelta.x
            : 9f / 16f;
        WorldWidthMeters = width;
        WorldHeightMeters = width * aspect;
        PanelCenter = center;
        PanelNormal = -forward;
        PanelRight = rotation * Vector3.right;
        PanelUp = rotation * Vector3.up;
        // Intentionally no backdrop: this panel is an interactive world-space menu over the normal game view.
        placed = true;
        // D-201 receipt: the panel's own yaw and the head's yaw must agree. The 0.9.4 fault logged a placement
        // whose forward was ArenaCamera's (Unity yaw -90) while the view was the HMD frame.
        var panelRotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
        var headRotationArray = new[] { headRotation.x, headRotation.y, headRotation.z, headRotation.w };
        HeadYawDegrees = PoseMathPolicy.YawDegrees(headRotationArray);
        PanelYawDegrees = PoseMathPolicy.YawDegrees(panelRotation);
        PanelHeadAngleDegrees = PoseMathPolicy.ForwardAngleDegrees(headRotationArray, panelRotation);
        PatchRegistry.LogInfo("menu ui: panel frozen at world " + center.ToString("F2") +
            " size " + width.ToString("F2") + "x" + (width * aspect).ToString("F2") + "m" +
            " headYaw=" + HeadYawDegrees.ToString("F1") + " panelYaw=" + PanelYawDegrees.ToString("F1") +
            " headPanelDelta=" + PanelHeadAngleDegrees.ToString("F1") + "deg (D-201: 0 = in front of the head)");
    }

    // D-201: re-place the panel in front of where the player is looking right now. Uses the same placement path as
    // the first placement, so a recenter is exactly "place it once, from the current live head pose".
    internal bool Recenter(Vector3 headPosition, Quaternion headRotation, float distanceMeters, float horizontalFovDeg)
    {
        if (!Built || canvases.Count == 0)
        {
            return false;
        }
        placed = false;
        Tick(headPosition, headRotation, distanceMeters, horizontalFovDeg);
        if (placed)
        {
            PatchRegistry.LogInfo("menu ui: recenter (both thumbsticks) re-placed the panel in front of the head");
        }
        return placed;
    }

    // Edge-detected recenter request from UpdatePointers; consumed by the patch so the stick hold cannot spam.
    internal bool ConsumeRecenterRequest()
    {
        var requested = recenterRequested;
        recenterRequested = false;
        return requested;
    }

    internal bool Build(Camera source, Camera eye)
    {
        if (Built)
        {
            return true;
        }

        try
        {
            eventCamera = eye;
            var converted = 0;
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                var rect = canvas.transform as RectTransform;
                var size = rect != null && rect.sizeDelta.x > 1f && rect.sizeDelta.y > 1f
                    ? rect.sizeDelta
                    : new Vector2(Screen.width, Screen.height);

                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                GraphicRaycaster? added = null;
                if (raycaster == null)
                {
                    added = canvas.gameObject.AddComponent<GraphicRaycaster>();
                    raycaster = added;
                }

                canvases.Add(new RetargetedCanvas
                {
                    Canvas = canvas,
                    Mode = canvas.renderMode,
                    WorldCamera = canvas.worldCamera,
                    Position = canvas.transform.position,
                    Rotation = canvas.transform.rotation,
                    Scale = canvas.transform.localScale,
                    Pivot = rect != null ? rect.pivot : new Vector2(0.5f, 0.5f),
                    SortingOrder = canvas.sortingOrder,
                    AddedRaycaster = added,
                });

                canvas.renderMode = RenderMode.WorldSpace;
                // A World Space canvas uses worldCamera as its input (event) camera; the eye camera is the one the
                // player is actually looking through, so controller rays convert to the right screen point.
                canvas.worldCamera = eye;
                if (rect != null)
                {
                    rect.sizeDelta = size;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                }
                converted++;
            }

            if (converted == 0)
            {
                return false;
            }

            canvases.Sort((a, b) => a.SortingOrder.CompareTo(b.SortingOrder));
            BuildLasers();
            Built = true;
            restoreNeeded = true;
            PatchRegistry.LogInfo("menu ui: converted " + converted +
                " root overlay canvas(es) to world space at 1.5 m (D-178; no render texture, no eye mask)");
            return true;
        }
        catch (Exception e)
        {
            PatchRegistry.LogWarning("menu ui build failed: " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    // Controller pointer: a ray from each hand is intersected with the panel plane, converted to a screen point for
    // the eye camera and fed to the game's own EventSystem (the canvases carry a GraphicRaycaster for this).
    internal void UpdatePointers(float[] anchorOrigin, float[] anchorRotation)
    {
        if (!Built || lasers[0] == null || eventCamera == null)
        {
            recenterRequested = false;
            return;
        }

        try
        {
            if (!handsEnabled)
            {
                // result 6 = the native host rejected the request in the menu; the session may still be settling, so
                // keep retrying every frame instead of giving up after one attempt.
                var enableResult = EnableHands(1);
                if (enableResult == 0)
                {
                    handsEnabled = true;
                    PatchRegistry.LogInfo("menu ui pointer: native hands enabled");
                }
                else if (!handsUnavailableLogged)
                {
                    handsUnavailableLogged = true;
                    PatchRegistry.LogWarning("menu ui pointer: EnableHands returned " + enableResult + " - retrying every frame");
                }
            }

            var readHandsResult = ReadHands(out var status);
            if (readHandsResult != 0 || status.Available != 1)
            {
                if (!handsUnavailableLogged)
                {
                    handsUnavailableLogged = true;
                    PatchRegistry.LogWarning("menu ui pointer: ReadHands result=" + readHandsResult +
                        " available=" + status.Available + " - waiting for valid controller samples");
                }
                for (var h = 0; h < 2; h++)
                {
                    if (lasers[h] != null) lasers[h]!.enabled = false;
                }
                recenterRequested = false;
                return;
            }
            // D-201: recenter chord (both thumbsticks clicked at once). Edge-detected so holding both sticks does
            // not spam re-placements. Read before the pointer guard so recenter also works with the laser off.
            var chord = RecenterChordEnabled && status.Left.Active != 0 && status.Right.Active != 0 &&
                status.Left.StickClick != 0 && status.Right.StickClick != 0;
            recenterRequested = chord && !recenterChordHeld;
            recenterChordHeld = chord;

            if (!PointerEnabled)
            {
                return;
            }

            if (handsUnavailableLogged)
            {
                handsUnavailableLogged = false;
                PatchRegistry.LogInfo("menu ui pointer: native controller samples available; drawing menu rays");
            }

            // The laser is a visual controller-pose aid and must not depend on the game's EventSystem.
            // Some menu scenes create EventSystem one or more frames after the canvas; keep drawing the ray
            // now, and enable hover/click/scroll automatically as soon as EventSystem appears.
            if (EventSystem.current == null)
            {
                if (!laserLogged[0])
                {
                    laserLogged[0] = true;
                    PatchRegistry.LogWarning("menu ui pointer: no EventSystem yet; laser remains visible, interaction deferred");
                }
                pointer = null;
            }
            else
            {
                pointer ??= new PointerEventData(EventSystem.current);
            }
            UpdateHand(status.Left, 0, anchorOrigin, anchorRotation);
            UpdateHand(status.Right, 1, anchorOrigin, anchorRotation);
        }
        catch (Exception e)
        {
            PointerError = "pointer: " + e.GetType().Name + ": " + e.Message;
            PatchRegistry.LogWarning("menu ui pointer failed: " + PointerError);
        }
    }

    private void UpdateHand(NativeHandPose pose, int hand, float[] anchorOrigin, float[] anchorRotation)
    {
        var line = lasers[hand];
        if (line == null)
        {
            return;
        }
        if (pose.Active == 0)
        {
            line.enabled = false;
            return;
        }

        if (!PoseMathPolicy.TryToWorld(anchorOrigin, anchorRotation, pose.X, pose.Y, pose.Z, pose.Qx, pose.Qy, pose.Qz,
                pose.Qw, out var origin, out var direction))
        {
            line.enabled = false;
            return;
        }

        var originVector = new Vector3(origin[0], origin[1], origin[2]);
        // D-180: TryToWorld's second output is a ROTATION quaternion (x,y,z,w), not a direction. Using its xyz as
        // the ray made the laser run along the controller's rotation axis (vertical) instead of out of its front.
        var forwardArray = PoseMathPolicy.Forward(direction);
        var directionVector = new Vector3(forwardArray[0], forwardArray[1], forwardArray[2]).normalized;
        var triggerDown = pose.Trigger > 0.5f;

        GameObject? target = null;
        var hitPoint = originVector + directionVector * 2f;
        var denominator = Vector3.Dot(directionVector, PanelNormal);
        if (Mathf.Abs(denominator) > 1e-4f)
        {
            var distance = Vector3.Dot(PanelCenter - originVector, PanelNormal) / denominator;
            if (distance > 0f)
            {
                hitPoint = originVector + directionVector * distance;
                if (pointer != null && EventSystem.current != null)
                {
                    pointer.position = eventCamera!.WorldToScreenPoint(hitPoint);
                    var results = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, results);
                    if (results.Count > 0)
                    {
                        // D-181: prefer the first hit that belongs to an interactable Selectable.
                        target = results[0].gameObject;
                        pointer.pointerCurrentRaycast = results[0];
                        for (var i = 0; i < results.Count; i++)
                        {
                            var candidate = results[i].gameObject;
                            if (candidate == null)
                            {
                                continue;
                            }
                            var selectable = candidate.GetComponentInParent<Selectable>();
                            if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                            {
                                target = selectable.gameObject;
                                pointer.pointerCurrentRaycast = results[i];
                                break;
                            }
                        }
                        PointerHoverCount++;
                    }
                }
            }
        }

        line.SetPosition(0, originVector);
        line.SetPosition(1, hitPoint);
        line.enabled = true;

        // D-181: hover feedback - exit the previous control, enter the new one (this is what highlights a button).
        if (pointer != null && EventSystem.current != null && hovered[hand] != target)
        {
            if (hovered[hand] != null)
            {
                ExecuteEvents.ExecuteHierarchy(hovered[hand], pointer, ExecuteEvents.pointerExitHandler);
            }
            hovered[hand] = target;
            if (target != null)
            {
                ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerEnterHandler);
            }
        }

        var wasDown = pointerWasDown[hand];
        if (pointer != null && EventSystem.current != null && triggerDown && !wasDown && target != null)
        {
            pointer.pointerPress = target;
            pointer.eligibleForClick = true;
            EventSystem.current.SetSelectedGameObject(target);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            pressed[hand] = target;
        }
        else if (pointer != null && EventSystem.current != null && !triggerDown && wasDown)
        {
            var downTarget = pressed[hand];
            pressed[hand] = null;
            if (downTarget != null && downTarget == target)
            {
                ExecuteEvents.ExecuteHierarchy(downTarget, pointer, ExecuteEvents.pointerUpHandler);
                var handled = ExecuteEvents.ExecuteHierarchy(downTarget, pointer, ExecuteEvents.pointerClickHandler);
                PointerClicks++;
                PatchRegistry.LogInfo("menu ui pointer: click on '" + downTarget.name + "' handled=" + handled + " (hand " + hand + ")");
            }
            else if (downTarget != null)
            {
                ExecuteEvents.ExecuteHierarchy(downTarget, pointer, ExecuteEvents.pointerUpHandler);
            }
        }
        // D-191: menu scrolling with the thumbstick. The stick Y feeds Unity's own IScrollHandler through the
        // EventSystem, so ScrollRect lists (and anything else that implements IScrollHandler) scroll exactly the way
        // the game intended. Deadzone 0.15 so a resting stick never scrolls. ScrollSpeed = scroll events per frame at
        // full deflection (config [MenuScreen] ScrollSpeed, default 3). Stick up = scroll up.
        var stickY = pose.StickY;
        if (Mathf.Abs(stickY) > 0.15f && target != null && pointer != null && EventSystem.current != null)
        {
            pointer.scrollDelta = new Vector2(0f, stickY * ScrollSpeed);
            if (ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.scrollHandler))
            {
                ScrollTicks++;
            }
        }

        pointerWasDown[hand] = triggerDown;
    }

    public void Dispose()
    {
        for (var hand = 0; hand < 2; hand++)
        {
            if (lasers[hand] != null)
            {
                UnityEngine.Object.Destroy(lasers[hand]!.gameObject);
                lasers[hand] = null;
            }
        }
        if (laserMaterial != null)
        {
            UnityEngine.Object.Destroy(laserMaterial);
            laserMaterial = null;
        }
        if (restoreNeeded)
        {
            foreach (var entry in canvases)
            {
                var canvas = entry.Canvas;
                if (canvas == null)
                {
                    continue;
                }
                try
                {
                    if (entry.AddedRaycaster != null)
                    {
                        UnityEngine.Object.Destroy(entry.AddedRaycaster);
                    }
                    canvas.renderMode = entry.Mode;
                    canvas.worldCamera = entry.WorldCamera;
                    canvas.transform.position = entry.Position;
                    canvas.transform.rotation = entry.Rotation;
                    canvas.transform.localScale = entry.Scale;
                    if (canvas.transform is RectTransform restoreRect)
                    {
                        restoreRect.pivot = entry.Pivot;
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        if (canvases.Count != 0)
        {
            PatchRegistry.LogInfo("menu ui: restored " + canvases.Count + " canvas(es) to screen space");
        }
        canvases.Clear();
        restoreNeeded = false;
        Built = false;
        placed = false;
        recenterRequested = false;
        recenterChordHeld = false;
        PanelHeadAngleDegrees = 0f;
        HeadYawDegrees = 0f;
        PanelYawDegrees = 0f;
        handsUnavailableLogged = false;
    }
}

