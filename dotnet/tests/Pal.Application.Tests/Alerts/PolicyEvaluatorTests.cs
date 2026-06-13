using System.Text.Json;
using Pal.Application.Alerts.Policy;
using Pal.Application.Persistence;
using Pal.Engine.Model;
using Xunit;

namespace Pal.Application.Tests.Alerts;

public class PolicyEvaluatorTests
{
    private static readonly Guid Ws = new("11111111-0000-0000-0000-000000000001");

    private static Finding MakeFinding(string ruleId, string severity = "warning") => new()
    {
        FindingId = $"fd_{ruleId}",
        PackId = "windows-core",
        RuleId = ruleId,
        Severity = severity,
        Category = "cpu",
        Title = ruleId,
        Summary = "",
        Explanation = "",
        EvidenceMetrics = [],
        Recommendations = [],
    };

    private static string FindingsJson(params string[] ruleIds) =>
        JsonSerializer.Serialize(ruleIds.Select(r => new
        {
            finding_id = $"fd_{r}",
            pack_id = "windows-core",
            rule_id = r,
            severity = "warning",
            category = "cpu",
            title = r,
        }));

    [Fact]
    public async Task NoCurrentWarnings_ReturnsEmpty()
    {
        var repo = new FakeAnalysisRepo();
        var ev = new PolicyEvaluator(repo);

        var result = await ev.EvaluateAsync(Ws, new[] { MakeFinding("cpu-high", "informational") }, TestContext.Current.CancellationToken);

        Assert.Empty(result.Escalations);
        Assert.Empty(result.NotificationSuppressed);
    }

    [Fact]
    public async Task InsufficientHistory_ReturnsEmpty()
    {
        var repo = new FakeAnalysisRepo();
        // Only 1 prior job exists — threshold needs at least 2 prior hits
        repo.AddJob("rule-a");
        var ev = new PolicyEvaluator(repo);

        var result = await ev.EvaluateAsync(Ws, new[] { MakeFinding("rule-a") }, TestContext.Current.CancellationToken);

        Assert.Empty(result.Escalations);
    }

    [Fact]
    public async Task ThreeOfFive_EscalatesWarningToCritical()
    {
        var repo = new FakeAnalysisRepo();
        // Last 4 prior jobs: rule-a fired in 2 of them — current run = 3rd hit → escalate
        repo.AddJob("rule-a");
        repo.AddJob("rule-a");
        repo.AddJob(/* nothing relevant */);
        repo.AddJob(/* nothing relevant */);
        var ev = new PolicyEvaluator(repo);

        var result = await ev.EvaluateAsync(Ws, new[] { MakeFinding("rule-a") }, TestContext.Current.CancellationToken);

        var esc = Assert.Single(result.Escalations);
        Assert.Equal("rule-a", esc.Key);
        Assert.Equal("critical", esc.Value.NewSeverity);
        Assert.Equal("warning-3of5-critical", esc.Value.PolicyRuleId);
    }

    [Fact]
    public async Task TwoOfFive_DoesNotEscalate()
    {
        var repo = new FakeAnalysisRepo();
        // Only 1 prior hit — current = 2 of 5 → below threshold
        repo.AddJob("rule-a");
        repo.AddJob(/* nothing relevant */);
        repo.AddJob(/* nothing relevant */);
        repo.AddJob(/* nothing relevant */);
        var ev = new PolicyEvaluator(repo);

        var result = await ev.EvaluateAsync(Ws, new[] { MakeFinding("rule-a") }, TestContext.Current.CancellationToken);

        Assert.Empty(result.Escalations);
    }

    [Fact]
    public async Task DuplicateRuleInOneJob_CountsOnce()
    {
        var repo = new FakeAnalysisRepo();
        // One job with rule-a duplicated should still only count as 1 window hit.
        repo.AddJobRaw(JsonSerializer.Serialize(new object[]
        {
            new { rule_id = "rule-a", severity = "warning" },
            new { rule_id = "rule-a", severity = "warning" },
            new { rule_id = "rule-a", severity = "critical" },
        }));
        repo.AddJob(/* nothing relevant */);
        repo.AddJob(/* nothing relevant */);
        repo.AddJob(/* nothing relevant */);
        var ev = new PolicyEvaluator(repo);

        var result = await ev.EvaluateAsync(Ws, new[] { MakeFinding("rule-a") }, TestContext.Current.CancellationToken);

        // 1 prior hit + current = 2 → no escalation
        Assert.Empty(result.Escalations);
    }

