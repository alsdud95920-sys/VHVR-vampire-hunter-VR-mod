using UnityEngine;
using UnityEngine.Rendering;

namespace VampireHuntersVR.Interaction;

// D-089: per-hand aim reticle (partial of HandAimDriver). The fire half lives in HandFireTrigger.cs.
// The reticle is the muzzle-forward ray hit point. It shows where the driven weapon points; it is not proof of the bullet path.
internal sealed partial class HandAimDriver
{
    private static readonly Color LeftColor = new Color(0.2f, 0.9f, 1f, 1f);
    private static readonly Color RightColor = new Color(1f, 0.35f, 0.1f, 1f);
    private const float ReticleMaxDistance = 60f;
    private const float ReticleNoHitDistance = 25f;
    private const float ReticleAngularSize = 0.016f;
    private Material? reticleMaterial;
    private string reticleShader = "";

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

    // D-095: one unlit, always-on-top material shared by the reticle spheres and the UI ray.
    private bool EnsureReticleMaterial()
    {
        if (reticleMaterial != null) return true;
        Shader? shader = null;
        foreach (var name in new[] { "Hidden/Internal-Colored", "Sprites/Default", "Unlit/Color", "UI/Default" })
        {
            shader = Shader.Find(name);
            if (shader != null) { reticleShader = name; break; }
        }
        if (shader == null) { lastError = "reticle: no unlit shader found"; return false; }
        reticleMaterial = new Material(shader);
        if (reticleMaterial.HasProperty("_ZTest")) reticleMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        if (reticleMaterial.HasProperty("_ZWrite")) reticleMaterial.SetInt("_ZWrite", 0);
        reticleMaterial.renderQueue = 5000;
        return true;
    }

    private void EnsureReticle(HandSlot slot, Color color)
    {
        if (slot.Reticle != null) return;
        if (!EnsureReticleMaterial()) return;
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
            if (slot.UiRay != null) UnityEngine.Object.Destroy(slot.UiRay);
            if (slot.UiDot != null) UnityEngine.Object.Destroy(slot.UiDot);
            slot.UiRay = null; slot.UiDot = null;
        }
        if (reticleMaterial != null) UnityEngine.Object.Destroy(reticleMaterial);
        reticleMaterial = null;
    }
}