namespace Pal.Persistence.Entities;

public sealed class WorkspaceAuditEventEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string EventType { get; set; }
    public required string EntityId { get; set; }
    public required string EventJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? UserId { get; set; }
}
