using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class WebhookSinkRepository : IWebhookSinkRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public WebhookSinkRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

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
            Id = sink.Id, Name = sink.Name, Url = sink.Url, Secret = sink.Secret,
            Enabled = sink.Enabled, Events = string.Join(",", sink.Events),
            CreatedAt = sink.CreatedAt, UpdatedAt = sink.UpdatedAt,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(WebhookSinkDto sink, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.WebhookSinks
            .Where(s => s.Id == sink.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Name, sink.Name)
                .SetProperty(x => x.Url, sink.Url)
                .SetProperty(x => x.Secret, sink.Secret)
                .SetProperty(x => x.Enabled, sink.Enabled)
                .SetProperty(x => x.Events, string.Join(",", sink.Events))
                .SetProperty(x => x.UpdatedAt, sink.UpdatedAt),
            ct);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.WebhookSinks.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<WebhookSinkDto>> ListEnabledForEventAsync(string eventName, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        // Load enabled sinks and filter in-memory: expected <20 rows, simpler than a DB-side string split.
        var enabled = await db.WebhookSinks.Where(s => s.Enabled).ToListAsync(ct);
        return enabled
            .Where(s => s.Events.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(eventName))
            .Select(ToDto)
            .ToList();
    }

    private static WebhookSinkDto ToDto(WebhookSinkEntity e) => new()
    {
        Id = e.Id, Name = e.Name, Url = e.Url, Secret = e.Secret, Enabled = e.Enabled,
        Events = e.Events.Split(',', StringSplitOptions.RemoveEmptyEntries),
        CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
    };
}
