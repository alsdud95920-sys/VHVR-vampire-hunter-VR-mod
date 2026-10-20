using System;
using System.Runtime.InteropServices;

namespace VampireHuntersVR.Stage0;

// Restored 2026-10-14 (D-178): the native-host interop types and P/Invoke surface were lost from the source
// tree (OpenXrHost.cs was a bad merge and this file was missing). Recovered verbatim from the shipped
// VampireHuntersVR.Stage0.dll (D-171) with ilspycmd; the struct layouts match the native host ABI that
// Initialize() asserts (NativeEyePose 44 bytes, NativeHostStatus 136 bytes).
[StructLayout(LayoutKind.Sequential)]
public struct NativeEyePose
{
    public float X;
    public float Y;
    public float Z;
    public float Qx;
    public float Qy;
    public float Qz;
    public float Qw;
    public float Left;
    public float Right;
    public float Up;
    public float Down;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeHostStatus
{
    public int State;
    public int Width;
    public int Height;
    public int Frames;
    public int GpuCompleted;
    public uint MainThread;
    public uint RenderThread;
    public int AdapterMatched;
    public int Stopping;
    public int Ended;
    public double HoldSeconds;
    public NativeEyePose Left;
    public NativeEyePose Right;
}

internal static class HostNative
{
    private const string Dll = "VhvrNativeHost.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int Configure(IntPtr left, IntPtr right, string output, string runtime, int target, int frameDump);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int RequestCapture(int id);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ReadStatus(out NativeHostStatus status);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ReadError();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CanReleaseSources();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int RequestPrepare();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr GetRenderEvent();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ConfigureHeadset(int budgetSeconds);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetDeferredGpuWait(int enabled);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetPrepareAhead(int enabled);

    // D-186-FovScale: the per-axis render-FOV scales (1.0 = pass-through). Applied on the native side to
    // the four angles reported by xrLocateViews (X = angleLeft/angleRight, Y = angleUp/angleDown), before
    // both the managed-side projection matrix and the compositor submit. The managed side multiplies the
    // same four angles when it builds the projection, so the picture stays aligned at the edges (one-sided
    // narrowing shows black bars). The native side refuses values outside 0.50-1.00 (returns non-zero) and
    // logs the rejection to the native log.
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetFovScaleX(float scale);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetFovScaleY(float scale);
}
