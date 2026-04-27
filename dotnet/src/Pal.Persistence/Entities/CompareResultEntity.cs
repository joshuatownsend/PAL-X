namespace Pal.Persistence.Entities;

public sealed class CompareResultEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid BaselineJobId { get; set; }
    public Guid CandidateJobId { get; set; }
    public required string ResultJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AnalysisJobEntity BaselineJob { get; set; } = null!;
    public AnalysisJobEntity CandidateJob { get; set; } = null!;
}
