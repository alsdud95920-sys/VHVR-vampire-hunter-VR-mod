namespace VampireHuntersVR.Render;

// D-147: per-eye render size preset. The native host creates one swapchain per eye at exactly the eye render target's
// size, so this is the whole resolution setting. A preset is a scale factor on the configured base size (default
// 2064x2208 = the per-eye size the runtime recommends for a Quest 3 through Virtual Desktop), which keeps the eye
// aspect ratio the compositor projects, instead of an absolute 16:9 number that would stretch the eye image.
public static class RenderResolutionPolicy
{
    public const int MinDimension = 512;
    public const int MaxDimension = 4096;
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Custom = "custom";
    public const double MinScale = 0.5;
    public const double MaxScale = 2.0;
    public const double CustomDefaultScale = 1.0;
    public const double LowScale = 0.8;
    public const double NormalScale = 1.0;
    public const double HighScale = 1.2;
    // "custom" keeps EyeWidth/EyeHeight exactly as configured (still clamped); the others scale the base size.
    public static bool IsValid(string? preset) => preset is Low or Normal or High or Custom;
    public static bool IsValidScale(double scale) => double.IsFinite(scale) && scale >= MinScale && scale <= MaxScale;
    public static double Scale(string? preset) => preset switch { Low => LowScale, High => HighScale, _ => NormalScale };
    // Kept from the earlier revision: a direct preset -> clamped dimension helper used by the older fixtures.
    public static int Dimension(int configured, string? preset) => Scaled(configured, Scale(preset));
    // The factor a sitting really uses: a named preset ignores EyeScale, "custom" reads it.
    public static double EffectiveScale(string? preset, double customScale) =>
        preset == Custom ? (IsValidScale(customScale) ? customScale : CustomDefaultScale) : Scale(preset);
    public static int Scaled(int configured, double scale) =>
        Math.Clamp((int)Math.Round(configured * scale, MidpointRounding.AwayFromZero), MinDimension, MaxDimension);
    public static (int Width, int Height) EyeSize(int width, int height, string? preset)
        => EyeSize(width, height, preset, CustomDefaultScale);
    public static (int Width, int Height) EyeSize(int width, int height, string? preset, double customScale)
    {
        double scale = EffectiveScale(preset, customScale);
        return (Scaled(width, scale), Scaled(height, scale));
    }
    // One string shared by the runtime log, the receipt and the settings program's preview, so the three cannot drift.
    // Both-eyes numbers come first because that is how a user picks the size.
    public static (int Width, int Height) BothEyes(int width, int height) => (width * 2, height * 2);
    public static string Describe(int baseWidth, int baseHeight, string? preset, double customScale)
    {
        (int width, int height) = EyeSize(baseWidth, baseHeight, preset, customScale);
        (int bothWidth, int bothHeight) = BothEyes(width, height);
        double scale = EffectiveScale(preset, customScale);
        return $"preset={preset} scale={scale:0.00} eye={width}x{height} both={bothWidth}x{bothHeight} " +
            $"pixels={(long)width * height * 2 / 1000000.0:0.0}MP base={baseWidth}x{baseHeight}/eye";
    }
    // D-170: eye-image MSAA. Zero means "no MSAA" (Unity sample count 1); the runtime only accepts the four sample
    // counts the OpenXR swapchain sample-count check passes, which is also exactly what the settings tool offers.
    public static bool IsValidMsaa(int samples) => samples is 0 or 2 or 4 or 8;
    public static int UnitySamples(int samples) => samples <= 0 ? 1 : samples;
    public static string MsaaLabel(int samples) => samples <= 0 ? "off" : $"{samples}x";
    // The MSAA-era receipt names the side-by-side eye pair (pair = both eyes wide, one eye tall) and the sample count,
    // so the same file the plugin and the settings tool compile states the effective eye size and MSAA in one line.
    public static string Describe(int baseWidth, int baseHeight, string? preset, int msaa)
    {
        (int width, int height) = EyeSize(baseWidth, baseHeight, preset);
        return $"preset={preset} eye={width}x{height} pair={width * 2}x{height} msaa={MsaaLabel(msaa)} " +
            $"base={baseWidth}x{baseHeight}/eye";
    }
}