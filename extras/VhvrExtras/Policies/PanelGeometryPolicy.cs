using System;

namespace VhvrExtras;

// Pure geometry for requested behaviour 1 (main menu on a panel 1.5 m in front of the player).
// Kept free of Unity types so tests/VhvrExtras can drive it deterministically.
//
// The panel is head-locked: its centre sits DistanceMeters along the head's forward axis and it faces the head,
// so "height is fixed" holds without a separate vertical offset (the headset's own recentring moves the rig).
internal static class PanelGeometryPolicy
{
    internal const float DefaultHorizontalFovDeg = 40f;
    internal const float DefaultAspect = 16f / 9f;

    internal static float Width(float distanceMeters, float horizontalFovDeg)
    {
        if (!float.IsFinite(distanceMeters) || distanceMeters <= 0f)
        {
            return 0f;
        }
        var fov = float.IsFinite(horizontalFovDeg)
            ? Math.Clamp(horizontalFovDeg, 10f, 120f)
            : DefaultHorizontalFovDeg;
        return 2f * distanceMeters * MathF.Tan(fov * MathF.PI / 360f);
    }

    internal static float Height(float width, float aspect)
    {
        var ratio = float.IsFinite(aspect) && aspect > 0.1f ? aspect : DefaultAspect;
        return width / ratio;
    }

    internal static float[] Center(float[] headPosition, float[] forwardUnit, float distanceMeters)
    {
        if (headPosition.Length != 3 || forwardUnit.Length != 3)
        {
            throw new ArgumentException("panel centre needs a 3-component position and direction");
        }
        var length = MathF.Sqrt(
            (forwardUnit[0] * forwardUnit[0]) + (forwardUnit[1] * forwardUnit[1]) + (forwardUnit[2] * forwardUnit[2]));
        if (!float.IsFinite(length) || length <= 1e-5f)
        {
            throw new ArgumentException("panel forward direction is not a usable vector");
        }
        return new[]
        {
            headPosition[0] + (forwardUnit[0] / length) * distanceMeters,
            headPosition[1] + (forwardUnit[1] / length) * distanceMeters,
            headPosition[2] + (forwardUnit[2] / length) * distanceMeters
        };
    }

    // Ray against the panel rectangle. u/v are in [-1, 1] inside the panel, with v increasing upwards.
    internal static bool TryRayPanel(
        float[] origin,
        float[] direction,
        float[] center,
        float[] normal,
        float[] right,
        float[] up,
        float halfWidth,
        float halfHeight,
        out float travel,
        out float u,
        out float v)
    {
        travel = 0f;
        u = 0f;
        v = 0f;
        if (origin.Length != 3 || direction.Length != 3 || center.Length != 3 || normal.Length != 3 ||
            right.Length != 3 || up.Length != 3)
        {
            return false;
        }
        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return false;
        }

        var denominator = Dot(direction, normal);
        if (!float.IsFinite(denominator) || MathF.Abs(denominator) < 1e-5f)
        {
            return false;
        }

        var toCenter = new[] { center[0] - origin[0], center[1] - origin[1], center[2] - origin[2] };
        var t = Dot(toCenter, normal) / denominator;
        if (!float.IsFinite(t) || t <= 0f)
        {
            return false;
        }

        var hit = new[] { origin[0] + direction[0] * t, origin[1] + direction[1] * t, origin[2] + direction[2] * t };
        var offset = new[] { hit[0] - center[0], hit[1] - center[1], hit[2] - center[2] };
        var localU = Dot(offset, right) / halfWidth;
        var localV = Dot(offset, up) / halfHeight;
        if (!float.IsFinite(localU) || !float.IsFinite(localV) || MathF.Abs(localU) > 1f || MathF.Abs(localV) > 1f)
        {
            return false;
        }

        travel = t;
        u = localU;
        v = localV;
        return true;
    }

    // Panel u/v (v up) to pixel coordinates of a render texture (y down), for pointer events.
    internal static void MapToPixels(float u, float v, int pixelWidth, int pixelHeight, out float x, out float y)
    {
        x = (u * 0.5f + 0.5f) * pixelWidth;
        y = (1f - (v * 0.5f + 0.5f)) * pixelHeight;
    }

    private static float Dot(float[] a, float[] b) => (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2]);
}
