namespace Pal.Persistence.Entities;

public sealed class OrgEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<WorkspaceEntity> Workspaces { get; set; } = [];
    public ICollection<OrgMembershipEntity> Members { get; set; } = [];
}
