using Pal.Engine.Model;
using Pal.Engine.Statistics;
using Xunit;

namespace Pal.Engine.Tests;

public class RollingWindowAggregatorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Sample S(int minuteOffset, double value) =>
        new(BaseTime.AddMinutes(minuteOffset), value);

    private static IReadOnlyList<Sample> HourlySeries()
    {
        // 61 samples: minute 0 through 60, values 10-70
        var samples = new List<Sample>();
        for (int m = 0; m <= 60; m++)
            samples.Add(S(m, 10 + m));
        return samples;
    }

    [Fact]
    public void Compute_EmptySamples_ReturnsEmpty()
    {
        var result = RollingWindowAggregator.Compute([], TimeSpan.FromMinutes(5), null, "avg", 2);
        Assert.Empty(result);
    }

    [Fact]
    public void Compute_Avg_ReturnsCorrectWindowAverages()
    {
        // 5 samples, 1 minute apart: values 10,20,30,40,50
        var samples = new[] { S(0, 10), S(1, 20), S(2, 30), S(3, 40), S(4, 50) };

        // 3-minute window, 1-minute step
        var results = RollingWindowAggregator.Compute(
            samples, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1), "avg", 1);

        // Window [0,3): samples 10,20,30 → avg 20
        // Window [1,4): samples 20,30,40 → avg 30
        // Window [2,5): samples 30,40,50 → avg 40
        // Window [3,6): samples 40,50 → avg 45
        // Window [4,7): sample 50 → minSamples=1 so included → avg 50
        Assert.True(results.Count >= 3);
        Assert.Equal(20.0, results[0].Value, precision: 10);
        Assert.Equal(30.0, results[1].Value, precision: 10);
        Assert.Equal(40.0, results[2].Value, precision: 10);
    }

    [Fact]
    public void Compute_P95_DetectsTransientSpike()
    {
        // 60-minute series — only 2 samples spike above 90 (at minutes 29 and 30)
        // 2/61 = 3.3%, so full-series p95 stays below 90; but any 5-min window
        // containing both spikes has p95 above 90.
        var samples = new List<Sample>();
        for (int m = 0; m <= 60; m++)
        {
            double v = (m == 29 || m == 30) ? 99.0 : 15.0;
            samples.Add(S(m, v));
        }

        // Verify: full-series p95 should be well below 90
        var sorted = samples.Select(s => s.Value!.Value).OrderBy(v => v).ToList();
        int p95Idx = (int)(0.95 * (sorted.Count - 1));
        Assert.True(sorted[p95Idx] < 90, $"Full-series p95 was {sorted[p95Idx]}, expected < 90");

        // Rolling 5-minute windows should catch the spike
        var windows = RollingWindowAggregator.Compute(
            samples, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), "p95", 2);

        Assert.Contains(windows, w => w.Value > 90);
    }

    [Fact]
    public void Compute_MinSamples_SkipsUnderpopulatedWindows()
    {
        // Only 1 sample
        var samples = new[] { S(0, 50.0) };
        var results = RollingWindowAggregator.Compute(
            samples, TimeSpan.FromMinutes(5), null, "avg", 2);

        Assert.Empty(results);
    }

    [Fact]
    public void Compute_Max_ReturnsMaxInWindow()
    {
        var samples = new[] { S(0, 10), S(1, 99), S(2, 20) };
        var results = RollingWindowAggregator.Compute(
            samples, TimeSpan.FromMinutes(5), null, "max", 1);

        Assert.Contains(results, r => r.Value == 99.0);
    }

    [Fact]
    public void Compute_AllNullValues_ReturnsEmpty()
    {
        var samples = new List<Sample>
        {
            new(DateTimeOffset.UtcNow, null),
            new(DateTimeOffset.UtcNow.AddMinutes(1), null)
        };
        var results = RollingWindowAggregator.Compute(
            samples, TimeSpan.FromMinutes(5), null, "avg", 2);

        Assert.Empty(results);
    }
}
