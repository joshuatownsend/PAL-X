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
        AssertHasFindings(reportJson);
        AssertReportIsSchemaValid(reportJson);
    }

    [Fact]
    public void DotNetClrReport_SatisfiesReportSchema()
    {
        if (RepoRoot is null) return;

        var reportJson = WriteReport("dotnet-clr", ["dotnet-clr"]);
        AssertHasFindings(reportJson, "dotnet");
        AssertReportIsSchemaValid(reportJson);
    }

    [Fact]
    public void ActiveDirectoryReport_SatisfiesReportSchema()
    {
        if (RepoRoot is null) return;

        var reportJson = WriteReport("active-directory", ["active-directory"]);
        AssertHasFindings(reportJson, "ad");
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
            OutputFormat = OutputFormat.List,
            // Draft-07 treats `format` as an annotation by default, so the schema's
            // `format: date-time` constraints (generated_at_utc, dataset times, finding
            // windows) would not be asserted. Force them on so a writer regressing to a
            // non-date string is caught by the contract test.
            RequireFormatValidation = true
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

            // Guard against silent mis-resolution. PackResolver.Resolve can return an empty
            // Packs list (e.g. a missing pack dir) without always populating Errors; a report
            // produced with no packs would still satisfy the (open) schema, making the contract
            // test misleading. Fail loudly instead.
            Assert.True(resolveResult.Errors.Count == 0,
                $"Pack resolution reported errors: {string.Join("; ", resolveResult.Errors)}");
            Assert.True(resolveResult.Packs.Count > 0,
                "Pack resolution produced zero packs — the engine would emit an empty report.");

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
            OutputFormat = OutputFormat.List,
            // Draft-07 treats `format` as an annotation by default, so the schema's
            // `format: date-time` constraints (generated_at_utc, dataset times, finding
            // windows) would not be asserted. Force them on so a writer regressing to a
            // non-date string is caught by the contract test.
            RequireFormatValidation = true
        });

        if (!results.IsValid)
        {
            var errors = CollectErrors(results);
            Assert.Fail($"Report does not satisfy pal.report/v1:\n{errors}");
        }
    }

    /// <summary>
    /// Guards the positive contract tests against becoming vacuous. An open schema is
    /// satisfied by an empty <c>findings</c> array, so a regressed alias/pack/fixture that
    /// stops producing findings would leave the schema test green while guarding nothing.
    /// Asserts the report has at least one finding, and — when <paramref name="requiredCategory"/>
    /// is given — at least one finding in that category (the thing the regression guard protects).
    /// </summary>
    private static void AssertHasFindings(string reportJson, string? requiredCategory = null)
    {
        using var doc = JsonDocument.Parse(reportJson);
        var findings = doc.RootElement.GetProperty("findings");

        Assert.True(findings.GetArrayLength() > 0,
            "Report contains zero findings — the fixture, alias table, or pack resolution has " +
            "regressed and this schema guard is now vacuous.");

        if (requiredCategory is not null)
        {
            Assert.True(
                findings.EnumerateArray().Any(f => f.GetProperty("category").GetString() == requiredCategory),
                $"Report has findings but none with category '{requiredCategory}' — the " +
                $"'{requiredCategory}' workload pack or alias has regressed and this regression guard is now vacuous.");
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
