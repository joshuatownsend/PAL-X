using System.Text.Json;
using System.Text.Json.Serialization;
using Pal.Application.Persistence;

namespace Pal.Application.Trends;

public sealed class TrendAnalyzer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] DirectionOrder =
        ["worsening", "appearing", "stable", "intermittent", "de-escalating", "resolving"];

    public TrendResultDto Analyze(IReadOnlyList<TrendJobEntryDto> jobs)
    {
        if (jobs.Count == 0)
            return new TrendResultDto
            {
                JobCount = 0,
                WindowStart = default,
                WindowEnd = default,
                Trends = []
            };

        // Build per-key timeline: correlationKey → [(jobId, completedAt, severity?)]
        var timelines = new Dictionary<string, List<TrendRunPointDto>>(StringComparer.OrdinalIgnoreCase);

        // Populate every key that appears in any job so absent entries (null) are explicit
        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parsedJobs = jobs.Select(j => (j.JobId, j.CompletedAt, Findings: ParseFindings(j.FindingsJson))).ToList();
        foreach (var (_, _, findings) in parsedJobs)
            foreach (var (key, _) in findings)
                allKeys.Add(key);

        foreach (var key in allKeys)
            timelines[key] = [];

        foreach (var (jobId, completedAt, findings) in parsedJobs)
        {
            foreach (var key in allKeys)
            {
                timelines[key].Add(new TrendRunPointDto
                {
                    JobId = jobId,
                    CompletedAt = completedAt,
                    Severity = findings.TryGetValue(key, out var sev) ? sev : null
                });
            }
        }

        var trends = new List<TrendFindingDto>();
        foreach (var (key, points) in timelines)
        {
            var presentPoints = points.Where(p => p.Severity is not null).ToList();
            if (presentPoints.Count == 0) continue;

            trends.Add(new TrendFindingDto
            {
                CorrelationKey = key,
                Direction = ComputeDirection(points),
                RunCount = presentPoints.Count,
                TotalRuns = points.Count,
                LatestSeverity = presentPoints[^1].Severity,
                FirstSeen = presentPoints[0].CompletedAt,
                LastSeen = presentPoints[^1].CompletedAt,
                RunPoints = points
            });
        }

        trends.Sort((a, b) =>
        {
            int ao = Array.IndexOf(DirectionOrder, a.Direction);
            int bo = Array.IndexOf(DirectionOrder, b.Direction);
            int c = ao.CompareTo(bo);
            return c != 0 ? c : string.Compare(a.CorrelationKey, b.CorrelationKey, StringComparison.Ordinal);
        });

        return new TrendResultDto
        {
            JobCount = jobs.Count,
            WindowStart = jobs[0].CompletedAt,
            WindowEnd = jobs[^1].CompletedAt,
            Trends = trends
        };
    }

    private static string ComputeDirection(List<TrendRunPointDto> points)
    {
        bool firstPresent = points[0].Severity is not null;
        bool lastPresent = points[^1].Severity is not null;
        int presentCount = points.Count(p => p.Severity is not null);

        if (presentCount == points.Count)
        {
            // Always present — compare first vs last severity rank
            int firstRank = SeverityRank(points[0].Severity);
            int lastRank = SeverityRank(points[^1].Severity);
            return lastRank > firstRank ? "worsening"
                 : lastRank < firstRank ? "de-escalating"
                 : "stable";
        }

        if (!firstPresent && lastPresent) return "appearing";
        if (firstPresent && !lastPresent) return "resolving";
        return "intermittent";
    }

    private static int SeverityRank(string? severity) => severity switch
    {
        "critical" => 3,
        "warning" => 2,
        "info" or "informational" => 1,
        _ => 0
    };

    private static Dictionary<string, string> ParseFindings(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var findings = JsonSerializer.Deserialize<List<FindingJson>>(json, JsonOpts) ?? [];
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in findings)
        {
            var key = MakeKey(f);
            if (!string.IsNullOrEmpty(key) && f.Severity is not null)
                result.TryAdd(key, f.Severity);
        }
        return result;
    }

    private static string MakeKey(FindingJson f)
    {
        var metric = f.Evidence?.Metrics?.FirstOrDefault()?.CanonicalMetric ?? "";
        return $"{f.RuleId}:{metric}";
    }

    private sealed class FindingJson
    {
        public string? RuleId { get; init; }
        public string? Severity { get; init; }
        public EvidenceJson? Evidence { get; init; }
    }

    private sealed class EvidenceJson
    {
        public List<EvidenceMetricJson>? Metrics { get; init; }
    }

    private sealed class EvidenceMetricJson
    {
        public string? CanonicalMetric { get; init; }
    }
}
