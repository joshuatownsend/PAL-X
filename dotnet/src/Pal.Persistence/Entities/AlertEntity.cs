namespace Pal.Persistence.Entities;

public sealed class AlertEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string RuleId { get; set; }
    public required string Severity { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public required string Status { get; set; } // "open" | "acknowledged" | "resolved"
    public Guid TriggeringJobId { get; set; }
    public Guid LatestJobId { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
