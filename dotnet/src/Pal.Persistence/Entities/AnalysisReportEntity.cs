namespace Pal.Persistence.Entities;

public sealed class AnalysisReportEntity
{
    public Guid Id { get; set; }
    public Guid AnalysisJobId { get; set; }
    public required string Format { get; set; }
    public required string StoragePath { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AnalysisJobEntity AnalysisJob { get; set; } = null!;
}
