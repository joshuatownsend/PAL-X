using Microsoft.EntityFrameworkCore;
using Pal.Application.Auth;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class TokenRepository : ITokenRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public TokenRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<TokenDto?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = await db.PersonalAccessTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<TokenDto>> ListByUserAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var tokens = await db.PersonalAccessTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        return tokens.Select(ToDto).ToList();
    }

    public async Task<TokenDto> CreateAsync(string userId, string name, string tokenHash, DateTimeOffset? expiresAt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entity = new PersonalAccessTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            TokenHash = tokenHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };
        db.PersonalAccessTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.PersonalAccessTokens
            .Where(t => t.Id == id && t.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task TouchLastUsedAsync(Guid id, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.PersonalAccessTokens
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, now), ct);
    }

    private static TokenDto ToDto(PersonalAccessTokenEntity e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        Name = e.Name,
        TokenHash = e.TokenHash,
        CreatedAt = e.CreatedAt,
        LastUsedAt = e.LastUsedAt,
        ExpiresAt = e.ExpiresAt,
    };
}
