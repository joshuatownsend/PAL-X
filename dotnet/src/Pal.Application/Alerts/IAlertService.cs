using Pal.Application.Persistence;
using Pal.Engine.Model;

namespace Pal.Application.Alerts;

public interface IAlertService
{
    Task EvaluateAsync(Guid jobId, Guid workspaceId, IReadOnlyList<Finding> findings, CancellationToken ct = default);
    Task<IReadOnlyList<AlertDto>> ListAsync(string? status = null, string? severity = null, CancellationToken ct = default);
    Task<AlertDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> AcknowledgeAsync(Guid id, CancellationToken ct = default);
    Task<bool> ResolveAsync(Guid id, string? note, CancellationToken ct = default);
    /// <summary>
    /// Sets <c>SnoozedUntil</c>. Pass NULL to clear. Returns false if the alert is missing
    /// or already resolved.
    /// </summary>
    Task<bool> SetSnoozedUntilAsync(Guid id, DateTimeOffset? snoozedUntil, CancellationToken ct = default);
}
