namespace Pal.Persistence.Entities;

public sealed class UploadEntity
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string SourceType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required string StoragePath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<AnalysisJobEntity> AnalysisJobs { get; set; } = [];
}
