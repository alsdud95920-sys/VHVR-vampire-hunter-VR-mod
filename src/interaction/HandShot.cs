using HarmonyLib;
using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-110 (TOOL-044/063): detached per-weapon hand aim frames and parent-safe camera transactions.
// shoot_forward's trailing Transform is a candidate aim input, NOT proof of actual bullet direction.
// Vector3 tracer detours remain OFF (D-097 crash). Headset/attack acceptance still required.
internal sealed partial class ShotAim
{
    public static ShotAim? Active;
    private Harmony? harmony;
    private Camera? source;
    private HandAimDriver? driver;
    public bool OverrideArg;
    public int ShootCalls, ArgNull, ArgCamera, ArgCameraChild, ArgOther, Overrides;
    public int Tracers, TracerNearMuzzle, TracerNearCamera;
    // D-100 ammo drain measurement (user: 39-round weapon empties after about 10 shots in VR, not on desktop).
    public int AmmoDrops, MaxShootsPerFrame, AmmoWeapons;
    public float AmmoDropTotal, MaxAmmoDrop, LastAmmo = float.NaN, FirstAmmo = float.NaN, WeaponMaxAmmo = float.NaN, WeaponFireRate = float.NaN;
    private readonly Dictionary<int, float> ammoByWeapon = new();
    private int lastShootFrame = -1, shootsThisFrame;
    public string? LastArg, Error;
    public Vector3 LastFrom, LastTo;
    public float LastTracerToMuzzle = float.NaN, LastTracerToCamera = float.NaN, MaxTracerToMuzzle;
    private int logged;

