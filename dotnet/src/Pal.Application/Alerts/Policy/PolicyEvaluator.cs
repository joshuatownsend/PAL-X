using System.Text.Json;
using Pal.Application.Persistence;
using Pal.Engine.Model;

namespace Pal.Application.Alerts.Policy;

/// <summary>
/// Phase 4 v1 policy: looks at the trailing window of completed jobs in the same workspace
/// and escalates a current warning to critical if the rule has fired in 3+ of the last 5 runs
/// (current run inclusive). Suppression is not yet implemented in v1.
/// </summary>
public sealed class PolicyEvaluator : IPolicyEvaluator
{
    private const int WindowSize = 5;
    private const int EscalationThreshold = 3;
    private const string EscalationRuleId = "warning-3of5-critical";

    private readonly IAnalysisRepository _analysis;

    public PolicyEvaluator(IAnalysisRepository analysis) => _analysis = analysis;

    public async Task<PolicyResult> EvaluateAsync(
        Guid workspaceId,
        IReadOnlyList<Finding> findings,
        CancellationToken ct = default)
    {
        var currentWarnings = findings
            .Where(f => string.Equals(f.Severity, "warning", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.RuleId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currentWarnings.Count == 0)
            return PolicyResult.Empty;

        // Pull the prior (WindowSize - 1) completed jobs. The current job is still 'running'
        // at this point, so it won't be in this list. ListJobsAsync respects the EF tenant
        // filter — this method must be called from inside a SetWorkspace block.
        var jobs = await _analysis.ListJobsAsync("completed", ct);
        var priorIds = jobs
            .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
            .Take(WindowSize - 1)
            .Select(j => j.Id)
            .ToList();

        // Need at least (EscalationThreshold - 1) prior runs to even have a chance of meeting
        // the bar. Skip cheap when we can't possibly escalate.
        if (priorIds.Count < EscalationThreshold - 1)
            return PolicyResult.Empty;

        var results = await _analysis.GetResultsAsync(priorIds, ct);
        var priorHits = CountRuleHits(results, currentWarnings);

        var escalations = new Dictionary<string, PolicyEscalation>(StringComparer.OrdinalIgnoreCase);
        foreach (var ruleId in currentWarnings)
        {
            // Current run counts as 1 hit; combined with prior hits, total must be >= threshold.
            var totalHits = 1 + priorHits.GetValueOrDefault(ruleId);
            if (totalHits >= EscalationThreshold)
            {
                escalations[ruleId] = new PolicyEscalation(
                    NewSeverity: "critical",
                    PolicyRuleId: EscalationRuleId,
                    Reason: $"warning fired in {totalHits} of last {priorIds.Count + 1} runs (threshold {EscalationThreshold})");
            }
        }

        return new PolicyResult(
            escalations,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> CountRuleHits(
        IReadOnlyList<AnalysisResultDto> results,
        IReadOnlyList<string> ruleIdsOfInterest)
    {
        var lookup = new HashSet<string>(ruleIdsOfInterest, StringComparer.OrdinalIgnoreCase);
        var hits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in results)
        {
            if (string.IsNullOrWhiteSpace(r.FindingsJson)) continue;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(r.FindingsJson); }
            catch (JsonException) { continue; }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                // Each historical job's findings list is deduped per-rule for this count;
                // a single rule firing twice in one run still counts as one window hit.
                var perJobRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in doc.RootElement.EnumerateArray())
                {
                    if (!f.TryGetProperty("rule_id", out var rid)) continue;
                    var ruleId = rid.GetString();
                    if (ruleId is null || !lookup.Contains(ruleId)) continue;
                    perJobRuleIds.Add(ruleId);
                }

                foreach (var ruleId in perJobRuleIds)
                    hits[ruleId] = hits.GetValueOrDefault(ruleId) + 1;
            }
        }

        return hits;
    }
}
