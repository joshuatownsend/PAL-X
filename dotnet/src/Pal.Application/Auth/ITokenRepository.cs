namespace Pal.Application.Auth;

public interface ITokenRepository
{
    Task<TokenDto?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<TokenDto>> ListByUserAsync(string userId, CancellationToken ct = default);
    Task<TokenDto> CreateAsync(string userId, string name, string tokenHash, DateTimeOffset? expiresAt, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string userId, CancellationToken ct = default);
    Task TouchLastUsedAsync(Guid id, DateTimeOffset now, CancellationToken ct = default);
}

public sealed class TokenDto
{
    public Guid Id { get; init; }
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
