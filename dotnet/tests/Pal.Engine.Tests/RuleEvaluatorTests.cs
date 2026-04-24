using Pal.Engine.Model;
using Pal.Engine.Rules;
using Xunit;

namespace Pal.Engine.Tests;

public class RuleEvaluatorTests
{
    private static TimeSeries MakeSeries(IEnumerable<double> values)
    {
        var t0 = new DateTimeOffset(2026, 4, 23, 8, 0, 0, TimeSpan.Zero);
        return new TimeSeries
        {
            SeriesId = "ser_001",
            CounterPathOriginal = @"\\SERVER01\Processor(_Total)\% Processor Time",
            CanonicalMetric = "processor.percent_processor_time",
            Instance = "_Total",
            Unit = "percent",
            Samples = values.Select((v, i) => new Sample(t0.AddSeconds(i * 15), v)).ToList()
        };
    }

    [Fact]
    public void Evaluate_AvgGt_FiresWhenAvgExceedsThreshold()
    {
        // All samples at 90% — far above threshold of 80%
        var series = MakeSeries(Enumerable.Repeat(90.0, 20));
        var condition = new Condition
        {
            Metric = "processor.percent_processor_time",
            Instance = "_Total",
            Aggregation = "avg",
            Operator = "gt",
            Threshold = new LiteralThreshold(80.0),
            DurationPercent = 20.0
        };

        var result = RuleEvaluator.Evaluate(condition, series, HostContext.Unknown);

        Assert.True(result.Fired);
        Assert.Equal(90.0, result.ActualValue, 1);
        Assert.Equal(80.0, result.ThresholdValue, 1);
    }

    [Fact]
    public void Evaluate_DurationPercent_DoesNotFireWhenTooFewSamplesBreach()
    {
        // 2 samples above 80% out of 10 = 20% — but duration_percent requires 50%
        var samples = Enumerable.Repeat(50.0, 8).Concat(Enumerable.Repeat(90.0, 2));
        var series = MakeSeries(samples);
        var condition = new Condition
        {
            Metric = "processor.percent_processor_time",
            Aggregation = "avg",
            Operator = "gt",
            Threshold = new LiteralThreshold(80.0),
            DurationPercent = 50.0
        };

        var result = RuleEvaluator.Evaluate(condition, series, HostContext.Unknown);
        Assert.False(result.Fired);
    }

    [Fact]
    public void Evaluate_LtOperator_FiresWhenValueBelowThreshold()
    {
        var series = MakeSeries(Enumerable.Repeat(200.0, 10));
        var condition = new Condition
        {
            Metric = "memory.available_mbytes",
            Aggregation = "avg",
            Operator = "lt",
            Threshold = new LiteralThreshold(300.0),
            DurationPercent = 1.0
        };

        var result = RuleEvaluator.Evaluate(condition, series, HostContext.Unknown);
        Assert.True(result.Fired);
    }

    [Fact]
    public void Evaluate_HostContextThreshold_ResolvesCorrectly()
    {
        var ctx = new HostContext { TotalPhysicalMemoryMb = 8192 };
        // 10% of 8192 MB = 819.2 MB — available = 500, so lt fires
        var series = MakeSeries(Enumerable.Repeat(500.0, 10));
        var condition = new Condition
        {
            Metric = "memory.available_mbytes",
            Aggregation = "avg",
            Operator = "lt",
            Threshold = new HostContextThreshold
            {
                HostContextVariable = "total_physical_memory_mb",
                Factor = 0.10,
                Minimum = 64
            },
            DurationPercent = 1.0
        };

        var result = RuleEvaluator.Evaluate(condition, series, ctx);
        Assert.True(result.Fired);
        Assert.Equal(819.2, result.ThresholdValue, 0);
    }

    [Fact]
    public void Evaluate_HostContextMissing_ReturnsSkipReason()
    {
        var series = MakeSeries(Enumerable.Repeat(500.0, 10));
        var condition = new Condition
        {
            Metric = "memory.available_mbytes",
            Aggregation = "avg",
            Operator = "lt",
            Threshold = new HostContextThreshold
            {
                HostContextVariable = "total_physical_memory_mb",
                Factor = 0.10
            }
        };

        var result = RuleEvaluator.Evaluate(condition, series, HostContext.Unknown);
        Assert.False(result.Fired);
        Assert.NotNull(result.SkipReason);
    }

    [Fact]
    public void Evaluate_BuildsReadableExpression()
    {
        var series = MakeSeries(Enumerable.Repeat(90.0, 10));
        var condition = new Condition
        {
            Metric = "processor.percent_processor_time",
            Instance = "_Total",
            Aggregation = "avg",
            Operator = "gt",
            Threshold = new LiteralThreshold(80.0),
            DurationPercent = 20.0
        };

        var result = RuleEvaluator.Evaluate(condition, series, HostContext.Unknown);
        Assert.Contains("avg(processor.percent_processor_time", result.Expression);
        Assert.Contains("> 80", result.Expression);
        Assert.Contains("20%", result.Expression);
    }
}
