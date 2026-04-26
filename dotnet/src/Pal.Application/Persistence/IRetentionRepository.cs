namespace Pal.Application.Persistence;

public interface IRetentionRepository
{
    /// <summary>
    /// Deletes non-baseline completed/failed jobs older than <paramref name="jobRetentionDays"/> days,
    /// their compare results (Restrict FK — must go first), and any uploads that become fully orphaned.
    /// Returns details needed for storage cleanup.
    /// </summary>
    Task<RetentionRunResult> PurgeJobsAsync(int jobRetentionDays, CancellationToken ct = default);

    /// <summary>
    /// Deletes audit events older than <paramref name="auditRetentionDays"/> days.
    /// </summary>
    Task<int> PurgeAuditEventsAsync(int auditRetentionDays, CancellationToken ct = default);
}

public sealed record RetentionRunResult(
    int JobsDeleted,
    int CompareResultsDeleted,
    IReadOnlyList<Guid> DeletedJobIds,
    IReadOnlyList<string> DeletedUploadSha256s)
{
    public static readonly RetentionRunResult Empty =
        new(0, 0, [], []);
}
