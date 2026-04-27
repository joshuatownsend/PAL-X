namespace Pal.Persistence;

public interface ITenantContext
{
    /// <summary>
    /// The active workspace for the current async execution context.
    /// Null means system/worker scope — EF query filters pass all rows through.
    /// </summary>
    Guid? WorkspaceId { get; }

    /// <summary>
    /// Sets the workspace for the current async execution context.
    /// Call at the start of each request and dispose (or reset) at the end.
    /// </summary>
    IDisposable SetWorkspace(Guid workspaceId);
}
