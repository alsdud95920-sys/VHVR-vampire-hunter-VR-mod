using UnityEngine;

namespace VampireHuntersVR.Interaction;

internal sealed partial class ShotAim
{
    public int AlignmentSamples, FlameSamples;
    public float MaxPostMuzzleToAim, MaxPostOriginDrift;
    public string? FlameError;
    // D-123 (TOOL-070): read-only flame FX sampling. This never calls the weapon's fire or effect entry points and never
    // writes a weapon field, transform or ParticleSystem. It only reads state so a later sitting can tell a repeatedly
    // restarted effect from a steadily emitting one.
    public int FlameFxFrames, VfxPlayToggles, VfxCountResets, FlamePoolSeen, FlamePoolActive, FlamePoolVfxActive, FlameProjV2Active;
    public int MinVfxParticles = int.MaxValue, MaxVfxParticles;
    public bool LastVfxPlaying;
    public string? FlameFxError;
    private int lastVfxParticles = -1;
    private readonly Dictionary<string, int> poseCounts = new();
    private DateTime nextFlame;

    private bool ShouldLog(Weapon weapon)
    {
        string key = weapon.GetIl2CppType().FullName + ":" + weapon.current_weapon_slot;
        poseCounts.TryGetValue(key, out int count);
        poseCounts[key] = count + 1;
        return count < 4 || count % 500 == 0; // first samples of EVERY type/hand, then bounded progress
    }

    private void MeasureAlignment(Weapon weapon, HandAimDriver.ShotFrame aim)
    {
        var muzzle = weapon.muzzle_flash_spawn;
        if (muzzle == null) return;
        AlignmentSamples++;
        MaxPostMuzzleToAim = Math.Max(MaxPostMuzzleToAim, Vector3.Angle(muzzle.forward, aim.Proxy.transform.forward));
        MaxPostOriginDrift = Math.Max(MaxPostOriginDrift, Vector3.Distance(muzzle.position, aim.Position));
    }

    private static string Pose(Transform? t) => t == null ? "null" :
        $"id={t.GetInstanceID()} parent={(t.parent == null ? "none" : t.parent.name + ":" + t.parent.GetInstanceID())} pos={Fmt(t.position)} fwd={Fmt(t.forward)}";

    private void PoseLog(string phase, Weapon weapon, HandAimDriver.ShotFrame aim)
    {
        try { PoseLogCore(phase, weapon, aim); }
        catch (Exception e) { FlameError = e.GetType().Name + ": " + e.Message; }
    }

    private void PoseLogCore(string phase, Weapon weapon, HandAimDriver.ShotFrame aim)
    {
        VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(
            $"[SHOT_SCOPE] phase={phase} frame={Time.frameCount} aimFrame={aim.Frame} depth={driver?.ShotScopeDepth} " +
            $"weapon={weapon.GetIl2CppType().FullName}:{weapon.GetInstanceID()} slot={weapon.current_weapon_slot} heldL={driver?.IsFireHeld(EWeaponSlot.LEFT_HAND)} heldR={driver?.IsFireHeld(EWeaponSlot.RIGHT_HAND)} " +
            $"arg={LastArg} aim=[{Pose(aim.Proxy.transform)}] muzzle=[{Pose(weapon.muzzle_flash_spawn)}] camera=[{Pose(source?.transform)}]");
        if (weapon.TryCast<Flamethrower>() is { } flame)
        {
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo($"[FLAME_POSE] phase={phase} frame={Time.frameCount} weapon={weapon.GetInstanceID()} firing={flame.already_firing} vfx=[{Pose(flame.vfx_flames_transform)}]");
            ObserveColliders(weapon.GetInstanceID(), phase);
        }
    }

    // Read existing reference-type objects, never detour draw_shoot_vfx Vector3 arguments or move attack objects.
    public void ObserveFlames()
    {
        if (DateTime.UtcNow < nextFlame) return;
        nextFlame = DateTime.UtcNow.AddSeconds(2);
        try
        {
            if (source == null || driver == null) return;
            foreach (var flame in source.GetComponentsInChildren<Flamethrower>(false))
            {
                var aim = driver.AimFor(flame);
                if (aim == null) continue;
                FlameSamples++;
                PoseLog("outside-scope", flame, aim);
                ObserveFlameFx(flame);
            }
            ObserveFlamePools();
        }
        catch (Exception e) { FlameError = e.GetType().Name + ": " + e.Message; }
    }

