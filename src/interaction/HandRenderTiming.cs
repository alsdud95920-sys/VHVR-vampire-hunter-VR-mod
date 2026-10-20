using HarmonyLib;
using System.Text.Json;
using UnityEngine;

namespace VampireHuntersVR.Interaction;

// D-136/D-138: observations only. No aim, animation, particle, camera, input, or native ABI writes.
internal sealed partial class HandAimDriver
{
    private RenderTimingDiagnostics? renderTiming;
    private string? renderTimingError;
    public void InstallRenderTiming(Camera source, Camera eyeLeft, Camera eyeRight, string directory)
    {
        try { renderTiming = new RenderTimingDiagnostics(source, eyeLeft, eyeRight, directory, this); }
        catch (Exception e) { renderTimingError = e.GetType().Name + ": " + e.Message; throw; }
    }
    public void SetRenderTimingPhase(int phase, int submitted, int until) => renderTiming?.SetPhase(phase, submitted, until);

    // D-178 recovery repair (found while verifying D-206): Release() released every other subsystem but not this one, so
    // the Camera FireOnPreCull/FireOnPreRender patches and the bounded writer stayed installed after a release. The
    // RenderTiming fixture "release-closes-diagnostics" pins the dispose - it lives here with the fields (file split).
    private void ReleaseRenderTiming()
    {
        renderTiming?.Dispose(); renderTiming = null;
    }
    private void ObserveHandRead(int frame, int read, NativeHandsStatus status) => renderTiming?.HandRead(frame, read, status);
    private void ObserveHandApplied() => renderTiming?.Applied();
    private void ObservePlayerPhase(string phase) => renderTiming?.Capture(phase, null);
    private object? RenderTimingReceipt() => new { installed = renderTiming != null, error = renderTimingError, status = renderTiming?.Receipt() };

    private sealed class RenderTimingDiagnostics : IDisposable
    {
        private static RenderTimingDiagnostics? active;
        private readonly Camera source, eyeLeft, eyeRight;
        private readonly HandAimDriver driver;
        private readonly BoundedDiagnosticWriter writer;
        private readonly Harmony harmony = new("vhvr.render-timing.d136");
        private readonly List<(Transform Node, string Role)> nodes = new();
        private readonly List<ParticleSystem> particles = new();
        private readonly List<Renderer> renderers = new();
        private readonly double started = Time.realtimeSinceStartupAsDouble;
        private double nextScan;
        private int tickFrame = -1, readResult = -1, readSeq, lastValidSeq, appliedFrame = -1;
        private int hostPhase, submitted = -1, renderUntil, serial, inventoryTruncated;
        private int leftDrivenBefore, rightDrivenBefore;
        private string? error;
        private bool completed;

