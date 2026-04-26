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
    public string? OptionsJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? FailureReason { get; init; }
    public required IReadOnlyList<JobPackDto> Packs { get; init; }
    public bool IsBaseline { get; init; }
    public string? BaselineLabel { get; init; }
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

public sealed class FindingSnapshotDto
{
    public required string FindingId { get; init; }
    public required string RuleId { get; init; }
    public required string Severity { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
}

public sealed class FindingDiffDto
{
    // "new" | "resolved" | "unchanged" | "severity_changed"
    public required string Status { get; init; }
    public required string CorrelationKey { get; init; }
    public FindingSnapshotDto? BaselineFinding { get; init; }
    public FindingSnapshotDto? CandidateFinding { get; init; }
}

public sealed class CompareSummaryDto
{
    public required int NewFindings { get; init; }
    public required int ResolvedFindings { get; init; }
    public required int UnchangedFindings { get; init; }
    public required int SeverityChanges { get; init; }
}

public sealed class CompareResultDto
{
    public required Guid Id { get; init; }
    public required Guid BaselineJobId { get; init; }
    public required Guid CandidateJobId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required CompareSummaryDto Summary { get; init; }
    public required IReadOnlyList<FindingDiffDto> Diffs { get; init; }
}

public sealed class TrendJobEntryDto
{
    public required Guid JobId { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required string FindingsJson { get; init; }
}

public sealed class TrendRunPointDto
{
    public required Guid JobId { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required string? Severity { get; init; }
}

public sealed class TrendFindingDto
{
    // "worsening" | "appearing" | "stable" | "intermittent" | "de-escalating" | "resolving"
    public required string Direction { get; init; }
    public required string CorrelationKey { get; init; }
    public required int RunCount { get; init; }
    public required int TotalRuns { get; init; }
    public required string? LatestSeverity { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public required IReadOnlyList<TrendRunPointDto> RunPoints { get; init; }
}

public sealed class TrendResultDto
{
    public required int JobCount { get; init; }
    public required DateTimeOffset WindowStart { get; init; }
    public required DateTimeOffset WindowEnd { get; init; }
    public required IReadOnlyList<TrendFindingDto> Trends { get; init; }
}

public sealed class CorrelationPairDto
{
    public required string KeyA { get; init; }
    public required string KeyB { get; init; }
    public required string DirectionA { get; init; }
    public required string DirectionB { get; init; }
    public required int CoRunCount { get; init; }
    public required int TotalRuns { get; init; }
    public required double CoScore { get; init; }
}

public sealed class CorrelationResultDto
{
    public required int JobCount { get; init; }
    public required DateTimeOffset WindowStart { get; init; }
    public required DateTimeOffset WindowEnd { get; init; }
    public required IReadOnlyList<CorrelationPairDto> Pairs { get; init; }
}

public sealed class AlertDto
{
    public required Guid Id { get; init; }
    public required string RuleId { get; init; }
    public required string Severity { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; } // "open" | "acknowledged" | "resolved"
    public required Guid TriggeringJobId { get; init; }
    public required Guid LatestJobId { get; init; }
    public required DateTimeOffset TriggeredAt { get; init; }
    public required DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
}
