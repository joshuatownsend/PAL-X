namespace Pal.Engine.Model;

public sealed class Finding
{
    public required string FindingId { get; init; }
    public required string PackId { get; init; }
    public required string RuleId { get; init; }
    public required string Severity { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Explanation { get; init; }
    public DateTimeOffset? WindowStart { get; init; }
    public DateTimeOffset? WindowEnd { get; init; }
    public required IReadOnlyList<EvidenceMetric> EvidenceMetrics { get; init; }
    public required IReadOnlyList<Recommendation> Recommendations { get; init; }
}

public sealed class EvidenceMetric
{
    public required string SeriesId { get; init; }
    public required string CanonicalMetric { get; init; }
    public required SeriesStatistics Statistics { get; init; }
    public required IReadOnlyList<TriggerDetail> TriggerDetails { get; init; }
}

public sealed class TriggerDetail
{
    public required string Expression { get; init; }
    public required bool Result { get; init; }
    public double? ActualValue { get; init; }
    public double? ExpectedValue { get; init; }
    public string? Notes { get; init; }
}
