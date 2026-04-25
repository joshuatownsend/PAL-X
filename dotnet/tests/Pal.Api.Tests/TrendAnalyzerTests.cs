using System.Text.Json;
using Pal.Application.Persistence;
using Pal.Application.Trends;
using Xunit;

namespace Pal.Api.Tests;

public class TrendAnalyzerTests
{
    private readonly TrendAnalyzer _analyzer = new();

    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TrendJobEntryDto Job(int dayOffset, params (string ruleId, string severity, string metric)[] findings)
    {
        var items = findings.Select(f => new
        {
            rule_id = f.ruleId,
            severity = f.severity,
            evidence = new { metrics = new[] { new { canonical_metric = f.metric } } }
        });
        return new TrendJobEntryDto
        {
            JobId = Guid.NewGuid(),
            CompletedAt = T0.AddDays(dayOffset),
            FindingsJson = JsonSerializer.Serialize(items)
        };
    }

    [Fact]
    public void Analyze_NoJobs_EmptyResult()
    {
        var result = _analyzer.Analyze([]);
        Assert.Empty(result.Trends);
        Assert.Equal(0, result.JobCount);
    }

    [Fact]
    public void Analyze_SingleJob_FindingsAreStable()
    {
        var result = _analyzer.Analyze([Job(0, ("high-cpu", "warning", "processor.percent_processor_time"))]);
        var t = Assert.Single(result.Trends);
        Assert.Equal("stable", t.Direction);
        Assert.Equal(1, t.RunCount);
        Assert.Equal(1, t.TotalRuns);
    }

    [Fact]
    public void Analyze_FindingAbsentFromAllRuns_NotIncluded()
    {
        var j1 = Job(0);
        var j2 = Job(1);
        var result = _analyzer.Analyze([j1, j2]);
        Assert.Empty(result.Trends);
    }

    [Fact]
    public void Analyze_PresentInAllRuns_SameSeverity_Stable()
    {
        var jobs = new[]
        {
            Job(0, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(1, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(2, ("high-cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("stable", t.Direction);
        Assert.Equal(3, t.RunCount);
        Assert.Equal(3, t.TotalRuns);
    }

    [Fact]
    public void Analyze_SeverityEscalating_Worsening()
    {
        var jobs = new[]
        {
            Job(0, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(1, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(2, ("high-cpu", "critical", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("worsening", t.Direction);
        Assert.Equal("critical", t.LatestSeverity);
    }

    [Fact]
    public void Analyze_SeverityDeclining_DeEscalating()
    {
        var jobs = new[]
        {
            Job(0, ("high-cpu", "critical", "processor.percent_processor_time")),
            Job(1, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(2, ("high-cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("de-escalating", t.Direction);
    }

    [Fact]
    public void Analyze_FindingNewInLaterRuns_Appearing()
    {
        var jobs = new[]
        {
            Job(0),
            Job(1),
            Job(2, ("high-cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("appearing", t.Direction);
        Assert.Equal(1, t.RunCount);
        Assert.Equal(3, t.TotalRuns);
    }

    [Fact]
    public void Analyze_FindingAbsentInLaterRuns_Resolving()
    {
        var jobs = new[]
        {
            Job(0, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(1, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(2)
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("resolving", t.Direction);
        Assert.Equal(2, t.RunCount);
        Assert.Equal(3, t.TotalRuns);
    }

    [Fact]
    public void Analyze_PresentInFirstAndLastButGaps_Intermittent()
    {
        var jobs = new[]
        {
            Job(0, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(1),
            Job(2, ("high-cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("intermittent", t.Direction);
    }

    [Fact]
    public void Analyze_TrendsOrderedWorseningBeforeStableBeforeResolving()
    {
        var jobs = new[]
        {
            Job(0,
                ("rule-a", "warning", "metric.a"),   // worsening
                ("rule-b", "warning", "metric.b"),   // stable
                ("rule-c", "warning", "metric.c")),  // resolving
            Job(1,
                ("rule-a", "warning", "metric.a"),
                ("rule-b", "warning", "metric.b")),
            Job(2,
                ("rule-a", "critical", "metric.a"),
                ("rule-b", "warning", "metric.b"))
        };
        var result = _analyzer.Analyze(jobs);

        var worsening = result.Trends.FirstOrDefault(t => t.Direction == "worsening");
        var stable = result.Trends.FirstOrDefault(t => t.Direction == "stable");
        var resolving = result.Trends.FirstOrDefault(t => t.Direction == "resolving");

        Assert.NotNull(worsening);
        Assert.NotNull(stable);
        Assert.NotNull(resolving);

        int wi = result.Trends.ToList().IndexOf(worsening!);
        int si = result.Trends.ToList().IndexOf(stable!);
        int ri = result.Trends.ToList().IndexOf(resolving!);

        Assert.True(wi < si, "worsening should come before stable");
        Assert.True(si < ri, "stable should come before resolving");
    }

    [Fact]
    public void Analyze_CorrelationKeyIsRuleColonMetric()
    {
        var result = _analyzer.Analyze([Job(0, ("my-rule", "warning", "memory.available_mbytes"))]);
        var t = Assert.Single(result.Trends);
        Assert.Equal("my-rule:memory.available_mbytes", t.CorrelationKey);
    }

    [Fact]
    public void Analyze_RunPointsIncludedWithNullForAbsentRuns()
    {
        var jobs = new[]
        {
            Job(0, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(1),
            Job(2, ("high-cpu", "critical", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);

        Assert.Equal(3, t.RunPoints.Count);
        Assert.Equal("warning", t.RunPoints[0].Severity);
        Assert.Null(t.RunPoints[1].Severity);
        Assert.Equal("critical", t.RunPoints[2].Severity);
    }

    [Fact]
    public void Analyze_WindowStartAndEndMatchOldestAndNewest()
    {
        var j1 = Job(0, ("r", "warning", "m"));
        var j2 = Job(5, ("r", "warning", "m"));
        var result = _analyzer.Analyze([j1, j2]);
        Assert.Equal(j1.CompletedAt, result.WindowStart);
        Assert.Equal(j2.CompletedAt, result.WindowEnd);
    }

    [Fact]
    public void Analyze_SeverityFluctuatesButSameFirstAndLast_Intermittent()
    {
        // warning → critical → warning: first == last rank but severity fluctuated
        var jobs = new[]
        {
            Job(0, ("high-cpu", "warning", "processor.percent_processor_time")),
            Job(1, ("high-cpu", "critical", "processor.percent_processor_time")),
            Job(2, ("high-cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(jobs);
        var t = Assert.Single(result.Trends);
        Assert.Equal("intermittent", t.Direction);
    }
}
