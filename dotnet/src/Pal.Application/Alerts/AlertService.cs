using Pal.Application.Persistence;
using Pal.Engine.Model;

namespace Pal.Application.Alerts;

public sealed class AlertService : IAlertService
{
    private readonly IAlertRepository _repo;

    public AlertService(IAlertRepository repo) => _repo = repo;

    public async Task EvaluateAsync(Guid jobId, IReadOnlyList<Finding> findings, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // When the same rule fires multiple times in one job, use the highest severity.
        var deduplicated = findings
            .GroupBy(f => f.RuleId)
            .Select(g => g.OrderByDescending(f => SeverityRank(f.Severity)).First())
            .ToList();

        foreach (var f in deduplicated)
        {
            var existing = await _repo.FindActiveByRuleIdAsync(f.RuleId, ct);
            if (existing is not null)
            {
                var escalated = SeverityRank(f.Severity) > SeverityRank(existing.Severity)
                    ? f.Severity : existing.Severity;
                await _repo.UpdateLatestAsync(existing.Id, jobId, escalated, now, ct);
            }
            else
            {
                await _repo.CreateAsync(new AlertDto
                {
                    Id = Guid.NewGuid(),
                    RuleId = f.RuleId,
                    Severity = f.Severity,
                    Category = f.Category,
                    Title = f.Title,
                    Status = "open",
                    TriggeringJobId = jobId,
                    LatestJobId = jobId,
                    TriggeredAt = now,
                    LastSeenAt = now,
                }, ct);
            }
        }
    }

    public Task<IReadOnlyList<AlertDto>> ListAsync(string? status = null, string? severity = null, CancellationToken ct = default)
        => _repo.ListAsync(status, severity, ct);

    public Task<AlertDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _repo.GetAsync(id, ct);

    public Task<bool> AcknowledgeAsync(Guid id, CancellationToken ct = default)
        => _repo.AcknowledgeAsync(id, DateTimeOffset.UtcNow, ct);

    public Task<bool> ResolveAsync(Guid id, string? note, CancellationToken ct = default)
        => _repo.ResolveAsync(id, note, DateTimeOffset.UtcNow, ct);

    public static int SeverityRank(string? s) => s switch
    {
        "critical" => 3,
        "warning" => 2,
        "informational" => 1,
        _ => 0
    };
}
