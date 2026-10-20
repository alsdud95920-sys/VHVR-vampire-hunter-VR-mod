namespace VampireHuntersVR.Interaction;

internal sealed partial class HandAimDriver
{
    private ByteBranchPatch? flameEndpoint;
    // Host discards a HandAimDriver after Release; a failed restoration must outlive that instance.
    private static ByteBranchPatch? recoveryPatch;
    private bool flameEndpointRequested;
    private string? flameEndpointError;

    public void InstallFlameEndpoint(bool enabled)
    {
        flameEndpointRequested = enabled;
        try
        {
            if (flameEndpoint?.Active == true) throw new InvalidOperationException("Flame endpoint patch already owned");
            recoveryPatch?.Dispose(); // retry an earlier failed release before any new application
            recoveryPatch = null;
            if (!enabled) return;
            flameEndpoint = FlameEndpointMemory.Prepare(); // retain owner even if apply AND rollback fail
            recoveryPatch = flameEndpoint;
            flameEndpoint.Apply();
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(
                "[FLAME_ENDPOINT] memory branch applied: native same-call end-start; NOT_LIVE_OR_VISUALLY_ACCEPTED");
        }
        catch (Exception e)
        {
            flameEndpointError = e.ToString();
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogError("[FLAME_ENDPOINT] APPLY_FAILED active=" +
                (flameEndpoint?.Active == true) + " " + flameEndpointError);
            // Explicit visual fallback only; never disable or alter accepted hand aim/fire.
        }
    }

    private object FlameEndpointReceipt() => new
    {
        enabled = flameEndpointRequested, memoryPatchActive = flameEndpoint?.Applied == true,
        restorationOwned = flameEndpoint?.Active == true || recoveryPatch?.Active == true,
        error = flameEndpointError, contextRva = "46BF1D", branchRva = "46BF2B",
        directionSource = "native draw_shoot_vfx end-start, not raw hand proxy or target_hit",
        acceptance = "NOT_GAME_OR_HEADSET_VERIFIED"
    };

    private void ReleaseFlameEndpoint()
    {
        try
        {
            (flameEndpoint ?? recoveryPatch)?.Dispose();
            if (recoveryPatch == flameEndpoint) recoveryPatch = null;
            if (recoveryPatch?.Active != true) recoveryPatch = null;
            flameEndpoint = null;
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo("[FLAME_ENDPOINT] original branch restored or not applied");
        }
        catch (Exception e)
        {
            flameEndpointError = e.ToString(); // retain owner for a later restoration retry
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogError("[FLAME_ENDPOINT] RESTORE_FAILED " + flameEndpointError);
        }
    }
}