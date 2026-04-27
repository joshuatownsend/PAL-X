using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class WebhookSinkRepository : IWebhookSinkRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;
    private readonly ITenantContext _tenant;

    public WebhookSinkRepository(IDbContextFactory<PalDbContext> factory, ITenantContext tenant)
    {
        _factory = factory;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<WebhookSinkDto>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return (await db.WebhookSinks.OrderBy(s => s.Name).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<WebhookSinkDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.WebhookSinks.FindAsync([id], ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task CreateAsync(WebhookSinkDto sink, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.WebhookSinks.Add(new WebhookSinkEntity
        {
            Id = sink.Id, WorkspaceId = _tenant.WorkspaceId ?? throw new InvalidOperationException("Tenant workspace is not set. Ensure the request passes through the workspace route group."),
            Name = sink.Name, Url = sink.Url, Secret = sink.Secret,
            Enabled = sink.Enabled, Events = string.Join(",", sink.Events),
            CreatedAt = sink.CreatedAt, UpdatedAt = sink.UpdatedAt,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(WebhookSinkDto sink, bool updateSecret, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.WebhookSinks.FindAsync([sink.Id], ct);
        if (entity is null) return false;

        entity.Name = sink.Name;
        entity.Url = sink.Url;
        entity.Enabled = sink.Enabled;
        entity.Events = string.Join(",", sink.Events);
        entity.UpdatedAt = sink.UpdatedAt;
        if (updateSecret) entity.Secret = sink.Secret;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.WebhookSinks.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<WebhookSinkDto>> ListEnabledForEventAsync(string eventName, Guid workspaceId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        // IgnoreQueryFilters because this is called from the alert notification path, which may run in
        // the analysis worker context (no tenant set). Scope manually via workspaceId.
        var enabled = await db.WebhookSinks
            .IgnoreQueryFilters()
            .Where(s => s.WorkspaceId == workspaceId && s.Enabled)
            .ToListAsync(ct);
        return enabled
            .Where(s => s.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(eventName))
            .Select(ToDto)
            .ToList();
    }

    private static WebhookSinkDto ToDto(WebhookSinkEntity e) => new()
    {
        Id = e.Id, Name = e.Name, Url = e.Url, Secret = e.Secret, Enabled = e.Enabled,
        Events = e.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
    };
}
