namespace Pal.Persistence.Entities;

public sealed class IngestionScheduleEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Name { get; set; }
    public required int IntervalMinutes { get; set; }
    public required string SourceConfigJson { get; set; }
    public required string PackIds { get; set; } // comma-separated, mirrors WebhookSinkEntity.Events
    public bool Enabled { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
