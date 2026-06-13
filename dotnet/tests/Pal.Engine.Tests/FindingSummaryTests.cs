using Pal.Engine.Model;
using Pal.Engine.Scoring;
using Xunit;

namespace Pal.Engine.Tests;

public class FindingSummaryTests
{
    private static Finding MakeFinding(string severity) => new()
    {
        FindingId = $"fd_{severity}",
        PackId = "test-pack",
        RuleId = $"test-{severity}",
        Severity = severity,
        Category = "test",
        Title = "Test",
        Summary = "Test",
        Explanation = "",
        EvidenceMetrics = [],
        Recommendations = []
    };

    [Fact]
    public void CountSeverities_EmptyList_ReturnsAllZero()
    {
        var counts = FindingSummary.CountSeverities([]);
        Assert.Equal(0, counts.Critical);
        Assert.Equal(0, counts.Warning);
        Assert.Equal(0, counts.Informational);
    }

    [Fact]
    public void CountSeverities_MixedList_ReturnsCorrectTriple()
    {
        var findings = new List<Finding>
        {
            MakeFinding("critical"),
            MakeFinding("critical"),
            MakeFinding("warning"),
            MakeFinding("informational"),
            MakeFinding("informational"),
            MakeFinding("informational")
        };
        var counts = FindingSummary.CountSeverities(findings);
        Assert.Equal(2, counts.Critical);
        Assert.Equal(1, counts.Warning);
        Assert.Equal(3, counts.Informational);
    }

    [Fact]
    public void CountSeverities_UnknownSeverity_CountsAsInformational()
    {
        // Anything that is neither "critical" nor "warning" must be tallied as
        // informational — this locks the else-branch semantic that makes the
        // dedup refactor byte-identical with the JSON writer's historical behavior.
        var findings = new List<Finding>
        {
            MakeFinding("critical"),
            MakeFinding("warning"),
            MakeFinding("trace"),   // non-standard — must land in informational
            MakeFinding("bogus"),   // non-standard — must land in informational
        };
        var counts = FindingSummary.CountSeverities(findings);
        Assert.Equal(1, counts.Critical);
        Assert.Equal(1, counts.Warning);
        Assert.Equal(2, counts.Informational);
    }
}
