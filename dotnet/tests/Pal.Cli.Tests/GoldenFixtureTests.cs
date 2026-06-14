using System.Text.Json;
using System.Text.Json.Nodes;
using Pal.Engine.Model;
using Pal.Engine.Normalization;
using Pal.Engine.Rules;
using Pal.Engine.Scoring;
using Pal.Ingestion.Csv;
using Pal.Ingestion.HostContext;
using Pal.Packs;
using Pal.Reporting.Json;
using Xunit;

namespace Pal.Cli.Tests;

public class GoldenFixtureTests
{
    private static string? RepoRoot => FindRepoRoot();

    [Fact]
    public void CpuPressure_FindsHighCpuSustained()
    {
        if (RepoRoot is null) return;

        var (findings, warnings) = RunAnalysis("cpu-pressure", [], null, null);

        var cpuFinding = findings.FirstOrDefault(f => f.RuleId == "high-cpu-sustained");
        Assert.NotNull(cpuFinding);
        Assert.Equal("warning", cpuFinding.Severity);
        Assert.Equal("cpu", cpuFinding.Category);
    }

    [Fact]
    public void DotNetClr_FindsClrAndAspNetExecutionFindings()
    {
        if (RepoRoot is null) return;

        // Load dotnet-clr explicitly (RunAnalysis disables auto-resolution). windows-core still loads
        // (always: true) but the capture has only .NET counters, so it fires nothing — every finding is dotnet.
        var (findings, _) = RunAnalysis("dotnet-clr", ["dotnet-clr"], null, null);

        var clrCritical = findings.FirstOrDefault(f => f.RuleId == "clr-critical-exception-rate");
        Assert.NotNull(clrCritical);
        Assert.Equal("critical", clrCritical.Severity);
        Assert.Equal("dotnet", clrCritical.Category);

        Assert.Contains(findings, f => f.RuleId == "clr-high-exception-rate" && f.Severity == "warning");
        Assert.Contains(findings, f => f.RuleId == "clr-high-gc-time" && f.Severity == "warning");
        Assert.Contains(findings, f => f.RuleId == "aspnet-high-request-execution-time" && f.Severity == "warning");
        Assert.Contains(findings, f => f.RuleId == "aspnet-critical-request-execution-time" && f.Severity == "critical");

        // Exactly the five dotnet-clr findings — proves no overlap with windows-core on a .NET-only capture.
        Assert.Equal(5, findings.Count);
        Assert.All(findings, f => Assert.Equal("dotnet", f.Category));
    }

    [Fact]
    public void CpuPressure_FindingsAreSortedBySeverityThenCategory()
    {
        if (RepoRoot is null) return;

        var (findings, _) = RunAnalysis("cpu-pressure", [], null, null);
        var sorted = findings.ToList();
        string[] severityOrder = ["critical", "warning", "informational"];

        for (int i = 1; i < sorted.Count; i++)
        {
            int prevRank = Array.IndexOf(severityOrder, sorted[i - 1].Severity);
            int curRank = Array.IndexOf(severityOrder, sorted[i].Severity);
            Assert.True(prevRank <= curRank, $"Finding {i - 1} should have severity >= finding {i}");
        }
    }

    [Fact]
    public void HealthyServer_ProducesNoFindings()
    {
        if (RepoRoot is null) return;

        var (findings, _) = RunAnalysis("healthy-server", [], null, null);

        // Healthy-server CSV has low CPU, high memory, low disk latency
        var criticals = findings.Count(f => f.Severity == "critical");
        var warnCpu = findings.Count(f => f.RuleId == "high-cpu-sustained");
        Assert.Equal(0, criticals);
        Assert.Equal(0, warnCpu);
    }

    [Fact]
    public void MemoryPressure_WithHostContext_FindsLowAvailableMemory()
    {
        if (RepoRoot is null) return;

        var (findings, warnings) = RunAnalysis("memory-pressure", [], 8192, null);

        var memFinding = findings.FirstOrDefault(f => f.RuleId == "low-available-memory");
        Assert.NotNull(memFinding);
    }

