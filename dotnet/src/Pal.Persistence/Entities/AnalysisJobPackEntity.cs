namespace Pal.Persistence.Entities;

public sealed class AnalysisJobPackEntity
{
    public Guid AnalysisJobId { get; set; }
    public required string PackId { get; set; }
    public required string PackVersion { get; set; }

    public AnalysisJobEntity AnalysisJob { get; set; } = null!;
}