    [Fact]
    public async Task MalformedFindingsJson_TreatsAsNoHits()
    {
        var repo = new FakeAnalysisRepo();
        repo.AddJobRaw("{not json");        // garbage — should be ignored, not throw
        repo.AddJob("rule-a");
        repo.AddJob("rule-a");
        repo.AddJob("rule-a");              // 3 prior hits + current = 4 of 5
        var ev = new PolicyEvaluator(repo);

        var result = await ev.EvaluateAsync(Ws, new[] { MakeFinding("rule-a") }, TestContext.Current.CancellationToken);

        // Even with malformed entry, valid jobs still count → escalation
        var esc = Assert.Single(result.Escalations);
        Assert.Equal("critical", esc.Value.NewSeverity);
    }

    // ── fake analysis repo ───────────────────────────────────────────────────

    private sealed class FakeAnalysisRepo : IAnalysisRepository
    {
        private readonly List<(AnalysisJobDto job, AnalysisResultDto? result)> _jobs = [];
        private DateTimeOffset _nextCompleted = DateTimeOffset.UtcNow.AddHours(-10);

        public void AddJob(params string[] ruleIds)
        {
            var json = ruleIds.Length == 0 ? "[]" : FindingsJson(ruleIds);
            AddJobRaw(json);
        }

        public void AddJobRaw(string findingsJson)
        {
            var id = Guid.NewGuid();
            _nextCompleted = _nextCompleted.AddHours(1);
            _jobs.Add((
                new AnalysisJobDto
                {
                    Id = id, WorkspaceId = Ws, UploadId = Guid.NewGuid(),
                    Status = "completed",
                    CreatedAt = _nextCompleted.AddMinutes(-1),
                    CompletedAt = _nextCompleted,
                    Packs = []
                },
                new AnalysisResultDto
                {
                    AnalysisJobId = id,
                    SummaryJson = "{}",
                    FindingsJson = findingsJson,
                    GeneratedAt = _nextCompleted
                }
            ));
        }

        public Task<IReadOnlyList<AnalysisJobDto>> ListJobsAsync(string? statusFilter, int? limit = null, int? offset = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AnalysisJobDto>>(
                _jobs.Where(j => statusFilter is null || j.job.Status == statusFilter)
                     .Select(j => j.job).ToList());

        public Task<IReadOnlyList<AnalysisJobDto>> GetRecentCompletedJobsAsync(int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AnalysisJobDto>>(
                _jobs.Select(j => j.job)
                     .Where(j => j.Status == "completed")
                     .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                     .ThenByDescending(j => j.CreatedAt)
                     .ThenByDescending(j => j.Id)
                     .Take(limit)
                     .ToList());

        public Task<IReadOnlyList<AnalysisResultDto>> GetResultsAsync(IEnumerable<Guid> jobIds, CancellationToken ct = default)
        {
            var ids = jobIds.ToHashSet();
            var results = _jobs.Where(j => ids.Contains(j.job.Id) && j.result is not null)
                               .Select(j => j.result!).ToList();
            return Task.FromResult<IReadOnlyList<AnalysisResultDto>>(results);
        }

        // ── unused interface methods (throw if accidentally exercised) ───────

        public Task<AnalysisJobDto> CreateJobAsync(Guid uploadId, IReadOnlyList<string> packIds, bool includeDataset = false, Guid? selectedBaselineId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AnalysisJobDto?> GetJobAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Guid>> GetQueuedJobIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> TryClaimJobAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task MarkCompletedAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task MarkFailedAsync(Guid id, string reason, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ResetOrphanedJobsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveResultAsync(Guid jobId, string summaryJson, string findingsJson, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveReportAsync(Guid jobId, string format, string storagePath, long sizeBytes, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AnalysisResultDto?> GetResultAsync(Guid jobId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AnalysisReportDto>> GetReportsAsync(Guid jobId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetJobPackVersionsAsync(Guid jobId, IReadOnlyList<JobPackDto> packs, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetBaselineAsync(Guid jobId, bool isBaseline, string? label, string? type = null, string? contextJson = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AnalysisJobDto>> ListBaselinesAsync(string? type = null, int? limit = null, int? offset = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AnalysisJobDto>> GetBaselineVersionsAsync(string type, string contextJson, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveDatasetArtifactAsync(Guid jobId, string storagePath, long byteLength, bool compressed, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid jobId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
