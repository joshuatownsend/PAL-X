namespace Pal.Application.Diagnostics;

public sealed class DiagnosticInsightDto
{
    // First 16 hex chars (64 bits) of SHA-256(jobId:sourceType:key) — stable across re-runs, not collision-proof
    public required string Id { get; init; }
    public required string Severity { get; init; }        // "critical" | "warning" | "informational"
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Narrative { get; init; }
    public required IReadOnlyList<string> Recommendations { get; init; }
    public required IReadOnlyList<string> AffectedRuleIds { get; init; }
    public required string SourceType { get; init; }      // "finding" | "trend" | "correlation"
    public string? SourceCorrelationKey { get; init; }
    public string? SourceDirection { get; init; }
}
