using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Pal.Engine.Model;

namespace Pal.Packs;

public sealed class PackLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public Pack Load(string yamlPath)
    {
        var text = File.ReadAllText(yamlPath);
        var raw = Deserializer.Deserialize<RawPack>(text)
            ?? throw new InvalidDataException($"Pack file '{yamlPath}' could not be deserialized.");
        return MapPack(raw, yamlPath);
    }

    public Pack LoadFromDirectory(string directory)
    {
        var packFile = Path.Combine(directory, "pack.yaml");
        if (!File.Exists(packFile))
            throw new FileNotFoundException($"No pack.yaml found in '{directory}'.");
        return Load(packFile);
    }

    private static Pack MapPack(RawPack raw, string sourcePath)
    {
        var rules = (raw.Rules ?? []).Select(MapRule).ToList();
        var recDefs = (raw.Recommendations ?? new Dictionary<string, RawRecommendationDef>())
            .ToDictionary(kv => kv.Key, kv => MapRecDef(kv.Value));

        return new Pack
        {
            PackId = raw.PackId ?? throw new InvalidDataException($"Pack at '{sourcePath}' missing pack_id"),
            PackName = raw.PackName ?? throw new InvalidDataException($"Pack at '{sourcePath}' missing pack_name"),
            Version = raw.Version ?? "0.0.0",
            Description = raw.Description,
            Applicability = raw.Applicability is null ? null : MapApplicability(raw.Applicability),
            MetricAliases = (raw.MetricAliases ?? new Dictionary<string, List<string>>())
                .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            RecommendationDefs = recDefs,
            Rules = rules
        };
    }

    private static PackApplicability MapApplicability(RawApplicability raw) => new()
    {
        Always = raw.Always,
        RequiresAny = raw.RequiresAny ?? [],
        RequiresAll = raw.RequiresAll ?? []
    };

    private static Rule MapRule(RawRule raw) => new()
    {
        RuleId = raw.RuleId ?? throw new InvalidDataException("Rule missing rule_id"),
        Severity = raw.Severity ?? throw new InvalidDataException($"Rule '{raw.RuleId}' missing severity"),
        Category = raw.Category ?? throw new InvalidDataException($"Rule '{raw.RuleId}' missing category"),
        Title = raw.Title ?? throw new InvalidDataException($"Rule '{raw.RuleId}' missing title"),
        Summary = raw.Summary ?? throw new InvalidDataException($"Rule '{raw.RuleId}' missing summary"),
        Explanation = raw.Explanation,
        AppliesWhen = raw.AppliesWhen is null ? null : MapAppliesWhen(raw.AppliesWhen),
        Conditions = (raw.Conditions ?? []).Select(MapCondition).ToList(),
        RecommendationIds = raw.Recommendations ?? []
    };

    private static RuleAppliesWhen MapAppliesWhen(RawAppliesWhen raw) => new()
    {
        RequiresAny = raw.RequiresAny ?? [],
        RequiresAll = raw.RequiresAll ?? []
    };

    private static Condition MapCondition(RawCondition raw)
    {
        ThresholdValue threshold;

        // YamlDotNet deserializes `threshold:` as:
        //   - double/int (boxed as object) when it's a literal number
        //   - Dictionary<object,object> when it's a nested host_context object
        if (raw.Threshold is Dictionary<object, object> hctDict)
        {
            string? hcVar = hctDict.TryGetValue("host_context", out var hcv) ? hcv?.ToString() : null;
            double factor = hctDict.TryGetValue("factor", out var fv) ? Convert.ToDouble(fv) : 1.0;
            double? minimum = hctDict.TryGetValue("minimum", out var mn) ? Convert.ToDouble(mn) : null;
            double? maximum = hctDict.TryGetValue("maximum", out var mx) ? Convert.ToDouble(mx) : null;
            threshold = new HostContextThreshold
            {
                HostContextVariable = hcVar
                    ?? throw new InvalidDataException("host_context threshold missing 'host_context' variable"),
                Factor = factor,
                Minimum = minimum,
                Maximum = maximum
            };
        }
        else if (raw.Threshold is not null)
        {
            threshold = new LiteralThreshold(Convert.ToDouble(raw.Threshold, System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            throw new InvalidDataException($"Condition on metric '{raw.Metric}' has no threshold defined.");
        }

        return new Condition
        {
            Metric = raw.Metric ?? throw new InvalidDataException("Condition missing metric"),
            Instance = raw.Instance,
            Aggregation = raw.Aggregation ?? throw new InvalidDataException("Condition missing aggregation"),
            Operator = raw.Operator ?? throw new InvalidDataException("Condition missing operator"),
            Threshold = threshold,
            DurationPercent = raw.DurationPercent ?? 1.0
        };
    }

    private static RecommendationDef MapRecDef(RawRecommendationDef raw) => new()
    {
        Priority = raw.Priority ?? "medium",
        Text = raw.Text ?? string.Empty,
        Rationale = raw.Rationale,
        Links = raw.Links ?? []
    };

    // ── raw deserialization types ──────────────────────────────────────

    private sealed class RawPack
    {
        public string? SchemaVersion { get; set; }
        public string? PackId { get; set; }
        public string? PackName { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        public RawApplicability? Applicability { get; set; }
        public Dictionary<string, List<string>>? MetricAliases { get; set; }
        public Dictionary<string, RawRecommendationDef>? Recommendations { get; set; }
        public List<RawRule>? Rules { get; set; }
    }

    private sealed class RawApplicability
    {
        public bool Always { get; set; }
        public List<string>? RequiresAny { get; set; }
        public List<string>? RequiresAll { get; set; }
    }

    private sealed class RawRule
    {
        public string? RuleId { get; set; }
        public string? Severity { get; set; }
        public string? Category { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? Explanation { get; set; }
        public RawAppliesWhen? AppliesWhen { get; set; }
        public List<RawCondition>? Conditions { get; set; }
        public List<string>? Recommendations { get; set; }
    }

    private sealed class RawAppliesWhen
    {
        public List<string>? RequiresAny { get; set; }
        public List<string>? RequiresAll { get; set; }
    }

    private sealed class RawCondition
    {
        public string? Metric { get; set; }
        public string? Instance { get; set; }
        public string? Aggregation { get; set; }
        public string? Operator { get; set; }
        public object? Threshold { get; set; }
        public double? DurationPercent { get; set; }
    }

    private sealed class RawRecommendationDef
    {
        public string? Priority { get; set; }
        public string? Text { get; set; }
        public string? Rationale { get; set; }
        public List<string>? Links { get; set; }
    }
}
