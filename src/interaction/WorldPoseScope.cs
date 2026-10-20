namespace VampireHuntersVR.Interaction;

// Engine-independent production transaction; callers supply parent-first, non-overlapping roots.
// Capture finishes before any write. Failed entry rolls back. Restore attempts every node.
internal sealed class WorldPoseScope<TPose> : IDisposable
{
    private readonly Action<TPose> writeParent;
    private readonly TPose parent;
    private readonly List<(Action<TPose> Write, TPose Pose)> children = new();
    private bool disposed;

    private WorldPoseScope(Func<TPose> readParent, Action<TPose> writeParent,
        IEnumerable<(Func<TPose> Read, Action<TPose> Write)> roots)
    {
        this.writeParent = writeParent;
        parent = readParent();
        foreach (var root in roots) children.Add((root.Write, root.Read()));
    }

    public static WorldPoseScope<TPose> Enter(Func<TPose> readParent, Action<TPose> writeParent,
        IEnumerable<(Func<TPose> Read, Action<TPose> Write)> roots, TPose aim)
    {
        var scope = new WorldPoseScope<TPose>(readParent, writeParent, roots);
        try
        {
            writeParent(aim);
            foreach (var child in scope.children) child.Write(child.Pose);
            return scope;
        }
        catch (Exception entry)
        {
            try { scope.Dispose(); }
            catch (Exception restore) { throw new AggregateException(entry, restore); }
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var errors = new List<Exception>();
        try { writeParent(parent); } catch (Exception e) { errors.Add(e); }
        for (int i = children.Count - 1; i >= 0; i--)
            try { children[i].Write(children[i].Pose); } catch (Exception e) { errors.Add(e); }
        if (errors.Count != 0) throw new AggregateException("World pose restoration failed", errors);
    }
}