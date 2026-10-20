using System.Collections.Concurrent;
using System.Text;

namespace VampireHuntersVR.Interaction;

// Unity objects are sampled on the main thread. Only immutable strings reach this worker.
internal sealed class BoundedDiagnosticWriter : IDisposable
{
    private readonly BlockingCollection<string> queue;
    private readonly Task worker;
    private readonly long maxBytes;
    private readonly int maxRows;
    private int stopped, dropped, accepted, written;
    private long bytes;
    private string? error;
    public int Dropped => Volatile.Read(ref dropped);
    public int Accepted => Volatile.Read(ref accepted);
    public int Written => Volatile.Read(ref written);
    public long Bytes => Interlocked.Read(ref bytes);
    public string? Error => Volatile.Read(ref error);

    public BoundedDiagnosticWriter(string path, int capacity = 512, int maxRows = 100000, long maxBytes = 268435456)
    {
        if (!Path.IsPathRooted(path) || capacity < 1 || maxRows < 1 || maxBytes < 1)
            throw new ArgumentException("invalid diagnostic limits/path");
        this.maxRows = maxRows; this.maxBytes = maxBytes;
        queue = new BlockingCollection<string>(capacity);
        worker = Task.Run(() => Write(path));
    }

    // Never waits for a disk, lock owned by the render thread, or free queue space.
    public bool TryRecord(string line)
    {
        if (Volatile.Read(ref stopped) != 0 || Error != null) return false;
        if (line.Length > 65536 || Accepted >= maxRows) { Interlocked.Increment(ref dropped); return false; }
        try
        {
            if (!queue.TryAdd(line)) { Interlocked.Increment(ref dropped); return false; }
            Interlocked.Increment(ref accepted); return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    private void Write(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var text = new StreamWriter(stream, new UTF8Encoding(false));
            foreach (string line in queue.GetConsumingEnumerable())
            {
                long length = Encoding.UTF8.GetByteCount(line) + Encoding.UTF8.GetByteCount(Environment.NewLine);
                if (Bytes + length > maxBytes) { Interlocked.Increment(ref dropped); continue; }
                text.WriteLine(line); Interlocked.Add(ref bytes, length); Interlocked.Increment(ref written);
                if (Written % 128 == 0) text.Flush();
            }
        }
        catch (Exception e) { Volatile.Write(ref error, e.GetType().Name + ": " + e.Message); }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref stopped, 1) == 0) queue.CompleteAdding();
    }
    public bool WaitForDrain(int milliseconds) => worker.Wait(milliseconds);
    public void Dispose() => Complete(); // no main-thread disk wait
}