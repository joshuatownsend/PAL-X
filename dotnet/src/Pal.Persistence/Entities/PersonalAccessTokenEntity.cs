namespace Pal.Persistence.Entities;

public sealed class PersonalAccessTokenEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string TokenHash { get; set; }  // SHA-256 hex of raw token
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
