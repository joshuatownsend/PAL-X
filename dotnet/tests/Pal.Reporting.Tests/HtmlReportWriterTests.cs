using System.Text;
using Pal.Engine.Model;
using Pal.Engine.Rules;
using Pal.Reporting.Html;
using Pal.Reporting.Json;
using Xunit;

namespace Pal.Reporting.Tests;

public class HtmlReportWriterTests
{
    private static JsonReportWriter.WriteInput MakeInput(IReadOnlyList<Finding>? findings = null) => new()
    {
        Dataset = new Dataset
        {
            DatasetId = "ds_test",
            MachineName = "TEST-SERVER",
            TimeZone = "UTC",
            StartTimeUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndTimeUtc   = new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero),
            SampleIntervalSeconds = 5,
            GapCount = 0,
            Series = []
        },
        Findings = findings ?? [],
        PackResolutions = [],
        EngineWarnings = [],
        CollectorWarnings = [],
        InputPath = "test.csv",
        OutputPath = "test.pal-report.json",
        HtmlReportPath = null,
        DurationMs = 0,
        GeneratedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
    };

    private static Finding MakeFinding(string severity) => new()
    {
        FindingId = "fnd_abc123",
        PackId = "windows-core",
        RuleId = "high-cpu-sustained",
        Severity = severity,
        Category = "cpu",
        Title = "Sustained High CPU Utilization",
        Summary = "CPU was above threshold for extended period.",
        Explanation = "The processor was consistently overloaded.",
        EvidenceMetrics = [],
        Recommendations = []
    };

    private static string RenderHtml(JsonReportWriter.WriteInput input)
    {
        using var ms = new MemoryStream();
        HtmlReportWriter.WriteToStream(input, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void HtmlReport_IsUtf8WithoutBom()
    {
        using var ms = new MemoryStream();
        HtmlReportWriter.WriteToStream(MakeInput(), ms);
        var bytes = ms.ToArray();
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "HTML report must not start with a UTF-8 BOM");
    }

    [Fact]
    public void HtmlReport_ContainsDoctype()
    {
        var html = RenderHtml(MakeInput());
        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
    }

    [Fact]
    public void HtmlReport_MachineName_AppearsInTitle()
    {
        var html = RenderHtml(MakeInput());
        Assert.Contains("<title>PAL Report — TEST-SERVER</title>", html);
    }

    [Fact]
    public void HtmlReport_NoFindings_ShowsHealthyStatusColor()
    {
        var html = RenderHtml(MakeInput());
        Assert.Contains("#388e3c", html);
    }

    [Fact]
    public void HtmlReport_CriticalFinding_ShowsCriticalStatusColor()
    {
        var html = RenderHtml(MakeInput([MakeFinding("critical")]));
        Assert.Contains("#d32f2f", html);
    }

    [Fact]
    public void HtmlReport_NoFindings_ShowsHealthyMessage()
    {
        var html = RenderHtml(MakeInput());
        Assert.Contains("No findings.", html);
    }

    [Fact]
    public void HtmlReport_WithFinding_ShowsFindingTitle()
    {
        var html = RenderHtml(MakeInput([MakeFinding("warning")]));
        Assert.Contains("Sustained High CPU Utilization", html);
    }
}
