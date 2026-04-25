namespace Pal.Application.Persistence;

public interface IAnalysisRepository
{
    Task<AnalysisJobDto> CreateJobAsync(Guid uploadId, IReadOnlyList<string> packIds, CancellationToken ct = default);
    Task<AnalysisJobDto?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisJobDto>> ListJobsAsync(string? statusFilter, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetQueuedJobIdsAsync(CancellationToken ct = default);

    // Returns true if the claim succeeded (job was in 'queued' state)
    Task<bool> TryClaimJobAsync(Guid id, CancellationToken ct = default);
    Task MarkCompletedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, string reason, CancellationToken ct = default);

    // On startup: reset any jobs stuck in 'running' back to 'queued'
    Task ResetOrphanedJobsAsync(CancellationToken ct = default);

    Task SaveResultAsync(Guid jobId, string summaryJson, string findingsJson, CancellationToken ct = default);
    Task SaveReportAsync(Guid jobId, string format, string storagePath, long sizeBytes, CancellationToken ct = default);
    Task<AnalysisResultDto?> GetResultAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisReportDto>> GetReportsAsync(Guid jobId, CancellationToken ct = default);

    // Pack version pinning: record which pack versions were used
    Task SetJobPackVersionsAsync(Guid jobId, IReadOnlyList<JobPackDto> packs, CancellationToken ct = default);

    // Baseline designation
    Task SetBaselineAsync(Guid jobId, bool isBaseline, string? label, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisJobDto>> ListBaselinesAsync(CancellationToken ct = default);
}
