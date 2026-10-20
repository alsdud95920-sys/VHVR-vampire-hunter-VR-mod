using UnityEngine;
using UnityEngine.Rendering;

namespace VampireHuntersVR.Interaction;

// D-089: per-hand aim reticle and opt-in trigger fire (partial of HandAimDriver).
// Fire goes through the game's own per-slot entry points (PlayerController.fire / fire_release with the slot of the
// hand's own weapon). No keyboard/mouse input is synthesized and no private API is used. What those entry points do with
// their `ref bool` argument is NOT known from the interop stub (shape only), so the first headset sitting must confirm it.
internal sealed partial class HandAimDriver
{
    private static readonly Color LeftColor = new Color(0.2f, 0.9f, 1f, 1f);
    private static readonly Color RightColor = new Color(1f, 0.35f, 0.1f, 1f);
    private const float ReticleMaxDistance = 60f;
    private const float ReticleNoHitDistance = 25f;
    private const float ReticleAngularSize = 0.016f; // roughly one degree of arc per unit distance
    private bool fireFailed;
    private string? fireError;
    private PlayerController? firedPlayer;
    private Material? reticleMaterial;
    private string reticleShader = "";

    // The reticle is the muzzle-forward ray hit point. It shows where the driven weapon points; it is not proof of the bullet path.
    private void UpdateReticles(Camera source)
    {
        foreach (var slot in new[] { left, right })
        {
            bool show = slot.State == HandAimState.Driving && slot.Container != null && slot.Container.gameObject.activeInHierarchy;
            Transform? muzzle = show ? FirstMuzzle(slot.Container!) : null;
            if (muzzle == null) { SetReticle(slot, false); continue; }
            Vector3 from = muzzle.position, dir = muzzle.forward;
            float distance = ReticleNoHitDistance;
            bool hit = false;
            Transform? playerRoot = source.transform.parent;
            var hits = Physics.RaycastAll(from, dir, ReticleMaxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.distance <= 0.05f || h.distance >= best) continue;
                if (playerRoot != null && h.collider.transform.IsChildOf(playerRoot)) continue;
                if (slot.Reticle != null && h.collider.transform.IsChildOf(slot.Reticle.transform)) continue;
                best = h.distance; hit = true;
            }
            if (hit) distance = best;
            slot.ReticleHit = hit; slot.ReticleDistance = distance;
            EnsureReticle(slot, ReferenceEquals(slot, left) ? LeftColor : RightColor);
            if (slot.Reticle == null) continue;
            var t = slot.Reticle.transform;
            t.position = from + dir * (distance - 0.02f);
            float size = Mathf.Max(0.04f, distance * ReticleAngularSize);
            t.localScale = new Vector3(size, size, size);
            SetReticle(slot, true);
        }
    }

    private void SetReticle(HandSlot slot, bool visible)
    {
        slot.ReticleVisible = visible && slot.Reticle != null;
        if (slot.Reticle != null && slot.Reticle.activeSelf != visible) slot.Reticle.SetActive(visible);
    }

    private void EnsureReticle(HandSlot slot, Color color)
    {
        if (slot.Reticle != null) return;
        if (reticleMaterial == null)
        {
            Shader? shader = null;
            foreach (var name in new[] { "Hidden/Internal-Colored", "Sprites/Default", "Unlit/Color", "UI/Default" })
            {
                shader = Shader.Find(name);
                if (shader != null) { reticleShader = name; break; }
            }
            if (shader == null) { lastError = "reticle: no unlit shader found"; return; }
            reticleMaterial = new Material(shader);
            if (reticleMaterial.HasProperty("_ZTest")) reticleMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            if (reticleMaterial.HasProperty("_ZWrite")) reticleMaterial.SetInt("_ZWrite", 0);
            reticleMaterial.renderQueue = 5000;
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "VHVR_Reticle_" + slot.Name;
        var collider = go.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(reticleMaterial);
        renderer.sharedMaterial.color = color;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        slot.Reticle = go;
    }

    private void DestroyReticles()
    {
        foreach (var slot in new[] { left, right })
        {
            if (slot.Reticle != null) UnityEngine.Object.Destroy(slot.Reticle);
            slot.Reticle = null; slot.ReticleVisible = false;
        }
        if (reticleMaterial != null) UnityEngine.Object.Destroy(reticleMaterial);
        reticleMaterial = null;
    }

    // Each hand fires only its own slot. The upper face button is reserved for the secondary-fire selector (not wired):
    // while it is held this hand never fires the primary.
    private void FireTick(PlayerController player)
    {
        firedPlayer = player;
        bool allowedWorld = !fireFailed && Time.timeScale > 0.01f && player != null && !player.lock_shoot_input;
        foreach (var slot in new[] { left, right })
        {
            bool allowed = allowedWorld && slot.State == HandAimState.Driving && slot.WeaponCount > 0 && !slot.Face;
            var edge = HandFirePolicy.Next(slot.FireHeld, slot.Trigger, allowed, out bool held);
            slot.FireHeld = held;
            try
            {
                if (held)
                {
                    bool pressed = true;
                    player!.fire(slot.Slot, ref pressed, Time.deltaTime);
                    slot.FireCalls++;
                }
                if (edge == TriggerEdge.Released)
                {
                    bool released = true;
                    player!.fire_release(slot.Slot, ref released);
                    slot.ReleaseCalls++;
                }
            }
            catch (System.Exception e)
            {
                fireFailed = true; fireError = e.GetType().Name + ": " + e.Message;
                slot.FireHeld = false;
                return;
            }
        }
    }

    // Release best effort so a held trigger never leaves a weapon firing after the session or driver ends.
    private void ReleaseFireBestEffort()
    {
        var player = firedPlayer;
        foreach (var slot in new[] { left, right })
        {
            if (!slot.FireHeld) continue;
            slot.FireHeld = false;
            try
            {
                if (player != null) { bool released = true; player.fire_release(slot.Slot, ref released); slot.ReleaseCalls++; }
            }
            catch (System.Exception) { }
        }
        firedPlayer = null;
    }
}
