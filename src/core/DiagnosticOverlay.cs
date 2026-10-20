using BepInEx.Logging;
using UnityEngine;

namespace VampireHuntersVR.Stage0;

// Stage 0 visible diagnostic (D-044). Draws exactly one IMGUI box in the top-left corner and logs the
// first frame it drew. Read-only: no game object, camera, input, renderer or XR API is touched, so the
// flat baseline the gate measures cannot change. The text follows the validated DiagnosticsEnabled flag.
// Failure is made detectable on purpose: OVERLAY_VISIBLE_FIRST_FRAME on the first OnGUI, or
// OVERLAY_NOT_CALLED after 10 s without one, so a missing label can never read as a pass.
public sealed class DiagnosticOverlay : MonoBehaviour
{
    public static ManualLogSource? Logger;
    public static volatile bool Visible;
    public static string Status = "starting";

    private const float GraceSeconds = 10f;
    private const int MaxDrawFrames = 120000;

    private bool updateSeen;
    private bool onGuiSeen;
    private bool reported;
    private float startedAt;
    private int frames;

    private void Update()
    {
        try
        {
            if (!updateSeen) { updateSeen = true; startedAt = Time.realtimeSinceStartup; }
            if (onGuiSeen || reported) return;
            if (Time.realtimeSinceStartup - startedAt < GraceSeconds) return;
            reported = true;
            Logger?.LogWarning("[Stage0] OVERLAY_NOT_CALLED: OnGUI was not invoked within 10 s of the first Update. " +
                "The visible diagnostic is NOT confirmed on this build; use the Canvas/TextMeshPro fallback before claiming criterion 3.");
        }
        catch (Exception e)
        {
            // A diagnostic must never disturb the game.
            Logger?.LogWarning("[Stage0] OVERLAY_UPDATE_FAILED; label may be missing, gameplay unaffected: " + e.GetType().Name);
        }
    }

    private void OnGUI()
    {
        try
        {
            if (!onGuiSeen)
            {
                onGuiSeen = true;
                Logger?.LogInfo("[Stage0] OVERLAY_VISIBLE_FIRST_FRAME: IMGUI OnGUI invoked; top-left label drawn, no camera/weapon change");
            }
            if (!Visible) return;
            if (++frames > MaxDrawFrames) return;
            GUI.Box(new Rect(10f, 10f, 660f, 56f), "[VHVR Stage 0] " + Status + "  | diagnostic only, no VR");
        }
        catch (Exception e)
        {
            if (reported) return;
            reported = true;
            Logger?.LogWarning("[Stage0] OVERLAY_DRAW_FAILED: " + e.GetType().Name + ": " + e.Message);
        }
    }
}