namespace Pal.Persistence;

/// <summary>
/// Singleton that stores the active workspace per async execution context via AsyncLocal.
/// Singletons injecting this are safe because they read the AsyncLocal value at call time,
/// not at construction time — each request sees its own value independently.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<Guid?> _workspaceId = new();

    public Guid? WorkspaceId => _workspaceId.Value;

    public IDisposable SetWorkspace(Guid workspaceId)
    {
        var previous = _workspaceId.Value;
        _workspaceId.Value = workspaceId;
        return new Restore(previous);
    }

    private sealed class Restore(Guid? previous) : IDisposable
    {
        public void Dispose() => _workspaceId.Value = previous;
    }
}
