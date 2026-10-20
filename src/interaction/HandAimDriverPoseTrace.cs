using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-127 (TOOL-071): the per-frame pose-trace hook. Read-only wrapper around PoseTrace; it resolves the nodes to watch
// (the container the driver writes, the weapon prefab and its muzzle inside it, the aim proxy and the camera) at most
// once a second and lets PoseTrace do the sampling. No transform is written here and no behavior depends on it.
internal sealed partial class HandAimDriver
{
    private readonly PoseTrace leftPose = new();
    private readonly PoseTrace rightPose = new();
    private DateTime nextPoseScan;

    private void TracePose(Camera source)
    {
        bool rescan = DateTime.UtcNow >= nextPoseScan;
        if (rescan) nextPoseScan = DateTime.UtcNow.AddSeconds(1);
        TraceHand(left, leftPose, source, rescan);
        TraceHand(right, rightPose, source, rescan);
    }

    private void TraceHand(HandSlot slot, PoseTrace trace, Camera source, bool rescan)
    {
        var container = slot.Container;
        if (container == null || rescan || trace.Container.Target != container)
        {
            trace.WeaponTransform = null; trace.MuzzleTransform = null; trace.AimTransform = null;
            if (container != null)
                foreach (var weapon in container.GetComponentsInChildren<Weapon>(false))
                {
                    if (weapon == null) continue;
                    trace.WeaponTransform = weapon.transform;
                    trace.MuzzleTransform = weapon.muzzle_flash_spawn;
                    trace.AimTransform = AimFor(weapon)?.Proxy.transform;
                    break;
                }
        }
        bool driving = slot.State == HandAimState.Driving || slot.State == HandAimState.HoldingLastPose;
        trace.Observe(slot.Name, container, source.transform, slot.LastWrittenRotation,
            slot.HasWritten, ShotScopeDepth, driving);
    }
}