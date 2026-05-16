namespace Pal.Engine.Model;

/// <summary>
/// The normalised, time-aligned representation of one performance capture.
/// Produced by a collector from a CSV or BLG input; consumed by <see cref="Pal.Engine.Rules.RuleEngine"/>.
/// </summary>
/// <remarks>
/// A dataset is fully deterministic from its inputs — same source bytes produce
/// the same <see cref="DatasetId"/>, the same <see cref="Series"/>, and the same statistics.
/// </remarks>
public sealed record Dataset
{
    /// <summary>Content-hash identifier of the form <c>ds_&lt;first 16 hex chars of SHA-256(input)&gt;</c>.</summary>
    public required string DatasetId { get; init; }

    /// <summary>Machine name as extracted from counter paths, when present.</summary>
    public required string? MachineName { get; init; }

    /// <summary>Capture time zone; reserved — currently informational only.</summary>
    public required string? TimeZone { get; init; }

    /// <summary>Earliest sample timestamp in the capture.</summary>
    public required DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>Latest sample timestamp in the capture.</summary>
    public required DateTimeOffset EndTimeUtc { get; init; }

    /// <summary>Median spacing between samples, in seconds.</summary>
    public required double SampleIntervalSeconds { get; init; }

    /// <summary>Number of detected gaps where adjacent samples are more than ~1.5× the cadence apart.</summary>
    public required int GapCount { get; init; }

    /// <summary>Every ingested (counter, instance) series. The unit of rule evaluation.</summary>
    public required IReadOnlyList<TimeSeries> Series { get; init; }

    /// <summary>Host hardware context (RAM, CPU count). Used for RAM-relative and CPU-count-relative thresholds.</summary>
    public HostContext HostContext { get; init; } = HostContext.Unknown;

    /// <summary>Convenience accessor for <c>Series.Count</c>.</summary>
    public int SeriesCount => Series.Count;

    /// <summary>Total number of samples across all series.</summary>
    public int SampleCount => Series.Sum(s => s.Samples.Count);

    /// <summary>
    /// Returns the first series matching the canonical metric ID and (optionally) instance, or <c>null</c> if none.
    /// Comparisons are case-insensitive.
    /// </summary>
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

    /// <summary>Returns every series matching the canonical metric ID, regardless of instance.</summary>
    public IEnumerable<TimeSeries> FindAllSeries(string canonicalMetric) =>
        Series.Where(s => s.CanonicalMetric.Equals(canonicalMetric, StringComparison.OrdinalIgnoreCase));
}
