using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace VampireHuntersVR.Interaction;

// Windows process-memory adapter. Never writes GameAssembly.dll on disk, allocates executable code,
// changes its length, or touches attack code. Restore protection and flush the CPU instruction cache.
internal static class FlameEndpointMemory
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint previous);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    internal static ByteBranchPatch Prepare()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("FlameEndpointVfx requires Windows x64");
        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>().SingleOrDefault(m =>
            string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidOperationException("GameAssembly module not loaded");
        using (var stream = File.OpenRead(module.FileName ?? throw new InvalidOperationException("Module path missing")))
        using (var hash = SHA256.Create())
            if (Convert.ToHexString(hash.ComputeHash(stream)) != FlameEndpointPin.GameSha256)
                throw new InvalidOperationException("Unsupported GameAssembly hash; FlameEndpointVfx refused");
        byte[] context = FlameEndpointPin.OriginalContext();
        if (module.ModuleMemorySize < FlameEndpointPin.ContextRva + context.Length)
            throw new InvalidOperationException("GameAssembly module bounds mismatch");
        return PrepareAt(IntPtr.Add(module.BaseAddress, FlameEndpointPin.ContextRva), context,
            FlameEndpointPin.BranchOffset, FlameEndpointPin.EndpointOpcode);
    }

    // Also tested on a private non-executable allocation; not a game hook/live branch test.
    internal static ByteBranchPatch InstallAt(IntPtr start, byte[] context, int offset, byte replacement)
    {
        var patch = PrepareAt(start, context, offset, replacement);
        patch.Apply();
        return patch;
    }

    private static ByteBranchPatch PrepareAt(IntPtr start, byte[] context, int offset, byte replacement)
    {
        uint? originalProtection = null; // survives a failed restore, so rollback retries the TRUE original protection
        byte[] Read() { var bytes = new byte[context.Length]; Marshal.Copy(start, bytes, 0, bytes.Length); return bytes; }
        void Write(byte value)
        {
            IntPtr address = IntPtr.Add(start, offset);
            var size = new UIntPtr(1);
            if (!VirtualProtect(address, size, 0x40 /* PAGE_EXECUTE_READWRITE */, out uint previous))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Code protection change failed");
            originalProtection ??= previous;
            Exception? error = null;
            try
            {
                Marshal.WriteByte(address, value); // single byte, same branch destination/instruction width
                if (!FlushInstructionCache(GetCurrentProcess(), address, size))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Instruction cache flush failed");
            }
            catch (Exception e) { error = e; }
            finally
            {
                if (!VirtualProtect(address, size, originalProtection.Value, out _))
                {
                    var restore = new Win32Exception(Marshal.GetLastWin32Error(), "Code protection restore failed");
                    error = error == null ? restore : new AggregateException(error, restore);
                }
                else originalProtection = null;
            }
            if (error != null) throw error;
        }
        return new ByteBranchPatch(Read, Write, context, offset, replacement);
    }
}