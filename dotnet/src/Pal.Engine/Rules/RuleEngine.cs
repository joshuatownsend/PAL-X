using System.Security.Cryptography;
using System.Text;
using Pal.Engine.Model;
using Pal.Engine.Statistics;

namespace Pal.Engine.Rules;

public sealed class RuleEngine
{
    public sealed class RunResult
    {
        public required IReadOnlyList<Finding> Findings { get; init; }
        public required IReadOnlyList<EngineWarning> Warnings { get; init; }
    }

    public sealed class EngineWarning
    {
        public required string Code { get; init; }
        public required string Message { get; init; }
        public required string Severity { get; init; }
        public object? Details { get; init; }
    }

    public RunResult Run(IReadOnlyList<Pack> packs, Dataset dataset)
    {
        // Precompute stats for all series
        foreach (var series in dataset.Series)
            series.Statistics ??= SeriesStatisticsCalculator.Compute(series.Samples);

        var findings = new List<Finding>();
        var warnings = new List<EngineWarning>();

        foreach (var pack in packs)
        {
            foreach (var rule in pack.Rules)
            {
                var ruleFindings = EvaluateRule(pack, rule, dataset, warnings);
                findings.AddRange(ruleFindings);
            }
        }

        // Sort: severity desc → category asc → rule_id asc → finding_id asc
        findings.Sort(CompareFinding);
        return new RunResult { Findings = findings, Warnings = warnings };
    }

    private IEnumerable<Finding> EvaluateRule(Pack pack, Rule rule, Dataset dataset, List<EngineWarning> warnings)
    {
        // Check host_context availability
        var missing = HostContextResolver.FindMissing(rule, dataset.HostContext);
        if (missing.Any)
        {
            var varList = string.Join(", ", missing.Variables.Select(v => $"host_context.{v}"));
            warnings.Add(new EngineWarning
            {
                Code = "rule.host_context_unavailable",
                Message = $"Rule '{rule.RuleId}' in pack '{pack.PackId}' skipped: {varList} not provided.",
                Severity = "informational",
                Details = new { rule_id = rule.RuleId, pack_id = pack.PackId, missing_variables = missing.Variables }
            });
            yield break;
        }

        // Check rule applicability guard
        if (!IsApplicable(rule.AppliesWhen, dataset))
            yield break;

        // Find candidate series for each condition
        // Currently: each condition targets one metric; all conditions must fire on matching series
        // For a finding, we require ALL conditions to be satisfied
        var conditionResults = new List<(TimeSeries series, RuleEvaluator.Result result, Condition condition)>();

        foreach (var condition in rule.Conditions)
        {
            var candidates = GetCandidateSeries(condition, dataset);
            if (!candidates.Any())
            {
                // metric not present — rule doesn't apply
                yield break;
            }

            bool anyFired = false;
            foreach (var series in candidates)
            {
                var result = RuleEvaluator.Evaluate(condition, series, dataset.HostContext);
                if (result.Fired)
                {
                    conditionResults.Add((series, result, condition));
                    anyFired = true;
                }
            }

            if (!anyFired)
                yield break; // This condition not satisfied — all conditions required
        }

        // Build finding
        var evidenceMetrics = conditionResults.Select(cr => new EvidenceMetric
        {
            SeriesId = cr.series.SeriesId,
            CanonicalMetric = cr.series.CanonicalMetric,
            Statistics = cr.series.Statistics!,
            TriggerDetails = [new TriggerDetail
            {
                Expression = cr.result.Expression,
                Result = true,
                ActualValue = cr.result.ActualValue,
                ExpectedValue = cr.result.ThresholdValue
            }]
        }).ToList();

        var primarySeries = conditionResults[0].series;
        string findingId = ComputeFindingId(rule.RuleId, primarySeries.CanonicalMetric,
            dataset.StartTimeUtc, dataset.EndTimeUtc);

        var recs = BuildRecommendations(pack, rule);

        yield return new Finding
        {
            FindingId = findingId,
            PackId = pack.PackId,
            RuleId = rule.RuleId,
            Severity = rule.Severity,
            Category = rule.Category,
            Title = rule.Title,
            Summary = rule.Summary,
            Explanation = rule.Explanation ?? string.Empty,
            WindowStart = dataset.StartTimeUtc,
            WindowEnd = dataset.EndTimeUtc,
            EvidenceMetrics = evidenceMetrics,
            Recommendations = recs
        };
    }

    private static IEnumerable<TimeSeries> GetCandidateSeries(Condition condition, Dataset dataset)
    {
        return dataset.Series.Where(s =>
        {
            if (!s.CanonicalMetric.Equals(condition.Metric, StringComparison.OrdinalIgnoreCase))
                return false;
            if (condition.Instance is null) return true;
            if (condition.Instance == "*") return true;
            return string.Equals(s.Instance, condition.Instance, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsApplicable(RuleAppliesWhen? guard, Dataset dataset)
    {
        if (guard is null) return true;
        var metricIds = dataset.Series.Select(s => s.CanonicalMetric).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (guard.RequiresAll.Count > 0 && guard.RequiresAll.Any(m => !metricIds.Contains(m)))
            return false;
        if (guard.RequiresAny.Count > 0 && !guard.RequiresAny.Any(m => metricIds.Contains(m)))
            return false;
        return true;
    }

    private static IReadOnlyList<Recommendation> BuildRecommendations(Pack pack, Rule rule)
    {
        return rule.RecommendationIds
            .Select(id => pack.RecommendationDefs.TryGetValue(id, out var def)
                ? new Recommendation { Id = id, Priority = def.Priority, Text = def.Text, Rationale = def.Rationale, Links = def.Links }
                : null)
            .Where(r => r is not null)
            .Cast<Recommendation>()
            .ToList();
    }

    private static string ComputeFindingId(string ruleId, string canonicalMetric,
        DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var input = $"{ruleId}|{canonicalMetric}|{windowStart:O}|{windowEnd:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "fd_" + ToBase32(hash[..10]);
    }

    private static string ToBase32(byte[] bytes)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        var sb = new StringBuilder();
        int bits = 0, val = 0;
        foreach (byte b in bytes)
        {
            val = (val << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(alphabet[(val >> bits) & 0x1F]);
            }
        }
        if (bits > 0) sb.Append(alphabet[(val << (5 - bits)) & 0x1F]);
        return sb.ToString();
    }

    private static int CompareFinding(Finding a, Finding b)
    {
        int sv = SeverityRank(b.Severity) - SeverityRank(a.Severity);
        if (sv != 0) return sv;
        int cat = string.Compare(a.Category, b.Category, StringComparison.Ordinal);
        if (cat != 0) return cat;
        int rid = string.Compare(a.RuleId, b.RuleId, StringComparison.Ordinal);
        if (rid != 0) return rid;
        return string.Compare(a.FindingId, b.FindingId, StringComparison.Ordinal);
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 3,
        "warning" => 2,
        "informational" => 1,
        _ => 0
    };
}
