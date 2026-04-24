namespace Pal.Engine.Model;

public sealed record Pack
{
    public required string PackId { get; init; }
    public required string PackName { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public PackApplicability? Applicability { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> MetricAliases { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    public IReadOnlyDictionary<string, RecommendationDef> RecommendationDefs { get; init; } = new Dictionary<string, RecommendationDef>();
    public required IReadOnlyList<Rule> Rules { get; init; }
}

public sealed class PackApplicability
{
    public bool Always { get; init; }
    public IReadOnlyList<string> RequiresAny { get; init; } = [];
    public IReadOnlyList<string> RequiresAll { get; init; } = [];
}

public sealed class Rule
{
    public required string RuleId { get; init; }
    public required string Severity { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public string? Explanation { get; init; }
    public RuleAppliesWhen? AppliesWhen { get; init; }
    public required IReadOnlyList<Condition> Conditions { get; init; }
    public required IReadOnlyList<string> RecommendationIds { get; init; }
}

public sealed class RuleAppliesWhen
{
    public IReadOnlyList<string> RequiresAny { get; init; } = [];
    public IReadOnlyList<string> RequiresAll { get; init; } = [];
}

public sealed class Condition
{
    public required string Metric { get; init; }
    public string? Instance { get; init; }
    public required string Aggregation { get; init; }
    public required string Operator { get; init; }
    public required ThresholdValue Threshold { get; init; }
    public double DurationPercent { get; init; } = 1.0;
}

public abstract class ThresholdValue
{
    public abstract double Resolve(HostContext ctx);
}

public sealed class LiteralThreshold(double value) : ThresholdValue
{
    public double Value { get; } = value;
    public override double Resolve(HostContext ctx) => Value;
}

public sealed class HostContextThreshold : ThresholdValue
{
    public required string HostContextVariable { get; init; }
    public double Factor { get; init; } = 1.0;
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }

    public override double Resolve(HostContext ctx)
    {
        var baseValue = ctx.Resolve(HostContextVariable)
            ?? throw new InvalidOperationException($"host_context.{HostContextVariable} is not available");
        var result = baseValue * Factor;
        if (Minimum.HasValue && result < Minimum.Value) result = Minimum.Value;
        if (Maximum.HasValue && result > Maximum.Value) result = Maximum.Value;
        return result;
    }
}

public sealed class RecommendationDef
{
    public required string Priority { get; init; }
    public required string Text { get; init; }
    public string? Rationale { get; init; }
    public IReadOnlyList<string> Links { get; init; } = [];
}
