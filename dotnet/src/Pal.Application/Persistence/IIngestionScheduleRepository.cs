namespace Pal.Application.Persistence;

public interface IIngestionScheduleRepository
{
    Task<IReadOnlyList<IngestionScheduleDto>> ListAsync(CancellationToken ct = default);
    Task<IngestionScheduleDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task CreateAsync(IngestionScheduleDto schedule, CancellationToken ct = default);
    Task<bool> UpdateAsync(IngestionScheduleDto schedule, CancellationToken ct = default);
    Task<bool> SetEnabledAsync(Guid id, bool enabled, DateTimeOffset updatedAt, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all enabled schedules across all workspaces with NextRunAt &lt;= now (or null).
    /// Bypasses the tenant filter — only valid from worker context. Caller must SetWorkspace
    /// per result before invoking workspace-scoped repositories.
    /// </summary>
    Task<IReadOnlyList<IngestionScheduleDto>> ListDueAsync(DateTimeOffset now, CancellationToken ct = default);

    Task RecordRunAsync(Guid id, DateTimeOffset lastRunAt, DateTimeOffset nextRunAt, CancellationToken ct = default);
}
