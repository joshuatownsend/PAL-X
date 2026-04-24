namespace Pal.Engine.Model;

public sealed class TimeSeries
{
    public required string SeriesId { get; init; }
    public required string CounterPathOriginal { get; init; }
    public required string CanonicalMetric { get; init; }
    public required string? Instance { get; init; }
    public required string? Unit { get; init; }
    public required IReadOnlyList<Sample> Samples { get; init; }
    public SeriesStatistics? Statistics { get; set; }
}