        public RenderTimingDiagnostics(Camera source, Camera eyeLeft, Camera eyeRight, string directory, HandAimDriver driver)
        {
            this.source = source; this.eyeLeft = eyeLeft; this.eyeRight = eyeRight; this.driver = driver;
            writer = new BoundedDiagnosticWriter(Path.Combine(directory, "render-timing.jsonl"));
            try
            {
                if (active != null) throw new InvalidOperationException("render timing already installed");
                harmony.Patch(AccessTools.Method(typeof(Camera), "FireOnPreCull"),
                    postfix: new HarmonyMethod(typeof(RenderTimingDiagnostics), nameof(BeforeCull)));
                harmony.Patch(AccessTools.Method(typeof(Camera), "FireOnPreRender"),
                    postfix: new HarmonyMethod(typeof(RenderTimingDiagnostics), nameof(BeforeRender)));
                active = this;
                Record(new { kind = "header", version = 1, utc = DateTime.UtcNow.ToString("o"), pid = Environment.ProcessId,
                    durationSeconds = 180, maxNodes = 24, maxRenderers = 16, maxParticles = 8,
                    maxBytes = 268435456, maxRows = 100000, sourceCamera = source.GetInstanceID(),
                    leftCamera = eyeLeft.GetInstanceID(), rightCamera = eyeRight.GetInstanceID(),
                    evidence = "READ_ONLY_NOT_ROOTCAUSE_NOT_HEADSET_ACCEPTANCE", particleVelocityMeasured = false,
                    particleSpaceMeasured = false, particleSpaceUnavailable = ParticleSpaceUnavailable });
            }
            catch
            {
                if (active == this) active = null;
                try { harmony.UnpatchSelf(); }
                finally { writer.Complete(); }
                throw;
            }
        }
        private static void BeforeCull(Camera __0) => active?.Capture("pre-cull", __0);
        private static void BeforeRender(Camera __0) => active?.Capture("pre-render", __0);
        public void SetPhase(int phase, int frames, int until) { hostPhase = phase; submitted = frames; renderUntil = until; }
        public void HandRead(int frame, int read, NativeHandsStatus status)
        {
            leftDrivenBefore = driver.left.DrivenFrames; rightDrivenBefore = driver.right.DrivenFrames;
            tickFrame = frame; readResult = read; readSeq = status.Seq;
            if (read == 0 && status.Seq > 0) lastValidSeq = status.Seq;
            Capture("hand-read", null); // includes busy/rejected reads BEFORE the existing early returns
        }
        public void Applied() { appliedFrame = Time.frameCount; Capture("hand-applied", null); }
        private static float? F(float value) => float.IsFinite(value) ? value : null;
        private static float?[] V(Vector3 p) => new[] { F(p.x), F(p.y), F(p.z) };
        private static float?[] Q(Quaternion q) => new[] { F(q.x), F(q.y), F(q.z), F(q.w) };
        private static string PathOf(Transform node)
        {
            var parts = new List<string>();
            for (int depth = 0; node != null && depth < 32; depth++, node = node.parent)
                parts.Add(node.name + "#" + node.GetInstanceID());
            parts.Reverse(); return string.Join("/", parts);
        }
        private void Add(Transform? node, string role)
        {
            if (node == null || nodes.Any(n => n.Node == node)) return;
            if (nodes.Count >= 24) { inventoryTruncated++; return; }
            nodes.Add((node, role));
        }
        private void Scan()
        {
            nodes.Clear(); renderers.Clear(); particles.Clear(); inventoryTruncated = 0;
            Add(source.transform, "source-camera"); Add(eyeLeft.transform, "left-eye-camera"); Add(eyeRight.transform, "right-eye-camera");
            Add(driver.left.Container, "left-mount"); Add(driver.right.Container, "right-mount");
            foreach (var anim in source.GetComponentsInChildren<WeaponAnim>(true))
                if (anim != null) Add(anim.weapon_base, "native-weapon-base");
            foreach (var weapon in source.GetComponentsInChildren<Weapon>(true))
            {
                if (weapon == null) continue;
                Add(weapon.transform, "weapon:" + weapon.current_weapon_slot);
                Add(weapon.muzzle_flash_spawn, "muzzle:" + weapon.current_weapon_slot);
                var flame = weapon.TryCast<Flamethrower>();
                if (flame != null) { Add(flame.vfx_flames_transform, "flame-transform"); Add(flame.vfx_flames?.transform, "flame-system"); }
            }
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (renderers.Count >= 16) { inventoryTruncated++; continue; }
                renderers.Add(renderer); Add(renderer.transform, "renderer");
                var skin = renderer.TryCast<SkinnedMeshRenderer>();
                if (skin == null) continue;
                Add(skin.rootBone, "skin-root");
                int count = 0;
                foreach (var bone in skin.bones) { if (++count > 4) { inventoryTruncated++; break; } Add(bone, "skin-bone-candidate"); }
            }
            foreach (var particle in source.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.Count >= 8) { inventoryTruncated++; continue; }
                particles.Add(particle); Add(particle.transform, "particle-emitter");
            }
            Record(new { kind = "inventory", frame = Time.frameCount, utc = DateTime.UtcNow.ToString("o"), inventoryTruncated,
                nodes = nodes.Select(n => new { id = n.Node.GetInstanceID(), role = n.Role, path = PathOf(n.Node),
                    parent = n.Node.parent == null ? 0 : n.Node.parent.GetInstanceID() }).ToArray(),
                renderers = renderers.Select(RendererIdentity).ToArray() });
        }
        private static object RendererIdentity(Renderer r)
        {
            var skin = r.TryCast<SkinnedMeshRenderer>();
            var mesh = skin != null ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            return new { id = r.GetInstanceID(), transform = r.transform.GetInstanceID(), type = r.GetIl2CppType().FullName,
                path = PathOf(r.transform), mesh = mesh == null ? 0 : mesh.GetInstanceID(), meshName = mesh?.name,
                rootBone = skin?.rootBone == null ? 0 : skin.rootBone.GetInstanceID(),
                bones = skin == null ? Array.Empty<int>() : skin.bones.Take(4).Select(b => b == null ? 0 : b.GetInstanceID()).ToArray(),
                boneCount = skin == null ? 0 : skin.bones.Length };
        }
        public void Capture(string phase, Camera? camera)
        {
            if (completed || error != null || writer.Error != null) return;
            if (camera != null && camera != source && camera != eyeLeft && camera != eyeRight) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now - started >= 180) { Dispose(); return; }
                if (now >= nextScan) { nextScan = now + 1; Scan(); }
                Record(new { kind = "sample", serial = ++serial, utc = DateTime.UtcNow.ToString("o"), realtime = now,
                    frame = Time.frameCount, phase, camera = camera == null ? 0 : camera.GetInstanceID(),
                    hostPhase, submitted, renderUntil, sourceEnabled = source.enabled,
                    leftEnabled = eyeLeft.enabled, rightEnabled = eyeRight.enabled, tickFrame, readResult, readSeq,
                    lastValidSeq, appliedFrame, tickCompletedThisFrame = appliedFrame == Time.frameCount,
                    leftDrivenThisFrame = tickFrame == Time.frameCount && driver.left.DrivenFrames > leftDrivenBefore,
                    rightDrivenThisFrame = tickFrame == Time.frameCount && driver.right.DrivenFrames > rightDrivenBefore,
                    leftState = driver.left.State.ToString(), rightState = driver.right.State.ToString(), scopeDepth = driver.ShotScopeDepth,
                    dropped = writer.Dropped, inventoryTruncated,
                    poses = nodes.Where(n => n.Node != null).Select(n => new { id = n.Node.GetInstanceID(),
                        active = n.Node.gameObject.activeInHierarchy, worldP = V(n.Node.position), worldQ = Q(n.Node.rotation),
                        localP = V(n.Node.localPosition), localQ = Q(n.Node.localRotation) }).ToArray(),
                    renderers = renderers.Where(r => r != null).Select(r => new { id = r.GetInstanceID(), enabled = r.enabled,
                        active = r.gameObject.activeInHierarchy }).ToArray(),
                    particles = particles.Where(p => p != null).Select(p => new { id = p.GetInstanceID(), count = p.particleCount,
                        playing = p.isPlaying, particleSpaceMeasured = false, particleSpaceUnavailable = ParticleSpaceUnavailable }).ToArray() });
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; Dispose(); }
        }
        // D-138: the deprecated alias compiled but failed in d136a at runtime, ending all observation.
        // Do not call it or guess Local/World. Keep this evidence gap explicit in the header and samples.
        private const string ParticleSpaceUnavailable = "D138_UNAVAILABLE_D136A_MAINMODULE_GETTER_MISSING";
        private void Record(object value) => writer.TryRecord(JsonSerializer.Serialize(value));
        public object Receipt() => new { completed, samples = serial, error, writerError = writer.Error,
            dropped = writer.Dropped, accepted = writer.Accepted, written = writer.Written,
            evidence = "READ_ONLY_NOT_ROOTCAUSE_NOT_HEADSET_ACCEPTANCE" };
        public void Dispose()
        {
            if (completed) return;
            completed = true;
            if (active == this) active = null;
            try { harmony.UnpatchSelf(); }
            catch (Exception e) { error ??= e.Message; }
            try
            {
                Record(new { kind = "end", utc = DateTime.UtcNow.ToString("o"), samples = serial, error,
                    writerError = writer.Error, accepted = writer.Accepted, writtenSoFar = writer.Written, dropped = writer.Dropped,
                    note = "writtenSoFar precedes asynchronous drain; missing end/drops/errors invalidate complete coverage" });
            }
            catch (Exception e) { error ??= e.GetType().Name + ": " + e.Message; }
            finally { writer.Complete(); }
        }
    }
}