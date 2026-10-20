using System;
using System.Runtime.InteropServices;

namespace VhvrExtras;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeHandPose
{
    public int Active;
    public float X, Y, Z, Qx, Qy, Qz, Qw, Trigger;
    public int Face;
    public float StickX, StickY;
    public int StickClick, Lower, Menu;
    public float Squeeze;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeHandsStatus
{
    public int Available, Seq, Frames, Reserved;
    public NativeHandPose Left, Right;
}

internal static class NativeHandsAbi
{
    internal static void AssertLayout()
    {
        if (Marshal.SizeOf<NativeHandPose>() != 64 ||
            Marshal.SizeOf<NativeHandsStatus>() != 144 ||
            Marshal.OffsetOf<NativeHandPose>(nameof(NativeHandPose.Squeeze)).ToInt32() != 60 ||
            Marshal.OffsetOf<NativeHandsStatus>(nameof(NativeHandsStatus.Right)).ToInt32() != 80)
        {
            throw new TypeLoadException("VhvrNativeHost hand ABI mismatch; expected HandPose=64, HandsStatus=144, Squeeze@60, Right@80.");
        }
    }
}
