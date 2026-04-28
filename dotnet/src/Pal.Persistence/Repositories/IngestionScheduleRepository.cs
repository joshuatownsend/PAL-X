using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class IngestionScheduleRepository : IIngestionScheduleRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;
    private readonly ITenantContext _tenant;

    public IngestionScheduleRepository(IDbContextFactory<PalDbContext> factory, ITenantContext tenant)
    {
        _factory = factory;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<IngestionScheduleDto>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entities = await db.IngestionSchedules.OrderBy(s => s.Name).ToListAsync(ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<IngestionScheduleDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.IngestionSchedules.FindAsync([id], ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task CreateAsync(IngestionScheduleDto schedule, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var workspaceId = _tenant.WorkspaceId
            ?? throw new InvalidOperationException("Tenant workspace is not set. Ensure the request passes through the workspace route group.");
        db.IngestionSchedules.Add(new IngestionScheduleEntity
        {
            Id = schedule.Id,
            WorkspaceId = workspaceId,
            Name = schedule.Name,
            IntervalMinutes = schedule.IntervalMinutes,
            SourceConfigJson = schedule.SourceConfigJson,
            PackIds = string.Join(",", schedule.PackIds),
            Enabled = schedule.Enabled,
            LastRunAt = schedule.LastRunAt,
            NextRunAt = schedule.NextRunAt,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(IngestionScheduleDto schedule, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.IngestionSchedules.FindAsync([schedule.Id], ct);
        if (entity is null) return false;

        entity.Name = schedule.Name;
        entity.IntervalMinutes = schedule.IntervalMinutes;
        entity.SourceConfigJson = schedule.SourceConfigJson;
        entity.PackIds = string.Join(",", schedule.PackIds);
        entity.Enabled = schedule.Enabled;
        entity.UpdatedAt = schedule.UpdatedAt;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetEnabledAsync(Guid id, bool enabled, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.IngestionSchedules
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Enabled, enabled)
                .SetProperty(s => s.UpdatedAt, updatedAt),
            ct);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.IngestionSchedules.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<IngestionScheduleDto>> ListDueAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        // Worker context — no tenant set. Scope is implicit via the per-schedule WorkspaceId
        // returned in each DTO, which the worker then SetWorkspace's before invoking other repos.
        var entities = await db.IngestionSchedules
            .IgnoreQueryFilters()
            .Where(s => s.Enabled && (s.NextRunAt == null || s.NextRunAt <= now))
            .ToListAsync(ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task RecordRunAsync(Guid id, DateTimeOffset lastRunAt, DateTimeOffset nextRunAt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.IngestionSchedules
            .IgnoreQueryFilters()  // worker context; bypass tenant filter
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LastRunAt, lastRunAt)
                .SetProperty(s => s.NextRunAt, nextRunAt),
            ct);
    }

    private static IngestionScheduleDto ToDto(IngestionScheduleEntity e) => new()
    {
        Id = e.Id,
        WorkspaceId = e.WorkspaceId,
        Name = e.Name,
        IntervalMinutes = e.IntervalMinutes,
        SourceConfigJson = e.SourceConfigJson,
        PackIds = e.PackIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Enabled = e.Enabled,
        LastRunAt = e.LastRunAt,
        NextRunAt = e.NextRunAt,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}
