namespace VampireHuntersVR.Interaction;

// D-139: exact-build visual branch only. Never replace native attack arguments or detour Vector3.
internal static class FlameEndpointPin
{
    internal const string GameSha256 = "25FEBBDE3B459D7A81B64A1EA8A8CF8321340B18E0AFA823BAB82C2A36398785";
    internal const int ContextRva = 0x46BF1D, BranchOffset = 14;
    internal const byte EndpointOpcode = 0xEB;
    internal static byte[] OriginalContext() => Convert.FromHexString(
        "0F2F3530DECC01488BBBD0020000771F488B93B80100004885D20F84AB0000004533C0488D4C2440E8267AA201EB59");
}

// Engine-independent transaction. Write changes ONE opcode; context/displacement must remain exact.
// A failed write may have changed memory: inspect and roll back rather than assuming no mutation.
internal sealed class ByteBranchPatch : IDisposable
{
    private readonly Func<byte[]> read;
    private readonly Action<byte> write;
    private readonly byte[] original, patched;
    private readonly int offset;
    public bool Active { get; private set; }
    public bool Applied { get; private set; }

    internal ByteBranchPatch(Func<byte[]> read, Action<byte> write, byte[] context, int offset, byte replacement)
    {
        if (offset < 0 || offset >= context.Length || context[offset] == replacement)
            throw new ArgumentException("Invalid branch patch");
        this.read = read; this.write = write; this.offset = offset;
        original = (byte[])context.Clone(); patched = (byte[])context.Clone(); patched[offset] = replacement;
    }

    public static ByteBranchPatch Install(Func<byte[]> read, Action<byte> write, byte[] context, int offset, byte replacement)
    {
        var patch = new ByteBranchPatch(read, write, context, offset, replacement);
        patch.Apply();
        return patch;
    }

    internal void Apply()
    {
        if (Active) throw new InvalidOperationException("Duplicate patch application");
        Require(original, "Original code context mismatch; patch refused");
        try
        {
            // Mark before writing: write/cache/protection failure can occur AFTER a successful byte store.
            Active = true;
            write(patched[offset]);
            Require(patched, "Patch readback mismatch");
            Applied = true;
        }
        catch (Exception entry)
        {
            try { Dispose(); }
            catch (Exception restore) { throw new AggregateException("Patch entry and rollback failed", entry, restore); }
            throw;
        }
    }

    private void Require(byte[] expected, string message)
    {
        if (!read().AsSpan().SequenceEqual(expected)) throw new InvalidOperationException(message);
    }

    public void Dispose()
    {
        if (!Active) return;
        Applied = false; // Active tracks restoration responsibility, not proof of a successful installation
        var current = read();
        if (!current.AsSpan().SequenceEqual(original) && !current.AsSpan().SequenceEqual(patched))
            throw new InvalidOperationException("Code changed externally; refuse to overwrite during restoration");
        write(original[offset]);
        Require(original, "Original code restoration readback mismatch");
        Active = false;
    }
}