namespace Pal.Application.Persistence;

public interface IAnalysisRepository
{
    Task<AnalysisJobDto> CreateJobAsync(Guid uploadId, IReadOnlyList<string> packIds, bool includeDataset = false, Guid? selectedBaselineId = null, CancellationToken ct = default);
    Task<AnalysisJobDto?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisJobDto>> ListJobsAsync(string? statusFilter, int? limit = null, int? offset = null, CancellationToken ct = default);

    // LIMIT and status filter pushed into SQL — prevents materializing every completed
    // job into memory for the trend/policy windowing callers.
    Task<IReadOnlyList<AnalysisJobDto>> GetRecentCompletedJobsAsync(int limit, CancellationToken ct = default);

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
    Task<IReadOnlyList<AnalysisResultDto>> GetResultsAsync(IEnumerable<Guid> jobIds, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisReportDto>> GetReportsAsync(Guid jobId, CancellationToken ct = default);

    // Pack version pinning: record which pack versions were used
    Task SetJobPackVersionsAsync(Guid jobId, IReadOnlyList<JobPackDto> packs, CancellationToken ct = default);

    // Baseline designation
    Task SetBaselineAsync(Guid jobId, bool isBaseline, string? label, string? type = null, string? contextJson = null, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisJobDto>> ListBaselinesAsync(string? type = null, int? limit = null, int? offset = null, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisJobDto>> GetBaselineVersionsAsync(string type, string contextJson, CancellationToken ct = default);

    // Dataset artifact (optional, only present when job was submitted with IncludeDataset=true)
    Task SaveDatasetArtifactAsync(Guid jobId, string storagePath, long byteLength, bool compressed, CancellationToken ct = default);
    Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid jobId, CancellationToken ct = default);
}
