using Pal.Engine.Model;
using Pal.Engine.Statistics;
using Xunit;

namespace Pal.Engine.Tests;

public class StatisticsTests
{
    [Fact]
    public void Compute_KnownValues_ReturnsCorrectAggregations()
    {
        var t0 = DateTimeOffset.UtcNow;
        var samples = Enumerable.Range(0, 10)
            .Select(i => new Sample(t0.AddSeconds(i * 15), (double)(i + 1) * 10.0))
            .ToList();

        var stats = SeriesStatisticsCalculator.Compute(samples);

        Assert.Equal(10, stats.Count);
        Assert.Equal(0, stats.MissingSampleCount);
        Assert.Equal(10.0, stats.Min, 1);
        Assert.Equal(100.0, stats.Max, 1);
        Assert.Equal(55.0, stats.Avg, 1);
        Assert.True(stats.StdDev > 0);
    }

    [Fact]
    public void Compute_WithGaps_CountsMissing()
    {
        var t0 = DateTimeOffset.UtcNow;
        var samples = new List<Sample>
        {
            new(t0, 10.0),
            new(t0.AddSeconds(15), null),
            new(t0.AddSeconds(30), 30.0),
        };
        var stats = SeriesStatisticsCalculator.Compute(samples);
        Assert.Equal(3, stats.Count);
        Assert.Equal(1, stats.MissingSampleCount);
        Assert.Equal(10.0, stats.Min, 1);
        Assert.Equal(30.0, stats.Max, 1);
        Assert.Equal(20.0, stats.Avg, 1);
    }

    [Fact]
    public void Compute_SingleSample_DoesNotThrow()
    {
        var stats = SeriesStatisticsCalculator.Compute([new Sample(DateTimeOffset.UtcNow, 42.0)]);
        Assert.Equal(42.0, stats.Avg, 1);
    }

    [Fact]
    public void Compute_AllNulls_ReturnsZeroStats()
    {
        var t0 = DateTimeOffset.UtcNow;
        var stats = SeriesStatisticsCalculator.Compute([
            new Sample(t0, null),
            new Sample(t0.AddSeconds(15), null)
        ]);
        Assert.Equal(2, stats.MissingSampleCount);
        Assert.Equal(0.0, stats.Avg);
    }

    [Theory]
    [InlineData("avg", 55.0)]
    [InlineData("min", 10.0)]
    [InlineData("max", 100.0)]
    public void GetAggregation_ReturnsExpectedValue(string agg, double expected)
    {
        var t0 = DateTimeOffset.UtcNow;
        var samples = Enumerable.Range(0, 10)
            .Select(i => new Sample(t0.AddSeconds(i * 15), (double)(i + 1) * 10.0))
            .ToList();
        var stats = SeriesStatisticsCalculator.Compute(samples);
        Assert.Equal(expected, SeriesStatisticsCalculator.GetAggregation(stats, agg), 1);
    }
}
