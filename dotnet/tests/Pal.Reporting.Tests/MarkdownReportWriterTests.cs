using System.Text;
using Pal.Engine.Model;
using Pal.Reporting.Json;
using Pal.Reporting.Markdown;
using Xunit;

namespace Pal.Reporting.Tests;

public class MarkdownReportWriterTests
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
        GeneratedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        InputDigest = "0000000000000000000000000000000000000000000000000000000000000000"
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

    private static string Render(JsonReportWriter.WriteInput input)
    {
        using var ms = new MemoryStream();
        new MarkdownReportWriter().WriteToStream(input, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void MarkdownReport_IsUtf8WithoutBom()
    {
        using var ms = new MemoryStream();
        new MarkdownReportWriter().WriteToStream(MakeInput(), ms);
        var bytes = ms.ToArray();
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "Markdown report must not start with a UTF-8 BOM");
    }

    [Fact]
    public void MarkdownReport_StartsWithH1Heading()
    {
        var md = Render(MakeInput());
        Assert.StartsWith("# PAL Performance Analysis Report", md.TrimStart());
    }

    [Fact]
    public void MarkdownReport_ContainsMachineName()
    {
        var md = Render(MakeInput());
        Assert.Contains("TEST-SERVER", md);
    }

    [Fact]
    public void MarkdownReport_NoFindings_ShowsHealthyMessage()
    {
        var md = Render(MakeInput());
        Assert.Contains("No findings.", md);
    }

    [Fact]
    public void MarkdownReport_WithFinding_ShowsFindingTitle()
    {
        var md = Render(MakeInput([MakeFinding("warning")]));
        Assert.Contains("Sustained High CPU Utilization", md);
    }

    [Fact]
    public void MarkdownReport_CriticalFinding_ShowsCriticalSeverity()
    {
        var md = Render(MakeInput([MakeFinding("critical")]));
        Assert.Contains("[CRITICAL]", md);
    }

    [Fact]
    public void MarkdownReport_ByteIdenticalOnTwoRenders()
    {
        var input = MakeInput([MakeFinding("warning")]);
        using var ms1 = new MemoryStream();
        using var ms2 = new MemoryStream();
        new MarkdownReportWriter().WriteToStream(input, ms1);
        new MarkdownReportWriter().WriteToStream(input, ms2);
        Assert.Equal(ms1.ToArray(), ms2.ToArray());
    }
}