    // D-123 (TOOL-070): read the weapon's own flame state and its ParticleSystem. Read-only; a throw is contained here so
    // one weird frame cannot abort the remaining samples.
    private void ObserveFlameFx(Flamethrower flame)
    {
        try
        {
            FlameFxFrames++;
            string ps = "null";
            if (flame.vfx_flames is { } system)
            {
                bool playing = system.isPlaying;
                int count = system.particleCount;
                if (FlameFxFrames > 1 && playing != LastVfxPlaying) VfxPlayToggles++;
                LastVfxPlaying = playing;
                if (lastVfxParticles >= 0 && playing && count < lastVfxParticles) VfxCountResets++;
                lastVfxParticles = count;
                if (count < MinVfxParticles) MinVfxParticles = count;
                if (count > MaxVfxParticles) MaxVfxParticles = count;
                var go = system.gameObject;
                ps = $"id={system.GetInstanceID()} active={go != null && go.activeInHierarchy} playing={playing} particles={count} " +
                    $"lifetimeMax={system.main.startLifetime.constantMax:F2}";
            }
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(
                $"[FLAME_FX] frame={Time.frameCount} depth={driver?.ShotScopeDepth} dt={Time.deltaTime:F4} weapon={flame.GetInstanceID()} " +
                $"alreadyFiring={flame.already_firing} isVfxPlaying={flame.is_vfx_playing} projectileT={flame.projectile_t:F3} radius={flame.radius_size:F3} ps=[{ps}]");
        }
        catch (Exception e) { FlameFxError = e.GetType().Name + ": " + e.Message; }
    }

    // D-123 (TOOL-070): the collider probe above only looks at Flamethrower_Projectile. Flames/Flames_Projectile_V2 are the
    // pooled classes that carry the FlameVFX object, so both are counted here as well. Read-only.
    private void ObserveFlamePools()
    {
        try
        {
            int active = 0, vfxActive = 0, v2 = 0;
            foreach (var pooled in Resources.FindObjectsOfTypeAll<Flames>())
            {
                if (pooled == null) continue;
                FlamePoolSeen++;
                if (pooled.gameObject == null || !pooled.gameObject.activeInHierarchy) continue;
                active++;
                var vfx = pooled.FlameVFX;
                if (vfx != null && vfx.activeInHierarchy) vfxActive++;
            }
            foreach (var pooled in Resources.FindObjectsOfTypeAll<Flames_Projectile_V2>())
                if (pooled != null && pooled.gameObject != null && pooled.gameObject.activeInHierarchy) v2++;
            FlamePoolActive = active; FlamePoolVfxActive = vfxActive; FlameProjV2Active = v2;
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(
                $"[FLAME_POOL] frame={Time.frameCount} depth={driver?.ShotScopeDepth} flamesActive={active} flamesWithVfx={vfxActive} " +
                $"projectilesV2Active={v2} instancesSeenInSamples={FlamePoolSeen}");
        }
        catch (Exception e) { FlameFxError = e.GetType().Name + ": " + e.Message; }
    }

    private void ObserveColliders(int weaponId, string phase)
    {
        foreach (var projectile in Resources.FindObjectsOfTypeAll<Flamethrower_Projectile>())
        {
            if (projectile == null || !projectile.gameObject.activeInHierarchy || projectile.weapon == null ||
                projectile.weapon.GetInstanceID() != weaponId) continue;
            var collider = projectile.projectile_collider;
            if (collider == null) continue;
            var axis = collider.direction == 0 ? Vector3.right : collider.direction == 1 ? Vector3.up : Vector3.forward;
            float half = Math.Max(0f, collider.height * 0.5f - collider.radius);
            var a = collider.transform.TransformPoint(collider.center - axis * half);
            var b = collider.transform.TransformPoint(collider.center + axis * half);
            VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo(
                $"[FLAME_COLLIDER] phase={phase} frame={Time.frameCount} weapon={weaponId} projectile={projectile.GetInstanceID()} enabled={collider.enabled} " +
                $"pose=[{Pose(collider.transform)}] localAxis={collider.direction} radius={collider.radius:F3} height={collider.height:F3} endpointA={Fmt(a)} endpointB={Fmt(b)} burning={projectile.burning_enemies?.Count}");
        }
    }
}