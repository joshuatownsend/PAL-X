using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Pal.Engine.Model;
using Pal.Engine.Normalization;
using Pal.Engine.Rules;
using Pal.Ingestion.Csv;
using Pal.Ingestion.HostContext;
using Pal.Packs;
using Pal.Reporting.Json;
using Xunit;

namespace Pal.Cli.Tests;

public class ReportSchemaContractTests
{
    private static string? RepoRoot => FindRepoRoot();

    private static string? SchemaPath => RepoRoot is null
        ? null
        : Path.Combine(RepoRoot, "dotnet", "schemas", "pal.report.v1.json");

    private static readonly JsonSchema? ReportSchema = SchemaPath is not null && File.Exists(SchemaPath)
        ? JsonSchema.FromText(File.ReadAllText(SchemaPath))
        : null;

    [Fact]
    public void CpuPressureReport_SatisfiesReportSchema()
    {
        if (RepoRoot is null) return;

        var reportJson = WriteReport("cpu-pressure", []);
        AssertReportIsSchemaValid(reportJson);
    }

    [Fact]
    public void DotNetClrReport_SatisfiesReportSchema()
    {
        if (RepoRoot is null) return;

        var reportJson = WriteReport("dotnet-clr", ["dotnet-clr"]);
        AssertReportIsSchemaValid(reportJson);
    }

    [Fact]
    public void ActiveDirectoryReport_SatisfiesReportSchema()
    {
        if (RepoRoot is null) return;

        var reportJson = WriteReport("active-directory", ["active-directory"]);
        AssertReportIsSchemaValid(reportJson);
    }

    [Fact]
    public void TamperedCategory_FailsReportSchema()
    {
        if (RepoRoot is null) return;

        var reportJson = WriteReport("dotnet-clr", ["dotnet-clr"]);
        var root = JsonNode.Parse(reportJson)!.AsObject();

        var findings = root["findings"]?.AsArray();
        Assert.NotNull(findings);
        Assert.True(findings.Count > 0, "dotnet-clr fixture produced zero findings — fixture or pipeline has drifted");

        findings[0]!.AsObject()["category"] = JsonValue.Create("not-a-real-category");

        var tampered = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        using var doc = JsonDocument.Parse(tampered);
        var results = ReportSchema!.Evaluate(doc.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });
        Assert.False(results.IsValid, "Expected tampered report to fail schema validation but it passed");
    }

    private static string WriteReport(string fixtureName, string[] packIds)
    {
        Assert.NotNull(RepoRoot);
        Assert.NotNull(ReportSchema);

        var inputCsv = Path.Combine(RepoRoot!, "fixtures", fixtureName, "input.csv");
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        var outputJson = Path.Combine(tmpDir, "test.pal-report.json");

        try
        {
            var registry = MetricAliasRegistry.BuildDefault();
            var collector = new CsvCollector(registry);
            var collectResult = collector.Collect(inputCsv);
            var dataset = collectResult.Dataset with { HostContext = HostContext.Unknown };

            var resolver = new PackResolver();
            var resolveResult = resolver.Resolve(packIds, [Path.Combine(RepoRoot!, "packs", "thresholds")], false);
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

            return File.ReadAllText(outputJson);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    private static void AssertReportIsSchemaValid(string reportJson)
    {
        Assert.NotNull(ReportSchema);
        using var doc = JsonDocument.Parse(reportJson);
        var results = ReportSchema!.Evaluate(doc.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        if (!results.IsValid)
        {
            var errors = CollectErrors(results);
            Assert.Fail($"Report does not satisfy pal.report/v1:\n{errors}");
        }
    }

    private static string CollectErrors(EvaluationResults root)
    {
        var lines = new List<string>();
        Traverse(root, lines);
        return string.Join("\n", lines);
    }

    private static void Traverse(EvaluationResults node, List<string> lines)
    {
        if (node.Errors is { Count: > 0 })
        {
            foreach (var (keyword, message) in node.Errors)
                lines.Add($"  at {node.InstanceLocation}: {keyword} -> {message}");
        }
        if (node.Details is { Count: > 0 })
        {
            foreach (var child in node.Details)
                Traverse(child, lines);
        }
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
