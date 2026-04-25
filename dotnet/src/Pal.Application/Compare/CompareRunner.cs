using System.Text.Json;
using System.Text.Json.Serialization;
using Pal.Application.Persistence;

namespace Pal.Application.Compare;

public sealed class CompareRunner
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Id = Guid.Empty and CreatedAt = default; the repository replaces them when persisting.
    public CompareResultDto Run(
        Guid baselineJobId, string baselineFindingsJson,
        Guid candidateJobId, string candidateFindingsJson)
    {
        var baselineFindings = ParseFindings(baselineFindingsJson);
        var candidateFindings = ParseFindings(candidateFindingsJson);

        var baselineByKey = IndexByKey(baselineFindings);
        var candidateByKey = IndexByKey(candidateFindings);

        var diffs = new List<FindingDiffDto>();

        // Resolved: in baseline, not in candidate
        foreach (var (key, bf) in baselineByKey)
        {
            if (!candidateByKey.TryGetValue(key, out var cf))
            {
                diffs.Add(new FindingDiffDto
                {
                    Status = "resolved",
                    CorrelationKey = key,
                    BaselineFinding = ToSnapshot(bf),
                    CandidateFinding = null
                });
            }
            else
            {
                string status = bf.Severity != cf.Severity ? "severity_changed" : "unchanged";
                diffs.Add(new FindingDiffDto
                {
                    Status = status,
                    CorrelationKey = key,
                    BaselineFinding = ToSnapshot(bf),
                    CandidateFinding = ToSnapshot(cf)
                });
            }
        }

        // New: in candidate, not in baseline
        foreach (var (key, cf) in candidateByKey)
        {
            if (!baselineByKey.ContainsKey(key))
            {
                diffs.Add(new FindingDiffDto
                {
                    Status = "new",
                    CorrelationKey = key,
                    BaselineFinding = null,
                    CandidateFinding = ToSnapshot(cf)
                });
            }
        }

        // Sort: new first, then severity_changed, resolved, unchanged; then by key
        string[] statusOrder = ["new", "severity_changed", "resolved", "unchanged"];
        diffs.Sort((a, b) =>
        {
            int ao = Array.IndexOf(statusOrder, a.Status);
            int bo = Array.IndexOf(statusOrder, b.Status);
            int c = ao.CompareTo(bo);
            return c != 0 ? c : string.Compare(a.CorrelationKey, b.CorrelationKey, StringComparison.Ordinal);
        });

        var summary = new CompareSummaryDto
        {
            NewFindings = diffs.Count(d => d.Status == "new"),
            ResolvedFindings = diffs.Count(d => d.Status == "resolved"),
            UnchangedFindings = diffs.Count(d => d.Status == "unchanged"),
            SeverityChanges = diffs.Count(d => d.Status == "severity_changed")
        };

        return new CompareResultDto
        {
            Id = Guid.Empty,
            BaselineJobId = baselineJobId,
            CandidateJobId = candidateJobId,
            CreatedAt = default,
            Summary = summary,
            Diffs = diffs
        };
    }

    private static Dictionary<string, FindingJson> IndexByKey(List<FindingJson> findings)
    {
        var dict = new Dictionary<string, FindingJson>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in findings)
        {
            var key = MakeKey(f);
            dict.TryAdd(key, f);
        }
        return dict;
    }

    private static string MakeKey(FindingJson f)
    {
        var metric = f.Evidence?.Metrics?.FirstOrDefault()?.CanonicalMetric ?? "";
        return $"{f.RuleId}:{metric}";
    }

    private static FindingSnapshotDto ToSnapshot(FindingJson f) => new()
    {
        FindingId = f.FindingId ?? "",
        RuleId = f.RuleId ?? "",
        Severity = f.Severity ?? "",
        Category = f.Category ?? "",
        Title = f.Title ?? "",
        Summary = f.Summary ?? ""
    };

    private static List<FindingJson> ParseFindings(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<FindingJson>>(json, JsonOpts) ?? [];
    }

    // Minimal deserialization target — only what comparison needs.
    private sealed class FindingJson
    {
        public string? FindingId { get; init; }
        public string? RuleId { get; init; }
        public string? Severity { get; init; }
        public string? Category { get; init; }
        public string? Title { get; init; }
        public string? Summary { get; init; }
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
