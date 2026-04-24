using Pal.Engine.Model;
using Pal.Engine.Scoring;
using Xunit;

namespace Pal.Engine.Tests;

public class StatusClassifierTests
{
    private static Finding MakeFinding(string severity, string category) => new()
    {
        FindingId = $"fd_{severity}_{category}",
        PackId = "test-pack",
        RuleId = $"test-{severity}",
        Severity = severity,
        Category = category,
        Title = "Test",
        Summary = "Test",
        Explanation = "",
        EvidenceMetrics = [],
        Recommendations = []
    };

    [Fact]
    public void ClassifyOverall_NoFindings_ReturnsHealthy() =>
        Assert.Equal(ReportStatus.Healthy, StatusClassifier.ClassifyOverall([]));

    [Fact]
    public void ClassifyOverall_OnlyWarnings_ReturnsWarning() =>
        Assert.Equal(ReportStatus.Warning,
            StatusClassifier.ClassifyOverall([MakeFinding("warning", "cpu")]));

    [Fact]
    public void ClassifyOverall_AnyCritical_ReturnsCritical() =>
        Assert.Equal(ReportStatus.Critical,
            StatusClassifier.ClassifyOverall([
                MakeFinding("warning", "cpu"),
                MakeFinding("critical", "memory")
            ]));

    [Fact]
    public void ClassifyByCategory_GroupsCorrectly()
    {
        var findings = new List<Finding>
        {
            MakeFinding("warning", "cpu"),
            MakeFinding("critical", "memory"),
            MakeFinding("informational", "disk")
        };
        var result = StatusClassifier.ClassifyByCategory(findings);

        Assert.Equal(ReportStatus.Warning, result["cpu"]);
        Assert.Equal(ReportStatus.Critical, result["memory"]);
        Assert.Equal(ReportStatus.Healthy, result["disk"]);
    }
}
