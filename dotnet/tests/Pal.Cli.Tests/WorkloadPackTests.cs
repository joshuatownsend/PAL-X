using System.Text.RegularExpressions;
using Pal.Engine.Model;
using Pal.Engine.Normalization;
using Pal.Engine.Rules;
using Pal.Engine.Scoring;
using Pal.Ingestion.Csv;
using Pal.Ingestion.HostContext;
using Pal.Packs;
using Xunit;

namespace Pal.Cli.Tests;

/// <summary>
/// One characterization test per ported workload pack: loads ONLY that pack (auto-resolution
/// disabled, like <see cref="GoldenFixtureTests"/>) against its fixture and asserts every rule
/// the pack declares actually fires, and that every finding carries the pack's category. The
/// fixtures are authored so each rule's highest threshold is breached, so a missing rule means
/// an alias/instance/threshold mismatch — a regression, not an expected gap.
/// </summary>
public class WorkloadPackTests
{
    private static string? RepoRoot => FindRepoRoot();

    [Theory]
    [InlineData("print-server", "print")]
    [InlineData("hyper-v", "hyperv")]
    [InlineData("sharepoint-2013", "sharepoint")]
    [InlineData("exchange-2016", "exchange")]
    [InlineData("sql-engine-2014", "sql")]
    [InlineData("citrix-xenapp", "citrix")]
    [InlineData("dynamics-ax", "dynamics")]
    [InlineData("dynamics-crm", "dynamics")]
    [InlineData("skype-for-business", "sfb")]
    [InlineData("classic-asp", "iis")]
    public void WorkloadPack_EveryDeclaredRuleFiresAndCategoryMatches(string packId, string category)
    {
        if (RepoRoot is null) return;

        var (findings, _) = RunAnalysis(packId, [packId]);

        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(category, f.Category));

        var declared = DeclaredRuleIds(packId);
        var fired = findings.Select(f => f.RuleId).ToHashSet();
        var missing = declared.Where(r => !fired.Contains(r)).ToList();
        Assert.True(missing.Count == 0,
            $"{packId}: {missing.Count} declared rule(s) did not fire on the fixture: {string.Join(", ", missing)}");
    }

    private static HashSet<string> DeclaredRuleIds(string packId)
    {
        string yaml = File.ReadAllText(Path.Combine(RepoRoot!, "packs", "thresholds", packId, "pack.yaml"));
        return Regex.Matches(yaml, @"^\s*-\s*rule_id:\s*(?<id>\S+)", RegexOptions.Multiline)
            .Select(m => m.Groups["id"].Value.Trim().Trim('"'))
            .ToHashSet();
    }

    private static (IReadOnlyList<Finding> findings, IReadOnlyList<RuleEngine.EngineWarning> warnings)
        RunAnalysis(string fixtureName, IReadOnlyList<string> packIds)
    {
        string csvPath = Path.Combine(RepoRoot!, "fixtures", fixtureName, "input.csv");

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new CsvCollector(registry);
        var collectResult = collector.Collect(csvPath);
        var hostCtx = HostContextReader.Read(null, null, null);
        var dataset = collectResult.Dataset with { HostContext = hostCtx };

        var resolver = new PackResolver();
        var resolveResult = resolver.Resolve(packIds, [Path.Combine(RepoRoot!, "packs", "thresholds")], false);

        var engine = new RuleEngine();
        var result = engine.Run(resolveResult.Packs, dataset);
        return (result.Findings, result.Warnings);
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
