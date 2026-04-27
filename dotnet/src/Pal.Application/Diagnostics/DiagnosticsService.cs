using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pal.Application.Correlation;
using Pal.Application.Persistence;

namespace Pal.Application.Diagnostics;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private const int MaxFindingInsights = 5;
    private const int MaxTrendInsights = 3;
    private const int MaxCorrelationInsights = 3;
    private const int TrendWindow = 10;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAnalysisRepository _analysis;
    private readonly CorrelationService _correlations;

    public DiagnosticsService(IAnalysisRepository analysis, CorrelationService correlations)
    {
        _analysis = analysis;
        _correlations = correlations;
    }

    public async Task<IReadOnlyList<DiagnosticInsightDto>> ForJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var result = await _analysis.GetResultAsync(jobId, ct);
        if (result is null) return [];

        var findings = ParseFindings(result.FindingsJson);
        if (findings.Count == 0) return [];

        var findingKeys = findings
            .Select(f => (Key: MakeKey(f), Finding: f))
            .Where(x => x.Key is not null)
            .ToDictionary(x => x.Key!, x => x.Finding);

        var (trendResult, correlationResult) = await _correlations.ComputeBothAsync(TrendWindow, ct);

        var insights = new List<DiagnosticInsightDto>();

        // Finding-based insights: critical first, then warning
        var actionableFindings = findings
            .Where(f => f.Severity is "critical" or "warning")
            .OrderBy(f => f.Severity == "critical" ? 0 : 1)
            .ThenBy(f => f.Category)
            .Take(MaxFindingInsights);

        foreach (var f in actionableFindings)
            insights.Add(BuildFindingInsight(jobId, f));

        // Trend-based insights: worsening/appearing keys present in this job's findings
        var trendInsights = trendResult.Trends
            .Where(t => (t.Direction is "worsening" or "appearing") && findingKeys.ContainsKey(t.CorrelationKey))
            .Take(MaxTrendInsights);

        foreach (var t in trendInsights)
        {
            var ruleId = ExtractRuleId(t.CorrelationKey);
            insights.Add(new DiagnosticInsightDto
            {
                Id = MakeId(jobId, "trend", t.CorrelationKey),
                Severity = t.LatestSeverity ?? "warning",
                Category = "trend",
                Title = $"{t.Direction.ToUpperInvariant()[0]}{t.Direction[1..]} pattern: {t.CorrelationKey}",
                Narrative = $"This metric has been {t.Direction} across {t.RunCount} of the last {t.TotalRuns} runs, first seen {t.FirstSeen:yyyy-MM-dd}.",
                Recommendations = [$"Review the trend timeline for {t.CorrelationKey} to identify when the pattern began and correlate with deployment or configuration changes."],
                AffectedRuleIds = ruleId is not null ? [ruleId] : [],
                SourceType = "trend",
                SourceCorrelationKey = t.CorrelationKey,
                SourceDirection = t.Direction
            });
        }

        // Correlation-based insights: both-worsening pairs where at least one key is in this job
        var correlationInsights = correlationResult.Pairs
            .Where(p => p.DirectionA == "worsening" && p.DirectionB == "worsening"
                && (findingKeys.ContainsKey(p.KeyA) || findingKeys.ContainsKey(p.KeyB)))
            .Take(MaxCorrelationInsights);

        foreach (var p in correlationInsights)
        {
            var ruleIds = new List<string>();
            if (ExtractRuleId(p.KeyA) is string rA) ruleIds.Add(rA);
            if (ExtractRuleId(p.KeyB) is string rB && !ruleIds.Contains(rB)) ruleIds.Add(rB);

            insights.Add(new DiagnosticInsightDto
            {
                Id = MakeId(jobId, "correlation", $"{p.KeyA}+{p.KeyB}"),
                Severity = "warning",
                Category = "correlation",
                Title = $"Co-worsening pattern: {p.KeyA} and {p.KeyB}",
                Narrative = $"These two metrics worsen together in {p.CoRunCount} of {p.TotalRuns} runs (co-score: {p.CoScore:F2}). Co-occurring degradations often share a root cause.",
                Recommendations = [$"Investigate whether a shared resource (disk, memory bus, lock contention) is causing both {p.KeyA} and {p.KeyB} to degrade together."],
                AffectedRuleIds = ruleIds,
                SourceType = "correlation",
                SourceCorrelationKey = $"{p.KeyA}+{p.KeyB}",
                SourceDirection = "worsening"
            });
        }

        return insights
            .DistinctBy(i => i.Id)
            .ToList();
    }

    private static List<FindingJson> ParseFindings(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<FindingJson>>(json, JsonOpts) ?? [];
    }

    private static string? MakeKey(FindingJson f)
    {
        var metric = f.Evidence?.Metrics?.FirstOrDefault()?.CanonicalMetric;
        if (f.RuleId is null) return null;
        return $"{f.RuleId}:{metric ?? ""}";
    }

    private static string? ExtractRuleId(string correlationKey)
    {
        var idx = correlationKey.IndexOf(':');
        return idx > 0 ? correlationKey[..idx] : null;
    }

    private static DiagnosticInsightDto BuildFindingInsight(Guid jobId, FindingJson f)
    {
        var recs = new List<string>();
        if (f.Recommendations is not null)
        {
            foreach (var r in f.Recommendations)
                if (r.Text is not null) recs.Add(r.Text);
        }
        if (recs.Count == 0)
            recs.Add($"Investigate the root cause of rule '{f.RuleId}' and review recent changes to this system.");

        return new DiagnosticInsightDto
        {
            Id = MakeId(jobId, "finding", f.RuleId ?? f.FindingId ?? "unknown"),
            Severity = f.Severity ?? "warning",
            Category = f.Category ?? "general",
            Title = f.Title ?? f.RuleId ?? "Finding",
            Narrative = f.Summary ?? f.Title ?? string.Empty,
            Recommendations = recs,
            AffectedRuleIds = f.RuleId is not null ? [f.RuleId] : [],
            SourceType = "finding",
            SourceCorrelationKey = null,
            SourceDirection = null
        };
    }

    private static string MakeId(Guid jobId, string sourceType, string key)
    {
        var raw = $"{jobId}:{sourceType}:{key}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private sealed class FindingJson
    {
        public string? FindingId { get; init; }
        public string? RuleId { get; init; }
        public string? Severity { get; init; }
        public string? Category { get; init; }
        public string? Title { get; init; }
        public string? Summary { get; init; }
        public EvidenceJson? Evidence { get; init; }
        public List<RecommendationJson>? Recommendations { get; init; }
    }

    private sealed class EvidenceJson
    {
        public List<EvidenceMetricJson>? Metrics { get; init; }
    }

    private sealed class EvidenceMetricJson
    {
        public string? CanonicalMetric { get; init; }
    }

    private sealed class RecommendationJson
    {
        public string? Text { get; init; }
    }
}
