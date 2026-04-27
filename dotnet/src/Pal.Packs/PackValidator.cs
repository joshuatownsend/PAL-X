using System.Text.RegularExpressions;
using Pal.Engine.Model;

namespace Pal.Packs;

public sealed class PackValidator
{
    private static readonly HashSet<string> ValidSeverities = ["critical", "warning", "informational"];
    private static readonly HashSet<string> ValidCategories = ["cpu", "memory", "disk", "network", "process", "iis", "sql", "system", "collection", "pack-validation"];
    private static readonly HashSet<string> ValidAggregations = ["avg", "min", "max", "p90", "p95", "p99", "trend"];
    private static readonly HashSet<string> ValidWindowAggregations = ["avg", "min", "max", "p90", "p95", "p99"];
    private static readonly HashSet<string> ValidSchemaVersions = ["pal.pack/v1", "pal.pack/v1.1"];
    private static readonly HashSet<string> ValidOperators = ["gt", "lt", "ge", "le", "eq"];
    private static readonly HashSet<string> ValidHcVariables = ["total_physical_memory_mb", "logical_processor_count"];
    private static readonly HashSet<string> ValidPriorities = ["high", "medium", "low"];

    public sealed class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; init; } = [];
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    public ValidationResult Validate(Pack pack)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!ValidSchemaVersions.Contains(pack.SchemaVersion))
            errors.Add($"schema_version '{pack.SchemaVersion}' is not recognized (expected pal.pack/v1 or pal.pack/v1.1)");

        if (string.IsNullOrWhiteSpace(pack.PackId))
            errors.Add("pack_id is required");
        else if (!Regex.IsMatch(pack.PackId, @"^[a-z][a-z0-9-]*$"))
            errors.Add($"pack_id '{pack.PackId}' must be kebab-case (lowercase letters, digits, hyphens)");

        if (string.IsNullOrWhiteSpace(pack.PackName))
            errors.Add("pack_name is required");

        if (!Regex.IsMatch(pack.Version ?? "", @"^\d+\.\d+\.\d+$"))
            errors.Add($"version '{pack.Version}' must be semver (e.g., 1.0.0)");

        var ruleIds = new HashSet<string>();
        foreach (var rule in pack.Rules)
        {
            string prefix = $"Rule '{rule.RuleId}'";

            if (!ruleIds.Add(rule.RuleId))
                errors.Add($"Duplicate rule_id: '{rule.RuleId}'");

            if (!ValidSeverities.Contains(rule.Severity))
                errors.Add($"{prefix}: invalid severity '{rule.Severity}'");

            if (!ValidCategories.Contains(rule.Category))
                errors.Add($"{prefix}: invalid category '{rule.Category}'");

            if (string.IsNullOrWhiteSpace(rule.Title))
                errors.Add($"{prefix}: title is required");

            if (string.IsNullOrWhiteSpace(rule.Summary))
                errors.Add($"{prefix}: summary is required");

            if (rule.Conditions.Count == 0)
                errors.Add($"{prefix}: at least one condition is required");

            foreach (var cond in rule.Conditions)
            {
                if (!ValidAggregations.Contains(cond.Aggregation))
                    errors.Add($"{prefix}, metric '{cond.Metric}': invalid aggregation '{cond.Aggregation}'");

                if (!ValidOperators.Contains(cond.Operator))
                    errors.Add($"{prefix}, metric '{cond.Metric}': invalid operator '{cond.Operator}'");

                if (cond.DurationPercent is < 0 or > 100)
                    errors.Add($"{prefix}, metric '{cond.Metric}': duration_percent must be 0-100");

                if (cond.Threshold is HostContextThreshold hct && !ValidHcVariables.Contains(hct.HostContextVariable))
                    errors.Add($"{prefix}: unknown host_context variable '{hct.HostContextVariable}'");

                if (cond.Window is not null)
                {
                    if (pack.SchemaVersion != "pal.pack/v1.1")
                        errors.Add($"{prefix}, metric '{cond.Metric}': 'window' requires schema_version pal.pack/v1.1");
                    if (!ValidWindowAggregations.Contains(cond.Aggregation))
                        errors.Add($"{prefix}, metric '{cond.Metric}': aggregation '{cond.Aggregation}' is not supported with 'window' (trend is not windowed)");
                    if (cond.Window.DurationSeconds < 30)
                        errors.Add($"{prefix}, metric '{cond.Metric}': window.duration_seconds must be >= 30");
                    if (cond.Window.StepSeconds.HasValue && cond.Window.StepSeconds.Value < 1)
                        errors.Add($"{prefix}, metric '{cond.Metric}': window.step_seconds must be >= 1");
                    if (cond.Window.StepSeconds.HasValue && cond.Window.StepSeconds.Value > cond.Window.DurationSeconds)
                        errors.Add($"{prefix}, metric '{cond.Metric}': window.step_seconds must be <= duration_seconds");
                    if (cond.Window.MinSamples < 1)
                        errors.Add($"{prefix}, metric '{cond.Metric}': window.min_samples must be >= 1");
                }
            }

            // Validate recommendation ID references
            foreach (var recId in rule.RecommendationIds)
            {
                if (!pack.RecommendationDefs.ContainsKey(recId))
                    errors.Add($"{prefix}: references recommendation '{recId}' which is not defined in the pack");
            }
        }

        // Validate recommendation definitions
        foreach (var (id, def) in pack.RecommendationDefs)
        {
            if (!ValidPriorities.Contains(def.Priority))
                errors.Add($"Recommendation '{id}': invalid priority '{def.Priority}'");
            if (string.IsNullOrWhiteSpace(def.Text))
                errors.Add($"Recommendation '{id}': text is required");
        }

        if (pack.Rules.Count == 0)
            warnings.Add($"Pack '{pack.PackId}' contains no rules");

        return new ValidationResult { Errors = errors, Warnings = warnings };
    }
}
