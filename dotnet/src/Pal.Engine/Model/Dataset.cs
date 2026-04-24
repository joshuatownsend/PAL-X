namespace Pal.Engine.Model;

public sealed record Dataset
{
    public required string DatasetId { get; init; }
    public required string? MachineName { get; init; }
    public required string? TimeZone { get; init; }
    public required DateTimeOffset StartTimeUtc { get; init; }
    public required DateTimeOffset EndTimeUtc { get; init; }
    public required double SampleIntervalSeconds { get; init; }
    public required int GapCount { get; init; }
    public required IReadOnlyList<TimeSeries> Series { get; init; }
    public HostContext HostContext { get; init; } = HostContext.Unknown;

    public int SeriesCount => Series.Count;
    public int SampleCount => Series.Sum(s => s.Samples.Count);

    public TimeSeries? FindSeries(string canonicalMetric, string? instance = null)
    {
        foreach (var s in Series)
        {
            if (!s.CanonicalMetric.Equals(canonicalMetric, StringComparison.OrdinalIgnoreCase))
                continue;
            if (instance is not null && !string.Equals(s.Instance, instance, StringComparison.OrdinalIgnoreCase))
                continue;
            return s;
        }
        return null;
    }

    public IEnumerable<TimeSeries> FindAllSeries(string canonicalMetric) =>
        Series.Where(s => s.CanonicalMetric.Equals(canonicalMetric, StringComparison.OrdinalIgnoreCase));
}
