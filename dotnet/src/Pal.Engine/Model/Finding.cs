namespace Pal.Engine.Model;

/// <summary>
/// One rule firing — what a rule emits when its conditions are met against a dataset.
/// </summary>
/// <remarks>
/// Findings sort deterministically: severity desc → category asc → rule_id asc → finding_id asc.
/// The <see cref="FindingId"/> is a content hash, so the same rule firing on the same data
/// always produces the same id, across reports.
/// </remarks>
public sealed class Finding
{
    /// <summary>Content-hash identifier: <c>base32(SHA-256(rule_id || canonical_metric || window_start || window_end))</c>.</summary>
    public required string FindingId { get; init; }

    /// <summary>Pack that contained the rule.</summary>
    public required string PackId { get; init; }

    /// <summary>Rule whose conditions fired.</summary>
    public required string RuleId { get; init; }

    /// <summary>One of <c>critical</c>, <c>warning</c>, <c>informational</c>.</summary>
    public required string Severity { get; init; }

    /// <summary>One of <c>cpu</c>, <c>memory</c>, <c>disk</c>, <c>network</c>, <c>process</c>, <c>iis</c>, <c>sql</c>, <c>system</c>, <c>collection</c>, <c>pack-validation</c>.</summary>
    public required string Category { get; init; }

    /// <summary>One-line title from the rule.</summary>
    public required string Title { get; init; }

    /// <summary>Short summary shown in the report's findings table.</summary>
    public required string Summary { get; init; }

    /// <summary>Longer paragraph explaining what the signal means and what to investigate.</summary>
    public required string Explanation { get; init; }

    /// <summary>For rolling-window rules (schema v1.1), the start of the window that fired. Null for capture-wide rules.</summary>
    public DateTimeOffset? WindowStart { get; init; }

    /// <summary>For rolling-window rules, the end of the window that fired. Null for capture-wide rules.</summary>
    public DateTimeOffset? WindowEnd { get; init; }

    /// <summary>Series that contributed evidence — one entry per metric matched by the rule's conditions.</summary>
    public required IReadOnlyList<EvidenceMetric> EvidenceMetrics { get; init; }

    /// <summary>Recommendations materialised from the pack's <c>recommendations:</c> map.</summary>
    public required IReadOnlyList<Recommendation> Recommendations { get; init; }
}

/// <summary>The evidence row for one series contributing to a <see cref="Finding"/>.</summary>
public sealed class EvidenceMetric
{
    /// <summary>Identifier of the specific (counter, instance) series within the dataset.</summary>
    public required string SeriesId { get; init; }

    /// <summary>Canonical metric ID this series resolved to (e.g., <c>processor.percent_processor_time</c>).</summary>
    public required string CanonicalMetric { get; init; }

    /// <summary>Summary statistics for the matched samples.</summary>
    public required SeriesStatistics Statistics { get; init; }

    /// <summary>One entry per condition that fired. Multi-condition rules emit multiple rows.</summary>
    public required IReadOnlyList<TriggerDetail> TriggerDetails { get; init; }
}

/// <summary>The "show your work" for one condition — the human-readable expression, the actual value, the threshold.</summary>
public sealed class TriggerDetail
{
    /// <summary>Human-readable form of the condition that fired (e.g., <c>avg(processor.percent_processor_time) &gt; 80 for &gt;= 20% of samples</c>).</summary>
    public required string Expression { get; init; }

    /// <summary>True if the condition fired; false if it didn't (false rows appear for partial multi-condition matches).</summary>
    public required bool Result { get; init; }

    /// <summary>The aggregated value computed from the input.</summary>
    public double? ActualValue { get; init; }

    /// <summary>The threshold the rule compared against.</summary>
    public double? ExpectedValue { get; init; }

    /// <summary>Free-form notes — e.g., the resolved host_context threshold.</summary>
    public string? Notes { get; init; }
}
