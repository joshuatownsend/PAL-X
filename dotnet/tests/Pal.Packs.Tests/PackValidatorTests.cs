using Pal.Engine.Model;
using Pal.Packs;
using Xunit;

namespace Pal.Packs.Tests;

public class PackValidatorTests
{
    private static Pack MinimalValidPack(string packId = "test-pack") => new()
    {
        PackId = packId,
        PackName = "Test Pack",
        Version = "1.0.0",
        RecommendationDefs = new Dictionary<string, RecommendationDef>
        {
            ["rec-1"] = new() { Priority = "high", Text = "Do something." }
        },
        Rules = [new Rule
        {
            RuleId = "test-rule",
            Severity = "warning",
            Category = "cpu",
            Title = "Test Rule",
            Summary = "A test rule.",
            Conditions = [new Condition
            {
                Metric = "processor.percent_processor_time",
                Aggregation = "avg",
                Operator = "gt",
                Threshold = new LiteralThreshold(80.0),
                DurationPercent = 20.0
            }],
            RecommendationIds = ["rec-1"]
        }]
    };

    private readonly PackValidator _validator = new();

    [Fact]
    public void Validate_ValidPack_ReturnsValid()
    {
        var result = _validator.Validate(MinimalValidPack());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_InvalidPackId_ReturnsError()
    {
        var pack = MinimalValidPack("Invalid Pack ID!");
        var result = _validator.Validate(pack);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("pack_id"));
    }

    [Fact]
    public void Validate_UnknownSeverity_ReturnsError()
    {
        var pack = MinimalValidPack();
        var rule = pack.Rules[0];
        var badRule = new Rule
        {
            RuleId = rule.RuleId, Severity = "extreme",
            Category = rule.Category, Title = rule.Title, Summary = rule.Summary,
            Conditions = rule.Conditions, RecommendationIds = []
        };
        var badPack = pack with { Rules = [badRule] };
        var result = _validator.Validate(badPack);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("severity"));
    }

    [Fact]
    public void Validate_UnresolvedRecommendationRef_ReturnsError()
    {
        var pack = MinimalValidPack();
        var rule = pack.Rules[0];
        var badRule = new Rule
        {
            RuleId = rule.RuleId, Severity = rule.Severity,
            Category = rule.Category, Title = rule.Title, Summary = rule.Summary,
            Conditions = rule.Conditions, RecommendationIds = ["nonexistent-rec"]
        };
        var badPack = pack with { Rules = [badRule] };
        var result = _validator.Validate(badPack);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("nonexistent-rec"));
    }

    [Fact]
    public void Validate_DuplicateRuleIds_ReturnsError()
    {
        var pack = MinimalValidPack();
        var rule = pack.Rules[0];
        var badPack = pack with { Rules = [rule, rule] };
        var result = _validator.Validate(badPack);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate rule_id"));
    }

    [Fact]
    public void Validate_EmptyRules_ReturnsWarning()
    {
        var pack = MinimalValidPack() with { Rules = [] };
        var result = _validator.Validate(pack);
        Assert.True(result.IsValid); // empty rules is a warning, not an error
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void LoadAndValidate_WindowsCorePack_IsValid()
    {
        var packPath = FindPackPath("windows-core");
        if (packPath is null) return;

        var loader = new PackLoader();
        var pack = loader.Load(packPath);
        var result = _validator.Validate(pack);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.True(pack.Rules.Count >= 10, $"Expected >= 10 rules, got {pack.Rules.Count}");
    }

    [Fact]
    public void LoadAndValidate_IisCorePack_IsValid()
    {
        var packPath = FindPackPath("iis-core");
        if (packPath is null) return;

        var pack = new PackLoader().Load(packPath);
        var result = _validator.Validate(pack);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void LoadAndValidate_SqlHostCorePack_IsValid()
    {
        var packPath = FindPackPath("sql-host-core");
        if (packPath is null) return;

        var pack = new PackLoader().Load(packPath);
        var result = _validator.Validate(pack);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void LoadAndValidate_BrokenPack_IsInvalid()
    {
        var packPath = FindFixturePack("broken-pack");
        if (packPath is null) return;

        var pack = new PackLoader().Load(packPath);
        var result = _validator.Validate(pack);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    private static string? FindPackPath(string packId)
    {
        var root = FindRepoRoot();
        if (root is null) return null;
        var path = Path.Combine(root, "packs", "thresholds", packId, "pack.yaml");
        return File.Exists(path) ? path : null;
    }

    private static string? FindFixturePack(string name)
    {
        var root = FindRepoRoot();
        if (root is null) return null;
        var path = Path.Combine(root, "fixtures", name, "pack.yaml");
        return File.Exists(path) ? path : null;
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Pal.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "packs")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
