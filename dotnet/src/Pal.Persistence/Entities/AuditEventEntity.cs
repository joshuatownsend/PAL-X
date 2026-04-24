namespace Pal.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public required string EntityId { get; set; }
    public required string EventJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
