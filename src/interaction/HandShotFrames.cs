using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-110: publish ONLY after both hands are driven, never from a child inside a camera override.
internal sealed partial class HandAimDriver
{
    internal sealed class ShotFrame
    {
        public Weapon Weapon = null!;
        public GameObject Proxy = null!;
        public Vector3 Position;
        public Quaternion Rotation;
        public int Frame;
    }
    private readonly Dictionary<int, ShotFrame> shotFrames = new();
    private readonly List<Transform> shotRoots = new();
    internal int ShotScopeDepth, ShotScopeMaxDepth, ShotScopeCalls, ShotScopeFailures;
    internal string? ShotScopeError;

    private void PublishShotFrames(Camera source)
    {
        var seen = new HashSet<int>();
        shotRoots.Clear();
        foreach (var weapon in source.GetComponentsInChildren<Weapon>(false))
        {
            if (weapon == null) continue;
            var root = weapon.transform;
            while (root.parent != null && root.parent != source.transform) root = root.parent;
            if (root != source.transform && root.parent == source.transform && !shotRoots.Contains(root)) shotRoots.Add(root);
            var slot = weapon.current_weapon_slot == EWeaponSlot.LEFT_HAND ? left :
                weapon.current_weapon_slot == EWeaponSlot.RIGHT_HAND ? right : null;
            if (slot == null || slot.State != HandAimState.Driving || !slot.HasAimRay ||
                slot.SharedOrAmbiguous || slot.Container != root || weapon.muzzle_flash_spawn == null) continue;
            int id = weapon.GetInstanceID();
            seen.Add(id);
            if (!shotFrames.TryGetValue(id, out var aim))
            {
                aim = new ShotFrame { Weapon = weapon, Proxy = new GameObject("VHVR_Aim_" + slot.Name + "_" + id) };
                shotFrames.Add(id, aim); // no parent, renderer or collider
            }
            aim.Position = weapon.muzzle_flash_spawn.position;
            aim.Rotation = slot.AimRotation;
            aim.Frame = Time.frameCount;
            aim.Proxy.transform.SetPositionAndRotation(aim.Position, aim.Rotation);
        }
        foreach (int id in shotFrames.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            UnityEngine.Object.Destroy(shotFrames[id].Proxy);
            shotFrames.Remove(id);
        }
    }

    internal ShotFrame? AimFor(Weapon weapon)
    {
        if (weapon == null || !weapon.gameObject.activeInHierarchy) return null;
        if (!shotFrames.TryGetValue(weapon.GetInstanceID(), out var aim) || aim.Weapon != weapon ||
            Time.frameCount - aim.Frame > HandAimPolicy.FreshTicks) return null;
        var slot = weapon.current_weapon_slot == EWeaponSlot.LEFT_HAND ? left :
            weapon.current_weapon_slot == EWeaponSlot.RIGHT_HAND ? right : null;
        return slot != null && slot.State == HandAimState.Driving && slot.SampleActive &&
            !slot.SharedOrAmbiguous && slot.Container != null && weapon.transform.IsChildOf(slot.Container) ? aim : null;
    }

    internal IDisposable BeginShotScope(Camera camera, ShotFrame aim)
    {
        var parent = camera.transform;
        var roots = shotRoots.Where(t => t != null && t.IsChildOf(parent)).ToArray();
        WorldPoseScope<(Vector3, Quaternion)> transaction;
        try
        {
            transaction = WorldPoseScope<(Vector3, Quaternion)>.Enter(
                () => (parent.position, parent.rotation), p => parent.SetPositionAndRotation(p.Item1, p.Item2),
                roots.Select(t => ((Func<(Vector3, Quaternion)>)(() => (t.position, t.rotation)),
                    (Action<(Vector3, Quaternion)>)(p => t.SetPositionAndRotation(p.Item1, p.Item2)))),
                (aim.Position, aim.Rotation));
        }
        catch (Exception e) { ShotScopeFailures++; ShotScopeError = e.Message; throw; }
        ShotScopeCalls++;
        ShotScopeDepth++;
        ShotScopeMaxDepth = Math.Max(ShotScopeMaxDepth, ShotScopeDepth);
        return new ScopeExit(this, transaction);
    }

    private sealed class ScopeExit : IDisposable
    {
        private HandAimDriver? owner;
        private readonly IDisposable transaction;
        internal ScopeExit(HandAimDriver owner, IDisposable transaction) { this.owner = owner; this.transaction = transaction; }
        public void Dispose()
        {
            var driver = owner;
            if (driver == null) return;
            owner = null;
            try { transaction.Dispose(); }
            catch (Exception e) { driver.ShotScopeFailures++; driver.ShotScopeError = e.Message; throw; }
            finally { driver.ShotScopeDepth--; }
        }
    }

    private void ReleaseShotFrames()
    {
        foreach (var aim in shotFrames.Values) if (aim.Proxy != null) UnityEngine.Object.Destroy(aim.Proxy);
        shotFrames.Clear();
        shotRoots.Clear();
    }
}