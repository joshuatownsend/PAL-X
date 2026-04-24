namespace Pal.Application.Persistence;

public sealed class UploadDto
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string SourceType { get; init; }
    public required long SizeBytes { get; init; }
    public required string Sha256 { get; init; }
    public required string StoragePath { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class AnalysisJobDto
{
    public required Guid Id { get; init; }
    public required Guid UploadId { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public required IReadOnlyList<JobPackDto> Packs { get; init; }
}

public sealed class JobPackDto
{
    public required string PackId { get; init; }
    public required string PackVersion { get; init; }
}

public sealed class AnalysisResultDto
{
    public required Guid AnalysisJobId { get; init; }
    public required string SummaryJson { get; init; }
    public required string FindingsJson { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}

public sealed class AnalysisReportDto
{
    public required Guid Id { get; init; }
    public required Guid AnalysisJobId { get; init; }
    public required string Format { get; init; }
    public required string StoragePath { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class PackSummaryDto
{
    public required string Id { get; init; }
    public required string CurrentVersion { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
}

public sealed class PackVersionDto
{
    public required string PackId { get; init; }
    public required string Version { get; init; }
    public required string StoragePath { get; init; }
}
