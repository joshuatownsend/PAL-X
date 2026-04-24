using Pal.Engine.Model;

namespace Pal.Engine.Statistics;

public static class SeriesStatisticsCalculator
{
    public static SeriesStatistics Compute(IReadOnlyList<Sample> samples)
    {
        var values = samples
            .Where(s => s.Value.HasValue)
            .Select(s => s.Value!.Value)
            .OrderBy(v => v)
            .ToList();

        int total = samples.Count;
        int missing = total - values.Count;

        if (values.Count == 0)
        {
            return new SeriesStatistics
            {
                Count = total, MissingSampleCount = missing,
                Min = 0, Max = 0, Avg = 0, Median = 0,
                P90 = 0, P95 = 0, P99 = 0, StdDev = 0, TrendPerHour = 0
            };
        }

        double avg = values.Average();
        double variance = values.Select(v => (v - avg) * (v - avg)).Average();

        return new SeriesStatistics
        {
            Count = total,
            MissingSampleCount = missing,
            Min = values[0],
            Max = values[^1],
            Avg = avg,
            Median = Percentile(values, 50),
            P90 = Percentile(values, 90),
            P95 = Percentile(values, 95),
            P99 = Percentile(values, 99),
            StdDev = Math.Sqrt(variance),
            TrendPerHour = ComputeTrendPerHour(samples)
        };
    }

    public static double GetAggregation(SeriesStatistics stats, string aggregation) => aggregation switch
    {
        "avg"   => stats.Avg,
        "min"   => stats.Min,
        "max"   => stats.Max,
        "p90"   => stats.P90,
        "p95"   => stats.P95,
        "p99"   => stats.P99,
        "trend" => stats.TrendPerHour,
        _ => throw new ArgumentException($"Unknown aggregation: {aggregation}")
    };

    private static double Percentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 1) return sorted[0];
        double rank = percentile / 100.0 * (sorted.Count - 1);
        int lower = (int)rank;
        double frac = rank - lower;
        if (lower + 1 >= sorted.Count) return sorted[^1];
        return sorted[lower] + frac * (sorted[lower + 1] - sorted[lower]);
    }

    private static double ComputeTrendPerHour(IReadOnlyList<Sample> samples)
    {
        var valid = samples.Where(s => s.Value.HasValue).ToList();
        if (valid.Count < 2) return 0.0;

        double n = valid.Count;
        double sumX = 0, sumY = 0, sumXy = 0, sumX2 = 0;
        var t0 = valid[0].Timestamp;

        foreach (var s in valid)
        {
            double x = (s.Timestamp - t0).TotalHours;
            double y = s.Value!.Value;
            sumX += x; sumY += y; sumXy += x * y; sumX2 += x * x;
        }

        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < double.Epsilon) return 0.0;
        return (n * sumXy - sumX * sumY) / denom;
    }
}
