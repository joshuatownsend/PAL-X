namespace Pal.Engine.Model;

/// <summary>
/// One (counter, instance) series — a sequence of timestamped samples for a single metric.
/// </summary>
/// <remarks>
/// The collector layer produces these from raw counter paths; the engine evaluates rules
/// against them by matching <see cref="CanonicalMetric"/> against the rule's <c>metric</c> field
/// and optionally filtering by <see cref="Instance"/>.
/// </remarks>
public sealed class TimeSeries
{
    /// <summary>Stable per-dataset series identifier.</summary>
    public required string SeriesId { get; init; }

    /// <summary>The raw Windows counter path as captured (e.g., <c>\\WEB-01\Processor(_Total)\% Processor Time</c>).</summary>
    public required string CounterPathOriginal { get; init; }

    /// <summary>The snake_case canonical metric ID this series resolved to. <c>unknown.*</c> if the path didn't match any alias.</summary>
    public required string CanonicalMetric { get; init; }

    /// <summary>Instance name (e.g., <c>_Total</c>, <c>C:</c>). Null if the counter has no instance dimension.</summary>
    public required string? Instance { get; init; }

    /// <summary>Inferred unit string (<c>%</c>, <c>MB</c>, etc.), where determinable.</summary>
    public required string? Unit { get; init; }

    /// <summary>Time-ordered samples for this series.</summary>
    public required IReadOnlyList<Sample> Samples { get; init; }

    /// <summary>Lazily computed summary statistics. Populated by the engine before rule evaluation.</summary>
    public SeriesStatistics? Statistics { get; set; }
}
