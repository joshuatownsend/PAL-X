using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class CompareRepository : ICompareRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDbContextFactory<PalDbContext> _factory;

    public CompareRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<CompareResultDto> CreateAsync(
        Guid baselineJobId, Guid candidateJobId, CompareResultDto result, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = new CompareResultEntity
        {
            Id = Guid.NewGuid(),
            BaselineJobId = baselineJobId,
            CandidateJobId = candidateJobId,
            ResultJson = JsonSerializer.Serialize(result, JsonOpts),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.CompareResults.Add(entity);
        await db.SaveChangesAsync(ct);
        return Hydrate(entity, result);
    }

    public async Task<CompareResultDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.CompareResults.FindAsync([id], ct);
        return entity is null ? null : Deserialize(entity);
    }

    public async Task<IReadOnlyList<CompareResultDto>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entities = await db.CompareResults
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
        return entities.Select(Deserialize).ToList();
    }

    private static CompareResultDto Deserialize(CompareResultEntity entity)
    {
        var result = JsonSerializer.Deserialize<CompareResultDto>(entity.ResultJson, JsonOpts)!;
        return Hydrate(entity, result);
    }

    private static CompareResultDto Hydrate(CompareResultEntity entity, CompareResultDto inner) => new()
    {
        Id = entity.Id,
        BaselineJobId = entity.BaselineJobId,
        CandidateJobId = entity.CandidateJobId,
        CreatedAt = entity.CreatedAt,
        Summary = inner.Summary,
        Diffs = inner.Diffs
    };
}
