using System;

namespace VhvrExtras;

// OpenXR -> Unity pose conversion, copied from the recovered src\render\StereoMath.cs and
// src\interaction\HandAim.cs (VampireHuntersVR.Render.StereoMath / Interaction.HandAimMath) so this plugin
// stays self-contained and the formula can be checked without the game. OpenXR local space is +X right,
// +Y up, -Z forward; Unity transforms use +Z forward.
internal static class PoseMathPolicy
{
    internal static bool IsFinite(float[] values)
    {
        foreach (var value in values)
        {
            if (!float.IsFinite(value))
            {
                return false;
            }
        }
        return true;
    }

    internal static float[] UnityPosition(float x, float y, float z)
    {
        return new[] { x, y, -z };
    }

    internal static bool TryUnityOrientation(float x, float y, float z, float w, out float[] quaternion)
    {
        quaternion = new[] { 0f, 0f, 0f, 1f };
        var norm = (x * x) + (y * y) + (z * z) + (w * w);
        if (!float.IsFinite(norm) || MathF.Abs(norm - 1f) > 0.01f)
        {
            return false;
        }
        var inverse = 1f / MathF.Sqrt(norm);
        quaternion = new[] { -x * inverse, -y * inverse, z * inverse, w * inverse };
        return true;
    }

    internal static float[] Rotate(float[] quaternion, float[] vector)
    {
        if (quaternion.Length != 4 || vector.Length != 3)
        {
            throw new ArgumentException("pose dimensions");
        }
        var tx = 2f * ((quaternion[1] * vector[2]) - (quaternion[2] * vector[1]));
        var ty = 2f * ((quaternion[2] * vector[0]) - (quaternion[0] * vector[2]));
        var tz = 2f * ((quaternion[0] * vector[1]) - (quaternion[1] * vector[0]));
        return new[]
        {
            vector[0] + (quaternion[3] * tx) + (quaternion[1] * tz) - (quaternion[2] * ty),
            vector[1] + (quaternion[3] * ty) + (quaternion[2] * tx) - (quaternion[0] * tz),
            vector[2] + (quaternion[3] * tz) + (quaternion[0] * ty) - (quaternion[1] * tx)
        };
    }

    internal static float[] Multiply(float[] a, float[] b)
    {
        if (a.Length != 4 || b.Length != 4)
        {
            throw new ArgumentException("pose dimensions");
        }
        return new[]
        {
            (a[3] * b[0]) + (a[0] * b[3]) + (a[1] * b[2]) - (a[2] * b[1]),
            (a[3] * b[1]) - (a[0] * b[2]) + (a[1] * b[3]) + (a[2] * b[0]),
            (a[3] * b[2]) + (a[0] * b[1]) - (a[1] * b[0]) + (a[2] * b[3]),
            (a[3] * b[3]) - (a[0] * b[0]) - (a[1] * b[1]) - (a[2] * b[2])
        };
    }

    // Unity forward (+Z) of a world rotation.
    internal static float[] Forward(float[] rotation)
    {
        return Rotate(rotation, new[] { 0f, 0f, 1f });
    }

    internal static bool TryToWorld(
        float[] origin,
        float[] anchorRotation,
        float x,
        float y,
        float z,
        float qx,
        float qy,
        float qz,
        float qw,
        out float[] position,
        out float[] rotation)
    {
        position = Array.Empty<float>();
        rotation = Array.Empty<float>();
        if (origin.Length != 3 || anchorRotation.Length != 4 || !IsFinite(origin) ||
            !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            return false;
        }

        if (!TryUnityOrientation(qx, qy, qz, qw, out var localRotation))
        {
            return false;
        }

        var local = Rotate(anchorRotation, UnityPosition(x, y, z));
        position = new[] { origin[0] + local[0], origin[1] + local[1], origin[2] + local[2] };
        rotation = Multiply(anchorRotation, localRotation);
        return IsFinite(position) && IsFinite(rotation);
    }

    // D-201: the menu panel and the eye cameras are both placed from the LIVE head pose, and the host's eye
    // composition has carried no game-camera rotation since D-195. The menu pointer must therefore be composed in
    // that same frame. anchorRotation is `source.transform.rotation` (= ArenaCamera in the menu, measured at
    // yaw -90 on 2026-10-18) - a frame the rendered view does not share, which is what bent the menu laser ~90
    // degrees and made the frozen panel sit 90 degrees off the player's view.
    internal static float[] HeadFrameRotation() => new[] { 0f, 0f, 0f, 1f };

    internal static float[] MenuPointerAnchor(bool headFrame, float[] anchorRotation)
    {
        if (anchorRotation.Length != 4 || !IsFinite(anchorRotation))
        {
            return HeadFrameRotation();
        }
        return headFrame ? HeadFrameRotation() : anchorRotation;
    }

    // Unity yaw (degrees) of a world rotation, taken from its forward axis.
    internal static float YawDegrees(float[] rotation)
    {
        var forward = Forward(rotation);
        return MathF.Atan2(forward[0], forward[2]) * 180f / MathF.PI;
    }

    // Angle between the forward axes of two world rotations, 0..180 degrees; NaN when a rotation is unusable.
    internal static float ForwardAngleDegrees(float[] rotationA, float[] rotationB)
    {
        var a = Forward(rotationA);
        var b = Forward(rotationB);
        var lengthA = MathF.Sqrt((a[0] * a[0]) + (a[1] * a[1]) + (a[2] * a[2]));
        var lengthB = MathF.Sqrt((b[0] * b[0]) + (b[1] * b[1]) + (b[2] * b[2]));
        if (lengthA <= 1e-5f || lengthB <= 1e-5f)
        {
            return float.NaN;
        }
        var dot = ((a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2])) / (lengthA * lengthB);
        return MathF.Acos(Math.Clamp(dot, -1f, 1f)) * 180f / MathF.PI;
    }
}