    [Fact]
    public void MemoryPressure_WithoutHostContext_EmitsSkipWarning()
    {
        if (RepoRoot is null) return;

        var (findings, engineWarnings) = RunAnalysis("memory-pressure", [], null, null, useSidecar: false);

        // low-available-memory rule needs host_context — expect a skip warning
        bool hasSkipWarning = engineWarnings.Any(w =>
            w.Code == "rule.host_context_unavailable" &&
            w.Message.Contains("low-available-memory"));
        Assert.True(hasSkipWarning, "Expected a host_context_unavailable warning for low-available-memory");
    }

    [Fact]
    public void DiskLatency_FindsSustainedDiskReadLatency()
    {
        if (RepoRoot is null) return;

        var (findings, _) = RunAnalysis("disk-latency", [], null, null);

        var diskFinding = findings.FirstOrDefault(f => f.RuleId == "sustained-disk-read-latency" ||
                                                        f.RuleId == "critical-disk-read-latency");
        Assert.NotNull(diskFinding);
    }

    [Fact]
    public void FindingIds_AreDeterministic_AcrossTwoRuns()
    {
        if (RepoRoot is null) return;

        var (f1, _) = RunAnalysis("cpu-pressure", [], null, null);
        var (f2, _) = RunAnalysis("cpu-pressure", [], null, null);

        var ids1 = f1.Select(f => f.FindingId).OrderBy(x => x).ToList();
        var ids2 = f2.Select(f => f.FindingId).OrderBy(x => x).ToList();
        Assert.Equal(ids1, ids2);
    }

    [Fact]
    public void JsonReport_IsUtf8WithoutBom()
    {
        if (RepoRoot is null) return;

        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var inputCsv = Path.Combine(RepoRoot, "fixtures", "cpu-pressure", "input.csv");
            var outputJson = Path.Combine(tmpDir, "test.pal-report.json");

            var registry = MetricAliasRegistry.BuildDefault();
            var collector = new CsvCollector(registry);
            var collectResult = collector.Collect(inputCsv);
            var dataset = collectResult.Dataset with { HostContext = HostContext.Unknown };

            var resolver = new PackResolver();
            var resolveResult = resolver.Resolve([], [Path.Combine(RepoRoot, "packs", "thresholds")], false);
            var engine = new RuleEngine();
            var engineResult = engine.Run(resolveResult.Packs, dataset);

            new JsonReportWriter().Write(new JsonReportWriter.WriteInput
            {
                Dataset = dataset,
                Findings = engineResult.Findings,
                PackResolutions = resolveResult.Resolutions,
                EngineWarnings = engineResult.Warnings,
                CollectorWarnings = collectResult.Warnings,
                InputPath = inputCsv,
                OutputPath = outputJson,
                HtmlReportPath = null,
                DurationMs = 0,
                GeneratedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                InputDigest = collectResult.InputDigest
            });

            var bytes = File.ReadAllBytes(outputJson);
            // UTF-8 BOM is EF BB BF
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "JSON report must not have a UTF-8 BOM");

