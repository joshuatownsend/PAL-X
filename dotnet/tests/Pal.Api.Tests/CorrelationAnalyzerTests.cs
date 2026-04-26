using System.Text.Json;
using Pal.Application.Correlation;
using Pal.Application.Persistence;
using Pal.Application.Trends;
using Xunit;

namespace Pal.Api.Tests;

public class CorrelationAnalyzerTests
{
    private readonly CorrelationAnalyzer _analyzer = new();
    private readonly TrendAnalyzer _trends = new();

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

    private TrendResultDto Trends(params TrendJobEntryDto[] jobs) => _trends.Analyze(jobs);

    // ── empty / degenerate cases ─────────────────────────────────────────

    [Fact]
    public void Analyze_EmptyTrendResult_EmptyPairs()
    {
        var result = _analyzer.Analyze(Trends());
        Assert.Empty(result.Pairs);
        Assert.Equal(0, result.JobCount);
    }

    [Fact]
    public void Analyze_SingleKey_NoPairsProduced()
    {
        var trendResult = Trends(
            Job(0, ("cpu", "warning", "processor.percent_processor_time")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time")));
        var result = _analyzer.Analyze(trendResult);
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Analyze_TwoKeysBothPresentInOneRunOnly_PairExcluded()
    {
        // coRunCount = 1 → below the ≥2 filter
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(1),
            Job(2)
        };
        var result = _analyzer.Analyze(Trends(jobs));
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Analyze_KeyWithOnePresenceRun_ExcludedFromEligible()
    {
        // "cpu" is in all 3 runs; "mem" is in only 1 run (RunCount < 2)
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time")),
            Job(2, ("cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(Trends(jobs));
        Assert.Empty(result.Pairs);
    }

    // ── co-occurrence counting ───────────────────────────────────────────

    [Fact]
    public void Analyze_TwoKeysBothPresentAllRuns_CoScoreIsOne()
    {
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(2, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes"))
        };
        var result = _analyzer.Analyze(Trends(jobs));
        var p = Assert.Single(result.Pairs);
        Assert.Equal(3, p.CoRunCount);
        Assert.Equal(3, p.TotalRuns);
        Assert.Equal(1.0, p.CoScore, precision: 5);
    }

    [Fact]
    public void Analyze_TwoKeysCoPresent2Of4Runs_CoScoreIsHalf()
    {
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(2, ("cpu", "warning", "processor.percent_processor_time")),
            Job(3, ("cpu", "warning", "processor.percent_processor_time"))
        };
        var result = _analyzer.Analyze(Trends(jobs));
        var p = Assert.Single(result.Pairs);
        Assert.Equal(2, p.CoRunCount);
        Assert.Equal(4, p.TotalRuns);
        Assert.Equal(0.5, p.CoScore, precision: 5);
    }

    // ── direction matching ───────────────────────────────────────────────

    [Fact]
    public void Analyze_BothSameDirection_DirectionMatchTrue()
    {
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(2, ("cpu", "critical", "processor.percent_processor_time"),
                    ("mem", "critical", "memory.available_mbytes"))
        };
        var result = _analyzer.Analyze(Trends(jobs));
        var p = Assert.Single(result.Pairs);
        Assert.Equal("worsening", p.DirectionA);
        Assert.Equal("worsening", p.DirectionB);
        Assert.Equal(p.DirectionA, p.DirectionB);
    }

    [Fact]
    public void Analyze_DifferentDirections_DirectionMatchFalse()
    {
        // cpu worsening, mem stable
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(2, ("cpu", "critical", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes"))
        };
        var result = _analyzer.Analyze(Trends(jobs));
        var p = Assert.Single(result.Pairs);
        Assert.NotEqual(p.DirectionA, p.DirectionB);
    }

    // ── ranking / sort order ─────────────────────────────────────────────

    [Fact]
    public void Analyze_DirectionMatchedStablePairRankedBeforeMismatchedPairs()
    {
        // cpu is "worsening"; mem and disk are "stable"
        // mem+disk pair: direction-matched (both stable) → ranked before cpu+mem and cpu+disk (mismatched)
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes"),
                    ("disk", "warning", "physical_disk.avg_disk_sec_read")),
            Job(1, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes"),
                    ("disk", "warning", "physical_disk.avg_disk_sec_read")),
            Job(2, ("cpu", "critical", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes"),
                    ("disk", "warning", "physical_disk.avg_disk_sec_read"))
        };
        var result = _analyzer.Analyze(Trends(jobs));

        var first = result.Pairs[0];
        bool isMemDiskPair =
            (first.KeyA == "mem:memory.available_mbytes" && first.KeyB == "disk:physical_disk.avg_disk_sec_read")
            || (first.KeyA == "disk:physical_disk.avg_disk_sec_read" && first.KeyB == "mem:memory.available_mbytes");
        Assert.True(isMemDiskPair, "mem+disk (both stable, direction-matched) should be ranked first");
    }

    [Fact]
    public void Analyze_DirectionMatchedPairBeforeUnmatched_SameScore()
    {
        // Three keys: A+B both worsening (match), A+C worsening+stable (no match)
        // all co-present in same runs → same coScore
        var jobs = new[]
        {
            Job(0, ("a", "warning", "m.a"), ("b", "warning", "m.b"), ("c", "warning", "m.c")),
            Job(1, ("a", "warning", "m.a"), ("b", "warning", "m.b"), ("c", "warning", "m.c")),
            Job(2, ("a", "critical", "m.a"), ("b", "critical", "m.b"), ("c", "warning", "m.c"))
        };
        var result = _analyzer.Analyze(Trends(jobs));

        // a+b → both worsening, dirMatch=true → must be first
        var first = result.Pairs[0];
        bool isAbPair = (first.KeyA == "a:m.a" && first.KeyB == "b:m.b")
                     || (first.KeyA == "b:m.b" && first.KeyB == "a:m.a");
        Assert.True(isAbPair, "a+b (both worsening) should be ranked first");
    }

    // ── cap at maxPairs ──────────────────────────────────────────────────

    [Fact]
    public void Analyze_MaxPairsCap_ReturnsOnlyMaxPairs()
    {
        // Build 5 keys all co-present in all runs → 10 pairs (5 choose 2)
        var keys = new[] { ("a", "m.a"), ("b", "m.b"), ("c", "m.c"), ("d", "m.d"), ("e", "m.e") };
        (string ruleId, string severity, string metric)[] AllFindings(string sev) =>
            keys.Select(k => (k.Item1, sev, k.Item2)).ToArray();

        var jobs = new[]
        {
            Job(0, AllFindings("warning")),
            Job(1, AllFindings("warning")),
            Job(2, AllFindings("warning"))
        };

        var result = _analyzer.Analyze(Trends(jobs), maxPairs: 3);
        Assert.Equal(3, result.Pairs.Count);
    }

    // ── window metadata ──────────────────────────────────────────────────

    [Fact]
    public void Analyze_WindowMetadataPassedThrough()
    {
        var jobs = new[]
        {
            Job(0, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes")),
            Job(5, ("cpu", "warning", "processor.percent_processor_time"),
                    ("mem", "warning", "memory.available_mbytes"))
        };
        var trendResult = Trends(jobs);
        var result = _analyzer.Analyze(trendResult);
        Assert.Equal(trendResult.JobCount, result.JobCount);
        Assert.Equal(trendResult.WindowStart, result.WindowStart);
        Assert.Equal(trendResult.WindowEnd, result.WindowEnd);
    }
}
