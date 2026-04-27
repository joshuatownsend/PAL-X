namespace Pal.Persistence.Entities;

public sealed class AnalysisResultEntity
{
    public Guid AnalysisJobId { get; set; }
    public required string SummaryJson { get; set; }
    public required string FindingsJson { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    // Nullable: only present when job was submitted with IncludeDataset=true
    public string? DatasetStoragePath { get; set; }
    public long? DatasetByteLength { get; set; }
    public bool? DatasetCompressed { get; set; }

    public AnalysisJobEntity AnalysisJob { get; set; } = null!;
}
