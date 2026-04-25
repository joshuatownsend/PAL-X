using System.Text.Json;
using Pal.Engine.Model;
using Pal.Reporting.Json;
using Xunit;

namespace Pal.Reporting.Tests;

public class JsonReportWriterTests
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
        DurationMs = 42,
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
        Summary = "CPU above threshold.",
        Explanation = "Processor was overloaded.",
        EvidenceMetrics = [],
        Recommendations = []
    };

    private static JsonElement WriteToJson(JsonReportWriter.WriteInput input)
    {
        using var ms = new MemoryStream();
        new JsonReportWriter().WriteToStream(input, ms);
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    [Fact]
    public void JsonReport_WriteToStream_ProducesValidJson_WithSchemaVersion()
    {
        var root = WriteToJson(MakeInput());
        Assert.Equal("pal.report/v1", root.GetProperty("schema_version").GetString());
    }

    [Fact]
    public void JsonReport_WriteToStream_OverallStatus_IsHealthy_WhenNoFindings()
    {
        var root = WriteToJson(MakeInput());
        Assert.Equal("healthy", root.GetProperty("summary").GetProperty("overall_status").GetString());
    }

    [Fact]
    public void JsonReport_WriteToStream_OverallStatus_IsWarning_WhenOnlyWarningFinding()
    {
        var root = WriteToJson(MakeInput([MakeFinding("warning")]));
        Assert.Equal("warning", root.GetProperty("summary").GetProperty("overall_status").GetString());
    }

    [Fact]
    public void JsonReport_WriteToStream_OverallStatus_IsCritical_WhenCriticalFinding()
    {
        var root = WriteToJson(MakeInput([MakeFinding("critical")]));
        Assert.Equal("critical", root.GetProperty("summary").GetProperty("overall_status").GetString());
    }

    [Fact]
    public void JsonReport_WriteToStream_FindingCounts_MatchActual()
    {
        Finding[] findings =
        [
            MakeFinding("critical"),
            MakeFinding("warning"),
            MakeFinding("informational")
        ];
        var root = WriteToJson(MakeInput(findings));
        var counts = root.GetProperty("summary").GetProperty("finding_counts");
        Assert.Equal(1, counts.GetProperty("critical").GetInt32());
        Assert.Equal(1, counts.GetProperty("warning").GetInt32());
        Assert.Equal(1, counts.GetProperty("informational").GetInt32());
    }
}
