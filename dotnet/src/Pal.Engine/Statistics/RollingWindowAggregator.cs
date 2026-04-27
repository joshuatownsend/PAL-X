using Pal.Engine.Model;

namespace Pal.Engine.Statistics;

public static class RollingWindowAggregator
{
    public sealed record WindowResult(DateTimeOffset Start, DateTimeOffset End, int SampleCount, double Value);

    public static IReadOnlyList<WindowResult> Compute(
        IReadOnlyList<Sample> samples,
        TimeSpan windowDuration,
        TimeSpan? step,
        string aggregation,
        int minSamples)
    {
        if (samples.Count == 0)
            return [];

        var validSamples = samples
            .Where(s => s.Value.HasValue)
            .OrderBy(s => s.Timestamp)
            .ToList();

        if (validSamples.Count == 0)
            return [];

        var windowStep = step ?? EstimateInterval(validSamples);

        var results = new List<WindowResult>();
        var windowStart = validSamples[0].Timestamp;
        var seriesEnd = validSamples[^1].Timestamp;

        while (windowStart <= seriesEnd)
        {
            var windowEnd = windowStart + windowDuration;
            var inWindow = validSamples
                .Where(s => s.Timestamp >= windowStart && s.Timestamp < windowEnd)
                .Select(s => s.Value!.Value)
                .ToList();

            if (inWindow.Count >= minSamples)
            {
                double value = Aggregate(inWindow, aggregation);
                results.Add(new WindowResult(windowStart, windowEnd, inWindow.Count, value));
            }

            windowStart += windowStep;
        }

        return results;
    }

    private static double Aggregate(List<double> values, string aggregation)
    {
        if (aggregation is "p90" or "p95" or "p99")
        {
            values.Sort();
            return aggregation switch
            {
                "p90" => SeriesStatisticsCalculator.Percentile(values, 90),
                "p95" => SeriesStatisticsCalculator.Percentile(values, 95),
                _ => SeriesStatisticsCalculator.Percentile(values, 99)
            };
        }
        return aggregation switch
        {
            "avg" => values.Average(),
            "min" => values.Min(),
            "max" => values.Max(),
            _ => throw new ArgumentException($"Aggregation '{aggregation}' is not supported for rolling windows")
        };
    }

    private static TimeSpan EstimateInterval(List<Sample> sorted)
    {
        if (sorted.Count < 2) return TimeSpan.FromMinutes(1);
        double totalSeconds = (sorted[^1].Timestamp - sorted[0].Timestamp).TotalSeconds;
        double avgInterval = totalSeconds / (sorted.Count - 1);
        return TimeSpan.FromSeconds(Math.Max(1, avgInterval));
    }
}
