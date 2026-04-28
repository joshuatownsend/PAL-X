namespace Pal.Application.Persistence;

public interface IAlertRepository
{
    Task<AlertDto?> FindActiveByRuleIdAsync(string ruleId, Guid workspaceId, CancellationToken ct = default);
    Task CreateAsync(AlertDto alert, CancellationToken ct = default);
    Task UpdateLatestAsync(Guid id, Guid latestJobId, string severity, DateTimeOffset lastSeenAt, string? policyApplied, CancellationToken ct = default);
    Task<IReadOnlyList<AlertDto>> ListAsync(string? status, string? severity, CancellationToken ct = default);
    Task<AlertDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> AcknowledgeAsync(Guid id, DateTimeOffset now, CancellationToken ct = default);
    Task<bool> ResolveAsync(Guid id, string? note, DateTimeOffset now, CancellationToken ct = default);
    /// <summary>
    /// Sets <c>SnoozedUntil</c>. Pass NULL to clear. Returns false if the alert is missing
    /// or already resolved (snoozing a resolved alert is meaningless).
    /// </summary>
    Task<bool> SetSnoozedUntilAsync(Guid id, DateTimeOffset? snoozedUntil, CancellationToken ct = default);
}
