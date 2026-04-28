using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class AlertRepository : IAlertRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public AlertRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<AlertDto?> FindActiveByRuleIdAsync(string ruleId, Guid workspaceId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        // IgnoreQueryFilters because this is called from the analysis worker (no tenant context set).
        // We scope manually via workspaceId so cross-workspace rule IDs don't collide.
        var entity = await db.Alerts
            .IgnoreQueryFilters()
            .Where(a => a.WorkspaceId == workspaceId && a.RuleId == ruleId && a.Status != "resolved")
            .FirstOrDefaultAsync(ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task CreateAsync(AlertDto alert, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Alerts.Add(new AlertEntity
        {
            Id = alert.Id,
            WorkspaceId = alert.WorkspaceId,
            RuleId = alert.RuleId,
            Severity = alert.Severity,
            Category = alert.Category,
            Title = alert.Title,
            Status = alert.Status,
            TriggeringJobId = alert.TriggeringJobId,
            LatestJobId = alert.LatestJobId,
            TriggeredAt = alert.TriggeredAt,
            LastSeenAt = alert.LastSeenAt,
            PolicyApplied = alert.PolicyApplied,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateLatestAsync(Guid id, Guid latestJobId, string severity, DateTimeOffset lastSeenAt, string? policyApplied, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Alerts
            .IgnoreQueryFilters()  // worker context; same rationale as FindActiveByRuleIdAsync
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.LatestJobId, latestJobId)
                .SetProperty(a => a.Severity, severity)
                .SetProperty(a => a.LastSeenAt, lastSeenAt)
                .SetProperty(a => a.PolicyApplied, policyApplied),
            ct);
    }

    public async Task<IReadOnlyList<AlertDto>> ListAsync(string? status, string? severity, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Alerts.AsQueryable();
        if (status is not null) query = query.Where(a => a.Status == status);
        if (severity is not null) query = query.Where(a => a.Severity == severity);
        var entities = await query.OrderByDescending(a => a.LastSeenAt).ToListAsync(ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<AlertDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.Alerts.FindAsync([id], ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<bool> AcknowledgeAsync(Guid id, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Alerts
            .Where(a => a.Id == id && a.Status == "open")
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, "acknowledged")
                .SetProperty(a => a.AcknowledgedAt, now),
            ct);
        return rows > 0;
    }

    public async Task<bool> ResolveAsync(Guid id, string? note, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Alerts
            .Where(a => a.Id == id && a.Status != "resolved")
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, "resolved")
                .SetProperty(a => a.ResolvedAt, now)
                .SetProperty(a => a.ResolutionNote, note),
            ct);
        return rows > 0;
    }

    private static AlertDto ToDto(AlertEntity e) => new()
    {
        Id = e.Id,
        WorkspaceId = e.WorkspaceId,
        RuleId = e.RuleId,
        Severity = e.Severity,
        Category = e.Category,
        Title = e.Title,
        Status = e.Status,
        TriggeringJobId = e.TriggeringJobId,
        LatestJobId = e.LatestJobId,
        TriggeredAt = e.TriggeredAt,
        LastSeenAt = e.LastSeenAt,
        AcknowledgedAt = e.AcknowledgedAt,
        ResolvedAt = e.ResolvedAt,
        ResolutionNote = e.ResolutionNote,
        PolicyApplied = e.PolicyApplied,
    };
}