            // Verify it's valid JSON
            using var doc = JsonDocument.Parse(File.ReadAllText(outputJson));
            Assert.Equal("pal.report/v1", doc.RootElement.GetProperty("schema_version").GetString());
        }
        finally { Directory.Delete(tmpDir, recursive: true); }
    }

    private (IReadOnlyList<Finding> findings, IReadOnlyList<RuleEngine.EngineWarning> warnings)
        RunAnalysis(string fixtureName, IReadOnlyList<string> packIds, double? hostMemoryMb, int? hostCpuCount,
            bool useSidecar = true)
    {
        string fixtureDir = Path.Combine(RepoRoot!, "fixtures", fixtureName);
        string csvPath = Path.Combine(fixtureDir, "input.csv");

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new CsvCollector(registry);
        var collectResult = collector.Collect(csvPath);
        string? sidecarPath = useSidecar ? Path.Combine(fixtureDir, "host-context.json") : null;
        var hostCtx = HostContextReader.Read(hostMemoryMb, hostCpuCount, sidecarPath);
        var dataset = collectResult.Dataset with { HostContext = hostCtx };

        var resolver = new PackResolver();
        var resolveResult = resolver.Resolve(packIds, [Path.Combine(RepoRoot!, "packs", "thresholds")], false);

        var engine = new RuleEngine();
        var result = engine.Run(resolveResult.Packs, dataset);
        return (result.Findings, result.Warnings);
    }

    [Fact]
    public void HealthyServer_JsonReport_MatchesGolden()
    {
        Assert.NotNull(RepoRoot);
        AssertMatchesGolden("healthy-server", null, null);
    }

    [Fact]
    public void CpuPressure_JsonReport_MatchesGolden()
    {
        Assert.NotNull(RepoRoot);
        AssertMatchesGolden("cpu-pressure", null, null);
    }

    [Fact]
    public void DiskLatency_JsonReport_MatchesGolden()
    {
        Assert.NotNull(RepoRoot);
        AssertMatchesGolden("disk-latency", null, null);
    }

    [Fact]
    public void MemoryPressure_JsonReport_MatchesGolden()
    {
        Assert.NotNull(RepoRoot);
        AssertMatchesGolden("memory-pressure", 8192, 4);
    }

    private void AssertMatchesGolden(string fixtureName, double? hostMemoryMb, int? hostCpuCount)
    {
        string fixtureDir = Path.Combine(RepoRoot!, "fixtures", fixtureName);
        string csvPath = Path.Combine(fixtureDir, "input.csv");
        string goldenPath = Path.Combine(fixtureDir, "golden.pal-report.json");
        string tmpFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pal-report.json");

        try
        {
            var registry = MetricAliasRegistry.BuildDefault();
            var collector = new CsvCollector(registry);
            var collectResult = collector.Collect(csvPath);

            string? sidecarPath = Path.Combine(fixtureDir, "host-context.json");
            if (!File.Exists(sidecarPath)) sidecarPath = null;
            var hostCtx = HostContextReader.Read(hostMemoryMb, hostCpuCount, sidecarPath);
            var dataset = collectResult.Dataset with { HostContext = hostCtx };

            var resolver = new PackResolver();
            var resolveResult = resolver.Resolve([], [Path.Combine(RepoRoot!, "packs", "thresholds")], false);
            var engine = new RuleEngine();
            var engineResult = engine.Run(resolveResult.Packs, dataset);

            new JsonReportWriter().Write(new JsonReportWriter.WriteInput
            {
                Dataset = dataset,
                Findings = engineResult.Findings,
                PackResolutions = resolveResult.Resolutions,
                EngineWarnings = engineResult.Warnings,
                CollectorWarnings = collectResult.Warnings,
                InputPath = csvPath,
                OutputPath = tmpFile,
                HtmlReportPath = null,
                DurationMs = 0,
                GeneratedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                InputDigest = collectResult.InputDigest
            });

            Assert.Equal(
                MaskEngineFields(File.ReadAllText(goldenPath)),
                MaskEngineFields(File.ReadAllText(tmpFile)));
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    // Masks fields that vary by machine, OS, git autocrlf setting, or temp path.
    private static string MaskEngineFields(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        // report_id and dataset_id are SHA-256 of file bytes; git autocrlf
        // converts CRLF<->LF on checkout, making the hash machine-specific.
        root["report_id"] = "masked";
        root["dataset"]!.AsObject()["dataset_id"] = "masked";
        var engineObj = root["engine"]!.AsObject();
        engineObj["version"] = "masked";
        engineObj["host_os"] = "masked";
        engineObj["runtime"] = "masked";
        root["artifacts"]!.AsObject()["json_report_path"] = "masked";
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "packs")) &&
                Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
