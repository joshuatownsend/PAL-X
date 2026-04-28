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
    /// <summary>
    /// Identifier of the Phase 4 policy rule that last adjusted this alert (escalation or
    /// suppression). NULL means no policy applied. Re-evaluated every job.
    /// </summary>
    public string? PolicyApplied { get; set; }
    /// <summary>
    /// If set and in the future, AlertService skips webhook notifications for this alert.
    /// The alert itself still updates (LastSeenAt, severity, policy) so the audit trail
    /// stays intact. Cleared by passing NULL through the snooze endpoint.
    /// </summary>
    public DateTimeOffset? SnoozedUntil { get; set; }
}
