using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class PackRepository : IPackRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public PackRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<PackSummaryDto>> ListPacksAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Packs
            .OrderBy(p => p.Id)
            .Select(p => new PackSummaryDto
            {
                Id = p.Id,
                CurrentVersion = p.CurrentVersion,
                Title = p.Title,
                Status = p.Status
            })
            .ToListAsync(ct);
    }

    public async Task<PackVersionDto?> GetVersionAsync(string packId, string version, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.PackVersions.FindAsync([packId, version], ct);
        return e is null ? null : new PackVersionDto
        {
            PackId = e.PackId,
            Version = e.Version,
            StoragePath = e.StoragePath,
            CreatedAt = e.CreatedAt
        };
    }

    public async Task<IReadOnlyList<PackVersionDto>> ListVersionsAsync(string packId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.PackVersions
            .Where(v => v.PackId == packId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new PackVersionDto
            {
                PackId = v.PackId,
                Version = v.Version,
                StoragePath = v.StoragePath,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<string?> GetVersionYamlPathAsync(string packId, string version, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.PackVersions.FindAsync([packId, version], ct);
        return e?.StoragePath;
    }

    public async Task UpsertPackAsync(string packId, string version, string title, string yamlPath, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var pack = await db.Packs.FindAsync([packId], ct);
        if (pack is null)
        {
            db.Packs.Add(new PackEntity
            {
                Id = packId,
                CurrentVersion = version,
                Title = title,
                Status = "active",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            pack.CurrentVersion = version;
            pack.Title = title;
            pack.UpdatedAt = now;
        }

        var pv = await db.PackVersions.FindAsync([packId, version], ct);
        if (pv is null)
        {
            db.PackVersions.Add(new PackVersionEntity
            {
                PackId = packId,
                Version = version,
                StoragePath = yamlPath,
                CreatedAt = now
            });
        }
        else
        {
            pv.StoragePath = yamlPath;
        }

        await db.SaveChangesAsync(ct);
    }
}
