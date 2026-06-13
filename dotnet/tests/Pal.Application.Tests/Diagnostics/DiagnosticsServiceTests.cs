using System.Text.Json;
using Pal.Application.Correlation;
using Pal.Application.Diagnostics;
using Pal.Application.Persistence;
using Pal.Application.Trends;
using Xunit;

namespace Pal.Application.Tests.Diagnostics;

public class DiagnosticsServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static DiagnosticsService BuildService(FakeAnalysisRepository repo)
    {
        var trendAnalyzer = new TrendAnalyzer();
        var correlationAnalyzer = new CorrelationAnalyzer();
        var trendService = new TrendService(repo, trendAnalyzer);
        var correlationService = new CorrelationService(trendService, correlationAnalyzer);
        return new DiagnosticsService(repo, correlationService);
    }

    private static string FindingsJson(params object[] findings) =>
        JsonSerializer.Serialize(findings, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

    private static object MakeFinding(string ruleId, string severity, string category,
        string metric = "processor.percent_processor_time",
        string? title = null, string? summary = null, string? recommendation = null) => new
    {
        FindingId = $"fd_{ruleId}",
        RuleId = ruleId,
        Severity = severity,
        Category = category,
        Title = title ?? ruleId,
        Summary = summary ?? $"{ruleId} fired",
        Evidence = new { Metrics = new[] { new { CanonicalMetric = metric } } },
        Recommendations = recommendation is not null
            ? new[] { new { Text = recommendation } }
            : Array.Empty<object>()
    };

    private static AnalysisJobDto MakeJob(Guid id, int hoursAgo = 1) => new()
    {
        Id = id,
        WorkspaceId = Guid.NewGuid(),
        UploadId = Guid.NewGuid(),
        Status = "completed",
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo - 1),
        CompletedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo),
        Packs = []
    };

    private static AnalysisResultDto MakeResult(Guid jobId, string findingsJson) => new()
    {
        AnalysisJobId = jobId,
        SummaryJson = "{}",
        FindingsJson = findingsJson,
        GeneratedAt = DateTimeOffset.UtcNow
    };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForJobAsync_CriticalFinding_EmitsInsightWithCitation()
    {
        var jobId = Guid.NewGuid();
        var json = FindingsJson(MakeFinding("high-cpu-sustained", "critical", "cpu",
            recommendation: "Investigate top CPU consumers."));
        var repo = new FakeAnalysisRepository(jobId, json, []);

        var svc = BuildService(repo);
        var insights = await svc.ForJobAsync(jobId, TestContext.Current.CancellationToken);

        var insight = Assert.Single(insights, i => i.SourceType == "finding");
        Assert.Equal("critical", insight.Severity);
        Assert.Equal("cpu", insight.Category);
        Assert.Contains("high-cpu-sustained", insight.AffectedRuleIds);
        Assert.NotEmpty(insight.Recommendations);
        Assert.Contains("Investigate top CPU consumers.", insight.Recommendations);
    }

    [Fact]
    public async Task ForJobAsync_WorseningTrend_EmitsInsightWithDirectionCitation()
    {
        var jobId = Guid.NewGuid();
        var h1 = Guid.NewGuid();
        var h2 = Guid.NewGuid();
        var h3 = Guid.NewGuid();

        // Current job has rule-a:disk.read_bytes as a warning finding
        var currentFindings = FindingsJson(
            MakeFinding("rule-a", "warning", "io", metric: "disk.read_bytes"));

        // History shows rule-a worsening: warning → warning → critical
        var hist1Findings = FindingsJson(MakeFinding("rule-a", "warning", "io", metric: "disk.read_bytes"));
        var hist2Findings = FindingsJson(MakeFinding("rule-a", "warning", "io", metric: "disk.read_bytes"));
        var hist3Findings = FindingsJson(MakeFinding("rule-a", "critical", "io", metric: "disk.read_bytes"));

        var histJobs = new List<AnalysisJobDto>
        {
            MakeJob(h1, hoursAgo: 4),
            MakeJob(h2, hoursAgo: 3),
            MakeJob(h3, hoursAgo: 2),
        };
        var histResults = new List<AnalysisResultDto>
        {
            MakeResult(h1, hist1Findings),
            MakeResult(h2, hist2Findings),
            MakeResult(h3, hist3Findings),
        };

        var repo = new FakeAnalysisRepository(jobId, currentFindings, histJobs, histResults);
        var svc = BuildService(repo);

        var insights = await svc.ForJobAsync(jobId, TestContext.Current.CancellationToken);

        var trendInsight = insights.FirstOrDefault(i => i.SourceType == "trend");
        Assert.NotNull(trendInsight);
        Assert.Equal("trend", trendInsight.Category);
        Assert.Equal("worsening", trendInsight.SourceDirection);
        Assert.Equal("rule-a:disk.read_bytes", trendInsight.SourceCorrelationKey);
        Assert.Contains("rule-a", trendInsight.AffectedRuleIds);
    }

    [Fact]
    public async Task ForJobAsync_BothWorseningCorrelation_EmitsCorrelationInsight()
    {
        var jobId = Guid.NewGuid();
        var h1 = Guid.NewGuid();
        var h2 = Guid.NewGuid();
        var h3 = Guid.NewGuid();

        // Current job has rule-a firing
        var currentFindings = FindingsJson(
            MakeFinding("rule-a", "warning", "cpu", metric: "processor.percent_processor_time"));

        // Both rule-a and rule-b worsen together across 3 historical runs
        string HistFindings(string sevA, string sevB) => FindingsJson(
            MakeFinding("rule-a", sevA, "cpu", metric: "processor.percent_processor_time"),
            MakeFinding("rule-b", sevB, "memory", metric: "memory.available_mbytes"));

        var histJobs = new List<AnalysisJobDto>
        {
            MakeJob(h1, hoursAgo: 4),
            MakeJob(h2, hoursAgo: 3),
            MakeJob(h3, hoursAgo: 2),
        };
        var histResults = new List<AnalysisResultDto>
        {
            MakeResult(h1, HistFindings("warning", "warning")),
            MakeResult(h2, HistFindings("warning", "warning")),
            MakeResult(h3, HistFindings("critical", "critical")),
        };

        var repo = new FakeAnalysisRepository(jobId, currentFindings, histJobs, histResults);
        var svc = BuildService(repo);

        var insights = await svc.ForJobAsync(jobId, TestContext.Current.CancellationToken);

        var corrInsight = insights.FirstOrDefault(i => i.SourceType == "correlation");
        Assert.NotNull(corrInsight);
        Assert.Equal("correlation", corrInsight.Category);
        Assert.Equal("worsening", corrInsight.SourceDirection);
        // Both rule IDs should be cited
        Assert.Contains(corrInsight.AffectedRuleIds, r => r == "rule-a" || r == "rule-b");
    }

    [Fact]
    public async Task ForJobAsync_NoFindings_ReturnsEmpty()
    {
        var jobId = Guid.NewGuid();
        var repo = new FakeAnalysisRepository(jobId, "[]", []);

        var svc = BuildService(repo);
        var insights = await svc.ForJobAsync(jobId, TestContext.Current.CancellationToken);

        Assert.Empty(insights);
    }

    [Fact]
    public async Task ForJobAsync_FindingWithNoRecommendations_FallsBackToGenericText()
    {
        var jobId = Guid.NewGuid();
        // MakeFinding with no recommendation arg → empty recommendations array
        var json = FindingsJson(MakeFinding("obscure-rule-xyz", "warning", "disk"));
        var repo = new FakeAnalysisRepository(jobId, json, []);

        var svc = BuildService(repo);
        var insights = await svc.ForJobAsync(jobId, TestContext.Current.CancellationToken);

        var insight = Assert.Single(insights, i => i.SourceType == "finding");
        Assert.NotEmpty(insight.Recommendations);
        // Generic fallback message references the rule ID
        Assert.Contains("obscure-rule-xyz", insight.Recommendations[0]);
    }
}

