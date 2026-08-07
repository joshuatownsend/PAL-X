using Pal.Packs;
using Xunit;

namespace Pal.Cli.Tests;

/// <summary>
/// End-to-end guard for <c>pal list-packs</c> against the real <c>packs/thresholds</c> tree:
/// every shipped pack directory must appear in the listing. The command previously asked
/// <see cref="PackResolver.Resolve"/> (a dataset-applicability query) instead of
/// <see cref="PackResolver.ListAvailable"/>, so it reported only <c>windows-core</c>.
/// The expected set is enumerated from disk rather than hardcoded, so adding a pack cannot
/// stale this test.
/// </summary>
public class ListPacksTests
{
    private static string? RepoRoot => FindRepoRoot();

    [Fact]
    public void ListAvailable_ListsEveryShippedPack()
    {
        if (RepoRoot is null) return;

        var thresholdsDir = Path.Combine(RepoRoot, "packs", "thresholds");
        var onDisk = Directory.EnumerateDirectories(thresholdsDir)
            .Where(d => File.Exists(Path.Combine(d, "pack.yaml")))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(onDisk.Count > 1, "fixture precondition: more than one pack ships");

        var result = new PackResolver().ListAvailable([thresholdsDir]);

        Assert.Empty(result.Errors);
        // Superset: BuildSearchPaths also probes the exe dir and CWD.
        Assert.Empty(onDisk.Except(result.Packs.Select(p => p.PackId)));
    }

    [Fact]
    public void ListAvailable_ShippedPacksCarryRuleCountsAndApplicability()
    {
        if (RepoRoot is null) return;

        var thresholdsDir = Path.Combine(RepoRoot, "packs", "thresholds");
        var result = new PackResolver().ListAvailable([thresholdsDir]);

        Assert.All(result.Packs, p =>
        {
            Assert.True(p.RuleCount > 0, $"{p.PackId} reported {p.RuleCount} rules");
            Assert.False(string.IsNullOrWhiteSpace(p.Applicability));
        });

        // windows-core is the one pack that loads unconditionally.
        var windowsCore = Assert.Single(result.Packs, p => p.PackId == "windows-core");
        Assert.True(windowsCore.AlwaysApplicable);

        // Every other shipped pack is counter-gated, not always-on.
        Assert.All(result.Packs.Where(p => p.PackId != "windows-core"),
            p => Assert.False(p.AlwaysApplicable, $"{p.PackId} unexpectedly declares always: true"));
    }

    [Theory]
    // Short lists pass through untouched.
    [InlineData("always", "always")]
    [InlineData("never (no applicability block)", "never (no applicability block)")]
    [InlineData("any of: a.one", "any of: a.one")]
    [InlineData("any of: a.one, b.two", "any of: a.one, b.two")]
    // Longer ones are capped so a 60-counter pack cannot wrap the table off-screen.
    [InlineData("any of: a.one, b.two, c.three", "any of: a.one, b.two (+1 more)")]
    [InlineData("all of: a.one, b.two, c.three, d.four", "all of: a.one, b.two (+2 more)")]
    public void Abbreviate_CapsTheMetricListForDisplay(string applicability, string expected) =>
        Assert.Equal(expected, Commands.ListPacksCommand.Abbreviate(applicability));

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
