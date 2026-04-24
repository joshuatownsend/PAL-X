namespace Pal.Persistence.Entities;

public sealed class AnalysisResultEntity
{
    public Guid AnalysisJobId { get; set; }
    public required string SummaryJson { get; set; }
    public required string FindingsJson { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public AnalysisJobEntity AnalysisJob { get; set; } = null!;
}
