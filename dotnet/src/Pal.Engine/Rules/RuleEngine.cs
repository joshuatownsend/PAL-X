using System.Security.Cryptography;
using System.Text;
using Pal.Engine.Model;
using Pal.Engine.Statistics;

namespace Pal.Engine.Rules;

/// <summary>
/// The rule engine. Evaluates a set of <see cref="Pack"/>s against a <see cref="Dataset"/> and
/// emits <see cref="Finding"/>s in deterministic sort order.
/// </summary>
/// <remarks>
/// The engine performs no I/O. It takes already-loaded packs and an already-ingested dataset
/// and returns findings. Pack loading lives in <c>Pal.Packs</c>; report writing lives in
/// <c>Pal.Reporting</c>. Findings are sorted: severity desc → category asc → rule_id asc → finding_id asc.
/// </remarks>
public sealed class RuleEngine
{
    /// <summary>The result of a <see cref="Run(IReadOnlyList{Pack}, Dataset)"/> call.</summary>
    public sealed class RunResult
    {
        /// <summary>Findings emitted by every rule that fired, in deterministic sort order.</summary>
        public required IReadOnlyList<Finding> Findings { get; init; }

        /// <summary>Non-fatal issues the engine encountered (e.g., host_context unknown, unmapped counter).</summary>
        public required IReadOnlyList<EngineWarning> Warnings { get; init; }
    }

    /// <summary>A non-fatal issue surfaced by the engine — informational, doesn't fail the run.</summary>
    public sealed class EngineWarning
    {
        /// <summary>Stable identifier (e.g., <c>host_context.unknown</c>, <c>metric.unmapped</c>).</summary>
        public required string Code { get; init; }

        /// <summary>Human-readable description.</summary>
        public required string Message { get; init; }

        /// <summary>One of <c>warning</c> or <c>informational</c>.</summary>
        public required string Severity { get; init; }

        /// <summary>Optional structured context (which rule, which metric, etc.).</summary>
        public object? Details { get; init; }
    }

    /// <summary>
    /// Evaluates the given packs against the given dataset and returns the findings.
    /// </summary>
    /// <param name="packs">Packs to evaluate. Applicability is checked per pack.</param>
    /// <param name="dataset">Dataset to evaluate against. Series statistics are computed eagerly at the start of this method for every series whose <see cref="TimeSeries.Statistics"/> is null.</param>
    /// <returns>Findings (sorted) plus any non-fatal warnings.</returns>
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

        if (!IsApplicable(rule.AppliesWhen, dataset))
            yield break;

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
