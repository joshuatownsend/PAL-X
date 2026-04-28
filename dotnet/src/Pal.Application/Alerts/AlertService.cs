using Pal.Application.Alerts.Policy;
using Pal.Application.Persistence;
using Pal.Application.Webhooks;
using Pal.Engine.Model;

namespace Pal.Application.Alerts;

public sealed class AlertService : IAlertService
{
    private readonly IAlertRepository _repo;
    private readonly INotificationService _notifications;
    private readonly IPolicyEvaluator _policy;

    public AlertService(IAlertRepository repo, INotificationService notifications, IPolicyEvaluator policy)
    {
        _repo = repo;
        _notifications = notifications;
        _policy = policy;
    }

    public async Task EvaluateAsync(Guid jobId, Guid workspaceId, IReadOnlyList<Finding> findings, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Policy runs as a pre-processor: returns ruleId → escalation (and, in the future,
        // ruleId → notification suppression). Findings remain immutable; we consult the
        // result when constructing or updating each alert below.
        var policyResult = await _policy.EvaluateAsync(workspaceId, findings, ct);

        // When the same rule fires multiple times in one job, use the highest severity.
        var deduplicated = findings
            .GroupBy(f => f.RuleId)
            .Select(g => g.OrderByDescending(f => SeverityRank(f.Severity)).First())
            .ToList();

        foreach (var f in deduplicated)
        {
            policyResult.Escalations.TryGetValue(f.RuleId, out var escalation);
            var effectiveSeverity = escalation?.NewSeverity ?? f.Severity;
            var policyApplied = escalation?.PolicyRuleId;
            var policySuppressed = policyResult.NotificationSuppressed.Contains(f.RuleId);

            var existing = await _repo.FindActiveByRuleIdAsync(f.RuleId, workspaceId, ct);
            if (existing is not null)
            {
                var escalated = SeverityRank(effectiveSeverity) > SeverityRank(existing.Severity);
                var newSeverity = escalated ? effectiveSeverity : existing.Severity;
                await _repo.UpdateLatestAsync(existing.Id, jobId, newSeverity, now, policyApplied, ct);
                if (escalated && !policySuppressed && !IsSnoozed(existing, now))
                {
                    var updated = await _repo.GetAsync(existing.Id, ct);
                    if (updated is not null)
                        _ = _notifications.NotifyAsync("alert.escalated", updated, CancellationToken.None);
                }
            }
            else
            {
                var newAlert = new AlertDto
                {
                    Id = Guid.NewGuid(), WorkspaceId = workspaceId, RuleId = f.RuleId,
                    Severity = effectiveSeverity, Category = f.Category, Title = f.Title, Status = "open",
                    TriggeringJobId = jobId, LatestJobId = jobId,
                    TriggeredAt = now, LastSeenAt = now,
                    PolicyApplied = policyApplied,
                };
                await _repo.CreateAsync(newAlert, ct);
                // A brand-new alert can't be snoozed yet (no prior state). Policy suppression
                // is the only blocker here.
                if (!policySuppressed)
                    _ = _notifications.NotifyAsync("alert.created", newAlert, CancellationToken.None);
            }
        }
    }

    private static bool IsSnoozed(AlertDto alert, DateTimeOffset now)
        => alert.SnoozedUntil is { } until && until > now;

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
                _ = _notifications.NotifyAsync("alert.acknowledged", alert, CancellationToken.None);
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
                _ = _notifications.NotifyAsync("alert.resolved", alert, CancellationToken.None);
        }
        return ok;
    }

    public Task<bool> SetSnoozedUntilAsync(Guid id, DateTimeOffset? snoozedUntil, CancellationToken ct = default)
        => _repo.SetSnoozedUntilAsync(id, snoozedUntil, ct);

    public static int SeverityRank(string? s) => s switch
    {
        "critical" => 3,
        "warning" => 2,
        "informational" => 1,
        _ => 0
    };
}
