namespace Pal.Persistence.Entities;

public sealed class OrgMembershipEntity
{
    public Guid OrgId { get; set; }
    public required string UserId { get; set; }
    // "admin" | "analyst" | "viewer"
    public required string Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public OrgEntity Org { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
