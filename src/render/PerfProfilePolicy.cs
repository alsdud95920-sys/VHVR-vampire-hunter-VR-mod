namespace VampireHuntersVR.Render;

// D-150: opt-in engine quality profile. The sitting measurements put the wall outside our own frame structure (the
// prepare-only frame was removed and the cycle did not move), so the remaining question is whether the *scene* cost is
// what the frame waits on: this class is the reversible quality-vs-fps dial for answering it with one sitting.
// Pure functions only, so the target values are fixture-testable; the plugin applies them with per-setting try/catch,
// logs before and after, and never assumes a setting exists.
public readonly record struct PerfSettings(
    float ShadowDistance, int ShadowCascades, string ShadowResolution, int PixelLightCount, float LodBias,
    int ParticleRaycastBudget, bool RealtimeReflectionProbes, bool SoftParticles, int AntiAliasing)
{
    public string Describe() =>
        $"shadowDistance={ShadowDistance:0.#} cascades={ShadowCascades} shadowResolution={ShadowResolution} " +
        $"pixelLights={PixelLightCount} lodBias={LodBias:0.##} particleRaycastBudget={ParticleRaycastBudget} " +
        $"realtimeReflectionProbes={RealtimeReflectionProbes} softParticles={SoftParticles} msaa={AntiAliasing}";
}

public static class PerfProfilePolicy
{
    public const string Off = "off";
    public const string Light = "light";
    public const string Strong = "strong";
    public static bool IsValid(string? profile) => profile is Off or Light or Strong;
    // Only ever lowers a dial: a profile can never raise quality above what the game asked for, so an A/B can never
    // pretend the game's own settings were better than they are.
    public static float Lower(float current, float target) => current <= 0f ? current : MathF.Min(current, target);
    public static int Lower(int current, int target) => current <= 0 ? current : Math.Min(current, target);
    // off (or an unknown name) = the game's own settings, untouched.
    public static PerfSettings? Target(string? profile, PerfSettings current) => profile switch
    {
        Light => new PerfSettings(
            ShadowDistance: Lower(current.ShadowDistance, 60f),
            ShadowCascades: Lower(current.ShadowCascades, 1),
            ShadowResolution: "Low",
            PixelLightCount: Lower(current.PixelLightCount, 2),
            LodBias: Lower(current.LodBias, 0.8f),
            ParticleRaycastBudget: Lower(current.ParticleRaycastBudget, 64),
            RealtimeReflectionProbes: false,
            SoftParticles: false,
            AntiAliasing: current.AntiAliasing),
        Strong => new PerfSettings(
            ShadowDistance: 0f,
            ShadowCascades: Lower(current.ShadowCascades, 1),
            ShadowResolution: "Low",
            PixelLightCount: Lower(current.PixelLightCount, 1),
            LodBias: Lower(current.LodBias, 0.6f),
            ParticleRaycastBudget: Lower(current.ParticleRaycastBudget, 32),
            RealtimeReflectionProbes: false,
            SoftParticles: false,
            AntiAliasing: 0),
        _ => null
    };
}
