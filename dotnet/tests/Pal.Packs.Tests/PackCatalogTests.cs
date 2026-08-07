using Pal.Packs;
using Xunit;

namespace Pal.Packs.Tests;

/// <summary>
/// Covers <see cref="PackResolver.ListAvailable"/> — the "what packs exist" query.
/// Regression guard for the bug where <c>pal list-packs</c> called
/// <see cref="PackResolver.Resolve"/> with <c>autoResolve: false</c> and therefore only
/// ever reported <c>windows-core</c>, ignoring every other pack on the search path.
/// </summary>
public class PackCatalogTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"pal-catalog-{Guid.NewGuid():N}");

    public PackCatalogTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // Assertions are supersets: BuildSearchPaths always appends exe-dir and CWD probe paths,
    // so packs beyond the temp fixtures may legitimately appear.
    private PackCatalogEntry Entry(PackResolver.CatalogResult result, string packId) =>
        Assert.Single(result.Packs, p => p.PackId == packId);

    [Fact]
    public void ListAvailable_ReturnsEveryPackOnTheSearchPath()
    {
        WritePack("alpha-pack", ruleCount: 1);
        WritePack("beta-pack", ruleCount: 3);
        WritePack("gamma-pack", ruleCount: 2);

        var result = new PackResolver().ListAvailable([_tempDir]);

        Assert.Empty(result.Errors);
        foreach (var id in new[] { "alpha-pack", "beta-pack", "gamma-pack" })
            Assert.Contains(result.Packs, p => p.PackId == id);
    }

    [Fact]
    public void ListAvailable_DoesNotStopAtWindowsCore()
    {
        // The exact shape of the original bug: windows-core present alongside others.
        WritePack("windows-core", ruleCount: 2, always: true);
        WritePack("iis-core", ruleCount: 1, requiresAny: ["iis.current_worker_processes"]);
        WritePack("exchange-2016", ruleCount: 4, requiresAny: ["exchange.rpc_averaged_latency"]);

        var result = new PackResolver().ListAvailable([_tempDir]);

        Assert.Contains(result.Packs, p => p.PackId == "iis-core");
        Assert.Contains(result.Packs, p => p.PackId == "exchange-2016");
    }

    [Fact]
    public void ListAvailable_ReportsMetadataFromTheLoadedPack()
    {
        WritePack("beta-pack", ruleCount: 3, requiresAny: ["sql.buffer_cache_hit_ratio"]);

        var entry = Entry(new PackResolver().ListAvailable([_tempDir]), "beta-pack");

        Assert.Equal("3.1.4", entry.Version);
        Assert.Equal(3, entry.RuleCount);
        Assert.False(entry.AlwaysApplicable);
        Assert.Equal("any of: sql.buffer_cache_hit_ratio", entry.Applicability);
        Assert.True(Path.IsPathRooted(entry.Path));
    }

    [Fact]
    public void ListAvailable_AlwaysApplicablePackIsDescribedAsAlways()
    {
        WritePack("windows-core", ruleCount: 1, always: true);

        var entry = Entry(new PackResolver().ListAvailable([_tempDir]), "windows-core");

        Assert.True(entry.AlwaysApplicable);
        Assert.Equal("always", entry.Applicability);
    }

    [Fact]
    public void ListAvailable_PrefersPackIdFromYamlOverDirectoryName()
    {
        // Directory name and pack_id deliberately disagree; the yaml wins.
        WritePack("directory-name", ruleCount: 1, packId: "declared-id");

        var result = new PackResolver().ListAvailable([_tempDir]);

        Assert.Contains(result.Packs, p => p.PackId == "declared-id");
        Assert.DoesNotContain(result.Packs, p => p.PackId == "directory-name");
    }

    [Fact]
    public void ListAvailable_SortsByPackId()
    {
        WritePack("gamma-pack", ruleCount: 1);
        WritePack("alpha-pack", ruleCount: 1);
        WritePack("beta-pack", ruleCount: 1);

        var ids = new PackResolver().ListAvailable([_tempDir]).Packs.Select(p => p.PackId).ToList();

        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
    }

    [Fact]
    public void ListAvailable_UnparseablePackIsReportedAndOmitted()
    {
        WritePack("good-pack", ruleCount: 1);
        var badDir = Path.Combine(_tempDir, "bad-pack");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "pack.yaml"), ":\n  this is not: [valid pack yaml");

        var result = new PackResolver().ListAvailable([_tempDir]);

        Assert.DoesNotContain(result.Packs, p => p.PackId == "bad-pack");
        Assert.Contains(result.Packs, p => p.PackId == "good-pack");
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ListAvailable_EmptyDirectoryYieldsNoErrors()
    {
        var result = new PackResolver().ListAvailable([_tempDir]);

        Assert.Empty(result.Errors);
    }

    private void WritePack(
        string dirName,
        int ruleCount,
        bool always = false,
        string[]? requiresAny = null,
        string? packId = null)
    {
        var dir = Path.Combine(_tempDir, dirName);
        Directory.CreateDirectory(dir);

        var applicability = always
            ? "applicability:\n  always: true\n"
            : $"applicability:\n  requires_any:\n{string.Join("\n", (requiresAny ?? ["processor.percent_processor_time"]).Select(m => $"    - {m}"))}\n";

        var rules = string.Join("\n", Enumerable.Range(1, ruleCount).Select(i => $"""
              - rule_id: rule-{i}
                severity: warning
                category: cpu
                title: "Rule {i}"
                summary: "Rule {i} summary."
                conditions:
                  - metric: processor.percent_processor_time
                    aggregation: avg
                    operator: gt
                    threshold: 80
                    duration_percent: 20
                recommendation_ids: [rec-1]
            """));

        File.WriteAllText(Path.Combine(dir, "pack.yaml"), $"""
            schema_version: "pal.pack/v1"
            pack_id: {packId ?? dirName}
            pack_name: "Pack {dirName}"
            version: "3.1.4"
            {applicability}
            recommendations:
              rec-1:
                priority: high
                text: "Do something."

            rules:
            {rules}
            """);
    }
}