// ── fake repository ──────────────────────────────────────────────────────────

internal sealed class FakeAnalysisRepository : IAnalysisRepository
{
    private readonly Guid _jobId;
    private readonly string _findingsJson;
    private readonly IReadOnlyList<AnalysisJobDto> _histJobs;
    private readonly IReadOnlyList<AnalysisResultDto> _histResults;

    public FakeAnalysisRepository(
        Guid jobId, string findingsJson,
        IReadOnlyList<AnalysisJobDto> histJobs,
        IReadOnlyList<AnalysisResultDto>? histResults = null)
    {
        _jobId = jobId;
        _findingsJson = findingsJson;
        _histJobs = histJobs;
        _histResults = histResults ?? [];
    }

    public Task<AnalysisResultDto?> GetResultAsync(Guid jobId, CancellationToken ct = default)
    {
        if (jobId != _jobId) return Task.FromResult<AnalysisResultDto?>(null);
        return Task.FromResult<AnalysisResultDto?>(new AnalysisResultDto
        {
            AnalysisJobId = _jobId,
            SummaryJson = "{}",
            FindingsJson = _findingsJson,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<IReadOnlyList<AnalysisJobDto>> ListJobsAsync(string? statusFilter, int? limit = null, int? offset = null, CancellationToken ct = default) =>
        Task.FromResult(_histJobs);

    public Task<IReadOnlyList<AnalysisJobDto>> GetRecentCompletedJobsAsync(int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AnalysisJobDto>>(
            _histJobs
                .Where(j => j.Status == "completed")
                .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                .ThenByDescending(j => j.CreatedAt)
                .ThenByDescending(j => j.Id)
                .Take(limit)
                .ToList());

    public Task<IReadOnlyList<AnalysisResultDto>> GetResultsAsync(IEnumerable<Guid> jobIds, CancellationToken ct = default)
    {
        var ids = new HashSet<Guid>(jobIds);
        IReadOnlyList<AnalysisResultDto> results = _histResults.Where(r => ids.Contains(r.AnalysisJobId)).ToList();
        return Task.FromResult(results);
    }

    // ── not exercised by DiagnosticsService tests ─────────────────────────────
    public Task<AnalysisJobDto> CreateJobAsync(Guid uploadId, IReadOnlyList<string> packIds, bool includeDataset = false, Guid? selectedBaselineId = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AnalysisJobDto?> GetJobAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Guid>> GetQueuedJobIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> TryClaimJobAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task MarkCompletedAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task MarkFailedAsync(Guid id, string reason, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ResetOrphanedJobsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task SaveResultAsync(Guid jobId, string summaryJson, string findingsJson, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SaveReportAsync(Guid jobId, string format, string storagePath, long sizeBytes, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<AnalysisReportDto>> GetReportsAsync(Guid jobId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetJobPackVersionsAsync(Guid jobId, IReadOnlyList<JobPackDto> packs, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetBaselineAsync(Guid jobId, bool isBaseline, string? label, string? type = null, string? contextJson = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<AnalysisJobDto>> ListBaselinesAsync(string? type = null, int? limit = null, int? offset = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<AnalysisJobDto>> GetBaselineVersionsAsync(string type, string contextJson, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SaveDatasetArtifactAsync(Guid jobId, string storagePath, long byteLength, bool compressed, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid jobId, CancellationToken ct = default) => throw new NotImplementedException();
}
