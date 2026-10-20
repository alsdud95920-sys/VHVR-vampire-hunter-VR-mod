namespace VhvrExtras;

// Pure arithmetic for requested behaviour 3 (UI scale).
//
// The shipped build sizes every root UI canvas by its target world width in metres
// (UiCanvas.Width / canvas pixel width -> localScale), with 1.9 m default, 1.4 m for the level-up screen and
// 1.6 m for head-locked canvases. Scaling the target width scales the whole HUD, which is exactly what the
// request asks for, so the multiplier is applied to that width instead of patching transforms per frame.
internal static class UiScalePolicy
{
    internal const float MinScale = 0.5f;
    internal const float MaxScale = 3f;

    internal static bool IsUsable(float scale)
    {
        return float.IsFinite(scale) && scale >= MinScale && scale <= MaxScale;
    }

    internal static float Clamp(float scale)
    {
        if (!float.IsFinite(scale))
        {
            return 1f;
        }
        if (scale < MinScale)
        {
            return MinScale;
        }
        if (scale > MaxScale)
        {
            return MaxScale;
        }
        return scale;
    }

    internal static float ScaleWidth(float width, float scale)
    {
        if (!float.IsFinite(width) || width <= 0f)
        {
            return width;
        }
        return width * Clamp(scale);
    }
}
