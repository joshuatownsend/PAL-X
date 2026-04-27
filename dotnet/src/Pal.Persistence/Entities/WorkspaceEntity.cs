namespace Pal.Persistence.Entities;

public sealed class WorkspaceEntity
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public OrgEntity Org { get; set; } = null!;
}
