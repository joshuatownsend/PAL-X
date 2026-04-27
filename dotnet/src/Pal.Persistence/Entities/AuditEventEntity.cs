namespace Pal.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid Id { get; set; }
    public Guid? OrgId { get; set; }  // null for system-generated events (retention runs, etc.)
    public required string EventType { get; set; }
    public required string EntityId { get; set; }
    public required string EventJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? UserId { get; set; }
}
