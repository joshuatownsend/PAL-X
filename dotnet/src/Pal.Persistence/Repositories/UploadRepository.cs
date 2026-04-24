using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class UploadRepository : IUploadRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public UploadRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<UploadDto?> FindBySha256Async(string sha256, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Uploads.FirstOrDefaultAsync(u => u.Sha256 == sha256, ct);
        return e is null ? null : ToDto(e);
    }

    public async Task<UploadDto> CreateAsync(string fileName, string sourceType, long sizeBytes,
        string sha256, string storagePath, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = new UploadEntity
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            SourceType = sourceType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            StoragePath = storagePath,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Uploads.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<UploadDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Uploads.FindAsync([id], ct);
        return e is null ? null : ToDto(e);
    }

    private static UploadDto ToDto(UploadEntity e) => new()
    {
        Id = e.Id,
        FileName = e.FileName,
        SourceType = e.SourceType,
        SizeBytes = e.SizeBytes,
        Sha256 = e.Sha256,
        StoragePath = e.StoragePath,
        CreatedAt = e.CreatedAt
    };
}
