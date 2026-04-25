using System.Text.Json;
using Pal.Application.Compare;
using Pal.Application.Persistence;
using Xunit;

namespace Pal.Api.Tests;

public class CompareRunnerTests
{
    private readonly CompareRunner _runner = new();

    private static string MakeFindings(params (string ruleId, string severity, string metric)[] findings)
    {
        var items = findings.Select(f => new
        {
            finding_id = $"fid_{f.ruleId}",
            rule_id = f.ruleId,
            severity = f.severity,
            category = "cpu",
            title = $"Title for {f.ruleId}",
            summary = "Summary",
            evidence = new
            {
                metrics = new[] { new { canonical_metric = f.metric } }
            }
        });
        return JsonSerializer.Serialize(items);
    }

    [Fact]
    public void Run_EmptyVsEmpty_NoFindings()
    {
        var result = _runner.Run(Guid.NewGuid(), "[]", Guid.NewGuid(), "[]");
        Assert.Empty(result.Diffs);
        Assert.Equal(0, result.Summary.NewFindings);
        Assert.Equal(0, result.Summary.ResolvedFindings);
    }

    [Fact]
    public void Run_NewFindingInCandidate_MarkedNew()
    {
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var baseline = MakeFindings();
        var candidate = MakeFindings(("high-cpu", "warning", "processor.percent_processor_time"));

        var result = _runner.Run(bId, baseline, cId, candidate);

        Assert.Equal(1, result.Summary.NewFindings);
        Assert.Equal(0, result.Summary.ResolvedFindings);
        var diff = Assert.Single(result.Diffs);
        Assert.Equal("new", diff.Status);
        Assert.Null(diff.BaselineFinding);
        Assert.NotNull(diff.CandidateFinding);
        Assert.Equal("high-cpu", diff.CandidateFinding!.RuleId);
    }

    [Fact]
    public void Run_FindingInBaselineOnly_MarkedResolved()
    {
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var baseline = MakeFindings(("high-cpu", "warning", "processor.percent_processor_time"));
        var candidate = MakeFindings();

        var result = _runner.Run(bId, baseline, cId, candidate);

        Assert.Equal(0, result.Summary.NewFindings);
        Assert.Equal(1, result.Summary.ResolvedFindings);
        var diff = Assert.Single(result.Diffs);
        Assert.Equal("resolved", diff.Status);
        Assert.NotNull(diff.BaselineFinding);
        Assert.Null(diff.CandidateFinding);
    }

    [Fact]
    public void Run_SameFindingSameSeverity_MarkedUnchanged()
    {
        var json = MakeFindings(("high-cpu", "warning", "processor.percent_processor_time"));
        var result = _runner.Run(Guid.NewGuid(), json, Guid.NewGuid(), json);

        Assert.Equal(1, result.Summary.UnchangedFindings);
        Assert.Equal("unchanged", result.Diffs[0].Status);
    }

    [Fact]
    public void Run_SameFindingDifferentSeverity_MarkedSeverityChanged()
    {
        var baseline = MakeFindings(("high-cpu", "warning", "processor.percent_processor_time"));
        var candidate = MakeFindings(("high-cpu", "critical", "processor.percent_processor_time"));

        var result = _runner.Run(Guid.NewGuid(), baseline, Guid.NewGuid(), candidate);

        Assert.Equal(1, result.Summary.SeverityChanges);
        var diff = Assert.Single(result.Diffs);
        Assert.Equal("severity_changed", diff.Status);
        Assert.Equal("warning", diff.BaselineFinding!.Severity);
        Assert.Equal("critical", diff.CandidateFinding!.Severity);
    }

    [Fact]
    public void Run_DiffsOrderedNewFirst()
    {
        var baseline = MakeFindings(("rule-a", "warning", "metric.a"), ("rule-b", "warning", "metric.b"));
        var candidate = MakeFindings(("rule-a", "warning", "metric.a"), ("rule-c", "warning", "metric.c"));

        var result = _runner.Run(Guid.NewGuid(), baseline, Guid.NewGuid(), candidate);

        // "new" before "resolved" before "unchanged"
        Assert.Equal("new", result.Diffs[0].Status);
        Assert.Equal("resolved", result.Diffs[1].Status);
        Assert.Equal("unchanged", result.Diffs[2].Status);
    }

    [Fact]
    public void Run_CorrelationKeyIncludesRuleAndMetric()
    {
        var json = MakeFindings(("my-rule", "warning", "memory.available_mbytes"));
        var result = _runner.Run(Guid.NewGuid(), json, Guid.NewGuid(), json);

        Assert.Equal("my-rule:memory.available_mbytes", result.Diffs[0].CorrelationKey);
    }
}
