using Pal.Application.Persistence;
using Pal.Application.Webhooks;
using Pal.Engine.Model;

namespace Pal.Application.Alerts;

public sealed class AlertService : IAlertService
{
    private readonly IAlertRepository _repo;
    private readonly INotificationService _notifications;

    public AlertService(IAlertRepository repo, INotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;
    }

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
                var escalated = SeverityRank(f.Severity) > SeverityRank(existing.Severity);
                var newSeverity = escalated ? f.Severity : existing.Severity;
                await _repo.UpdateLatestAsync(existing.Id, jobId, newSeverity, now, ct);
                if (escalated)
                {
                    var updated = await _repo.GetAsync(existing.Id, ct);
                    if (updated is not null)
                        await _notifications.NotifyAsync("alert.escalated", updated, ct);
                }
            }
            else
            {
                var newAlert = new AlertDto
                {
                    Id = Guid.NewGuid(), RuleId = f.RuleId, Severity = f.Severity,
                    Category = f.Category, Title = f.Title, Status = "open",
                    TriggeringJobId = jobId, LatestJobId = jobId,
                    TriggeredAt = now, LastSeenAt = now,
                };
                await _repo.CreateAsync(newAlert, ct);
                await _notifications.NotifyAsync("alert.created", newAlert, ct);
            }
        }
    }

    public Task<IReadOnlyList<AlertDto>> ListAsync(string? status = null, string? severity = null, CancellationToken ct = default)
        => _repo.ListAsync(status, severity, ct);

    public Task<AlertDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _repo.GetAsync(id, ct);

    public async Task<bool> AcknowledgeAsync(Guid id, CancellationToken ct = default)
    {
        var ok = await _repo.AcknowledgeAsync(id, DateTimeOffset.UtcNow, ct);
        if (ok)
        {
            var alert = await _repo.GetAsync(id, ct);
            if (alert is not null)
                await _notifications.NotifyAsync("alert.acknowledged", alert, ct);
        }
        return ok;
    }

    public async Task<bool> ResolveAsync(Guid id, string? note, CancellationToken ct = default)
    {
        var ok = await _repo.ResolveAsync(id, note, DateTimeOffset.UtcNow, ct);
        if (ok)
        {
            var alert = await _repo.GetAsync(id, ct);
            if (alert is not null)
                await _notifications.NotifyAsync("alert.resolved", alert, ct);
        }
        return ok;
    }

    public static int SeverityRank(string? s) => s switch
    {
        "critical" => 3,
        "warning" => 2,
        "informational" => 1,
        _ => 0
    };
}
