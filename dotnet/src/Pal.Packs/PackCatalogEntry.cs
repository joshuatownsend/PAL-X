namespace Pal.Packs;

/// <summary>
/// A pack found on the search path, described without reference to any dataset.
/// This is the "what exists" view — contrast with <see cref="Pal.Engine.Model.PackResolutionInfo"/>,
/// which describes why a pack was selected for a particular analysis run.
/// </summary>
public sealed class PackCatalogEntry
{
    public required string PackId { get; init; }
    public required string PackName { get; init; }
    public required string Version { get; init; }
    public required string SchemaVersion { get; init; }
    public string? Description { get; init; }
    public required int RuleCount { get; init; }

    /// <summary>Pack loads on every run regardless of the counters present.</summary>
    public required bool AlwaysApplicable { get; init; }

    /// <summary>
    /// Human-readable summary of the pack's applicability block — "always", a metric list,
    /// or "never" for a pack that declares no applicability at all.
    /// </summary>
    public required string Applicability { get; init; }

    /// <summary>Absolute path to the pack.yaml this entry was loaded from.</summary>
    public required string Path { get; init; }
}