    // D-097: the draw_shoot_vfx prefix crashed the game (ha3, ha7: c0000005 in PrefixTracer, reading the Vector3 argument
    // through a null pointer; value-type arguments are not delivered by the Il2Cpp detour). The tracer measurement is therefore
    // OFF by default (tracer=false) and only shoot_forward (reference-type Transform argument) is patched.
    public bool TracerPatched;
    public bool Install(Camera camera, HandAimDriver aimDriver, bool overrideArg, bool tracer = false)
    {
        try
        {
            source = camera; driver = aimDriver; OverrideArg = overrideArg;
            var h = new Harmony("vhvr.shotaim");
            var shoot = AccessTools.Method(typeof(Weapon), "shoot_forward");
            var vfx = tracer ? AccessTools.Method(typeof(Weapon), "draw_shoot_vfx") : null;
            if (shoot == null || (tracer && vfx == null)) { Error = "target method missing"; return false; }
            h.Patch(shoot, prefix: new HarmonyMethod(typeof(ShotAim).GetMethod(nameof(PrefixShoot), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)),
                finalizer: new HarmonyMethod(typeof(ShotAim).GetMethod(nameof(FinalizeShoot), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
            if (vfx != null)
            {
                h.Patch(vfx, prefix: new HarmonyMethod(typeof(ShotAim).GetMethod(nameof(PrefixTracer), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
                TracerPatched = true;
            }
            harmony = h; Active = this;
            return true;
        }
        catch (System.Exception e) { Error = e.GetType().Name + ": " + e.Message; return false; }
    }

    public void Remove()
    {
        Active = null;
        try { harmony?.UnpatchSelf(); } catch (System.Exception) { }
        harmony = null;
    }

    private bool Mine(Weapon weapon)
    {
        if (weapon == null || source == null) return false;
        var slot = weapon.current_weapon_slot;
        return (slot == EWeaponSlot.LEFT_HAND || slot == EWeaponSlot.RIGHT_HAND) && weapon.transform.IsChildOf(source.transform);
    }

    private void Log(string text)
    {
        if (logged++ < 40) VampireHuntersVR.Stage0.OpenXrHost.Logger?.LogInfo("[SHOT] " + text);
    }

    // D-100: read-only measurement. Each shoot_forward call compares the weapon's current_ammo with the value seen at its previous call;
    // a drop is the cost of the previous shot. AmmoDrops/ShootCalls near 1 = one ammo per shoot call; MaxShootsPerFrame > 1 means
    // the game fires several shoot_forward calls in one Unity frame (catch-up when the frame rate is below the weapon's fire rate).
    private void RecordAmmo(Weapon weapon)
    {
        int frame = Time.frameCount;
        if (frame != lastShootFrame) { lastShootFrame = frame; shootsThisFrame = 0; }
        if (++shootsThisFrame > MaxShootsPerFrame) MaxShootsPerFrame = shootsThisFrame;
        float ammo = weapon.current_ammo;
        if (float.IsNaN(ammo) || float.IsInfinity(ammo)) return;
        int id = weapon.GetInstanceID();
        if (!ammoByWeapon.TryGetValue(id, out float before))
        {
            AmmoWeapons++;
            if (float.IsNaN(FirstAmmo)) FirstAmmo = ammo;
            try { WeaponMaxAmmo = weapon.max_ammo(); WeaponFireRate = weapon.fire_rate(); } catch (System.Exception) { }
        }
        else if (ammo < before) { AmmoDrops++; AmmoDropTotal += before - ammo; if (before - ammo > MaxAmmoDrop) MaxAmmoDrop = before - ammo; }
        ammoByWeapon[id] = ammo; LastAmmo = ammo;
    }

    // __8 = trailing Transform; invocation-local __state prevents an unowned nested call popping an outer scope.
    public int CameraAimedShots, MissingFrames;
    public int LeftShootCalls, RightShootCalls, LeftShotsWithoutHeld, RightShotsWithoutHeld;
    public float LastCameraToMuzzleDeg = float.NaN, MaxCameraToMuzzleDeg;
    private sealed class Invocation
    {
        public IDisposable Scope = null!;
        public ShotAim Owner = null!;
        public HandAimDriver.ShotFrame Aim = null!;
        public bool Log;
    }

    private static bool PrefixShoot(Weapon __instance, ref Transform? __8, out Invocation? __state)
    {
        __state = null;
        var s = Active;
        if (s == null) return true;
        try
        {
            if (!s.Mine(__instance)) return true;
            s.ShootCalls++;
            bool held = s.driver?.IsFireHeld(__instance.current_weapon_slot) == true;
            if (__instance.current_weapon_slot == EWeaponSlot.LEFT_HAND)
            {
                s.LeftShootCalls++;
                if (!held) s.LeftShotsWithoutHeld++;
            }
            else
            {
                s.RightShootCalls++;
                if (!held) s.RightShotsWithoutHeld++;
            }
            s.RecordAmmo(__instance);
            var arg = __8; var camera = s.source!.transform; var muzzle = __instance.muzzle_flash_spawn;
            string kind;
            if (arg == null) { kind = "null"; s.ArgNull++; }
            else if (arg == camera) { kind = "camera"; s.ArgCamera++; }
            else if (arg.IsChildOf(camera)) { kind = "camera-child"; s.ArgCameraChild++; }
            else { kind = "other"; s.ArgOther++; }
            s.LastArg = kind + ":" + (arg == null ? "" : arg.name);
            var aim = s.driver?.AimFor(__instance);
            if (!s.OverrideArg) return true;
            if (aim == null) { s.MissingFrames++; return true; } // explicitly counted fallback, no stale transform write
            bool replace = kind == "null" || kind == "camera" || arg == muzzle;
            bool diagnostic = s.ShouldLog(__instance);
            if (diagnostic) s.PoseLog("pre", __instance, aim);
            float off = muzzle == null ? 0f : Vector3.Angle(camera.forward, muzzle.forward);
            s.LastCameraToMuzzleDeg = off; s.MaxCameraToMuzzleDeg = Math.Max(off, s.MaxCameraToMuzzleDeg);
            var scope = s.driver!.BeginShotScope(s.source!, aim);
            __state = new Invocation { Owner = s, Scope = scope, Aim = aim, Log = diagnostic };
            if (replace) { __8 = aim.Proxy.transform; s.Overrides++; }
            s.CameraAimedShots++;
            s.MeasureAlignment(__instance, aim);
            if (diagnostic) s.PoseLog("post", __instance, aim);
        }
        catch (System.Exception e) { s.Error = e.GetType().Name + ": " + e.Message; }
        return true;
    }

    private static void FinalizeShoot(Weapon __instance, Invocation? __state)
    {
        if (__state == null) return;
        var s = __state.Owner; // restore even if Active/ownership changed or the original call threw
        try
        {
            if (__state.Log) s.PoseLog("before-restore", __instance, __state.Aim);
        }
        catch (System.Exception e) { s.Error = e.GetType().Name + ": " + e.Message; }
        finally
        {
            try { __state.Scope.Dispose(); }
            catch (System.Exception e) { s.Error = e.GetType().Name + ": " + e.Message; }
        }
        try { if (__state.Log) s.PoseLog("restored", __instance, __state.Aim); }
        catch (System.Exception e) { s.Error = e.GetType().Name + ": " + e.Message; }
    }

    private static bool PrefixTracer(Weapon __instance, Vector3 __0, Vector3 __1)
    {
        var s = Active;
        if (s == null) return true;
        try
        {
            if (!s.Mine(__instance)) return true;
            var muzzle = __instance.muzzle_flash_spawn;
            Vector3 dir = __1 - __0;
            if (muzzle == null || dir.sqrMagnitude < 0.01f) return true;
            s.Tracers++; s.LastFrom = __0; s.LastTo = __1;
            float toMuzzle = Vector3.Angle(dir, muzzle.forward), toCamera = Vector3.Angle(dir, s.source!.transform.forward);
            s.LastTracerToMuzzle = toMuzzle; s.LastTracerToCamera = toCamera;
            if (toMuzzle > s.MaxTracerToMuzzle) s.MaxTracerToMuzzle = toMuzzle;
            if (toMuzzle < 6f) s.TracerNearMuzzle++;
            if (toCamera < 6f) s.TracerNearCamera++;
            s.Log($"tracer slot={__instance.current_weapon_slot} from={Fmt(__0)} to={Fmt(__1)} angleToMuzzle={toMuzzle:F1} angleToCamera={toCamera:F1}");
        }
        catch (System.Exception e) { s.Error = e.GetType().Name + ": " + e.Message; }
        return true;
    }

    private static string Fmt(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
}