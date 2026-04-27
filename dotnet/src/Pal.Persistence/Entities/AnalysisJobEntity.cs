namespace Pal.Persistence.Entities;

public sealed class AnalysisJobEntity
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UploadId { get; set; }
    public required string Status { get; set; }
    public string? OptionsJson { get; set; }
    public string? ContextJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }

    public bool IsBaseline { get; set; }
    public string? BaselineLabel { get; set; }

    public UploadEntity Upload { get; set; } = null!;
    public ICollection<AnalysisJobPackEntity> Packs { get; set; } = [];
    public AnalysisResultEntity? Result { get; set; }
    public ICollection<AnalysisReportEntity> Reports { get; set; } = [];
}
