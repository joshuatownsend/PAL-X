using Pal.Engine.Model;

namespace Pal.Packs;

public sealed class PackResolver
{
    private readonly PackLoader _loader = new();
    private readonly PackValidator _validator = new();

    public sealed class ResolveResult
    {
        public required IReadOnlyList<Pack> Packs { get; init; }
        public required IReadOnlyList<PackResolutionInfo> Resolutions { get; init; }
        public required IReadOnlyList<string> Errors { get; init; }
    }

    public sealed class CatalogResult
    {
        public required IReadOnlyList<PackCatalogEntry> Packs { get; init; }
        public required IReadOnlyList<string> Errors { get; init; }
    }

    /// <summary>
    /// Lists every pack on the search path, independent of any dataset.
    /// <para>
    /// This answers "what packs exist?" — <see cref="Resolve"/> answers the different question
    /// "which packs apply to this run?" and deliberately returns a subset. Callers doing
    /// discovery (e.g. <c>pal list-packs</c>) must use this method; using <see cref="Resolve"/>
    /// with <c>autoResolve: false</c> yields only <c>windows-core</c>.
    /// </para>
    /// Packs that fail to load or validate are omitted and reported in <see cref="CatalogResult.Errors"/>.
    /// Entries are sorted by pack ID so output is deterministic across filesystems.
    /// </summary>
    public CatalogResult ListAvailable(IReadOnlyList<string> packDirs)
    {
        var errors = new List<string>();
        var entries = new List<PackCatalogEntry>();

        foreach (var (_, path) in DiscoverPacks(BuildSearchPaths(packDirs)))
        {
            var pack = LoadAndValidate(path, errors);
            if (pack is null) continue;

            entries.Add(new PackCatalogEntry
            {
                // pack_id from the yaml is authoritative; DiscoverPacks keys by directory name.
                PackId = pack.PackId,
                PackName = pack.PackName,
                Version = pack.Version,
                SchemaVersion = pack.SchemaVersion,
                Description = pack.Description,
                RuleCount = pack.Rules.Count,
                AlwaysApplicable = pack.Applicability?.Always ?? false,
                Applicability = DescribeApplicability(pack.Applicability),
                Path = Path.GetFullPath(path)
            });
        }

        return new CatalogResult
        {
            Packs = entries.OrderBy(e => e.PackId, StringComparer.Ordinal).ToList(),
            Errors = errors
        };
    }

    private static string DescribeApplicability(PackApplicability? applicability)
    {
        if (applicability is null) return "never (no applicability block)";
        if (applicability.Always) return "always";

        if (applicability.RequiresAll.Count > 0)
            return $"all of: {string.Join(", ", applicability.RequiresAll)}";

        if (applicability.RequiresAny.Count > 0)
            return $"any of: {string.Join(", ", applicability.RequiresAny)}";

        return "never (empty applicability block)";
    }

    public ResolveResult Resolve(
        IReadOnlyList<string> explicitPackIds,
        IReadOnlyList<string> packDirs,
        bool autoResolve,
        IReadOnlyCollection<string>? presentMetrics = null)
    {
        var searchPaths = BuildSearchPaths(packDirs);
        var availablePacks = DiscoverPacks(searchPaths);
        var errors = new List<string>();
        var selected = new List<(Pack pack, string mode)>();
        var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (explicitPackIds.Count > 0)
        {
            foreach (var id in explicitPackIds)
            {
                if (!availablePacks.TryGetValue(id, out var packPath))
                {
                    errors.Add($"Pack '{id}' not found on any search path.");
                    continue;
                }
                var pack = LoadAndValidate(packPath, errors);
                if (pack is not null) { selected.Add((pack, "explicit")); loadedIds.Add(pack.PackId); }
            }
        }
        else
        {
            if (availablePacks.TryGetValue("windows-core", out var wcPath))
            {
                var pack = LoadAndValidate(wcPath, errors);
                if (pack is not null) { selected.Add((pack, "auto")); loadedIds.Add(pack.PackId); }
            }

            if (autoResolve && presentMetrics is not null)
            {
                foreach (var (id, path) in availablePacks)
                {
                    if (loadedIds.Contains(id)) continue;

                    var pack = LoadAndValidate(path, errors);
                    if (pack is null) continue;

                    if (IsApplicable(pack, presentMetrics))
                    {
                        selected.Add((pack, "auto"));
                        loadedIds.Add(pack.PackId);
                    }
                }
            }
        }

        return new ResolveResult
        {
            Packs = selected.Select(s => s.pack).ToList(),
            Resolutions = selected.Select(s => new PackResolutionInfo
            {
                PackId = s.pack.PackId,
                PackName = s.pack.PackName,
                Version = s.pack.Version,
                ResolutionMode = s.mode
            }).ToList(),
            Errors = errors
        };
    }

    private Dictionary<string, string> DiscoverPacks(IEnumerable<string> searchPaths)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir)) continue;

            // Check if this directory is itself a pack
            if (File.Exists(Path.Combine(dir, "pack.yaml")))
            {
                var packId = Path.GetFileName(dir);
                if (!found.ContainsKey(packId))
                    found[packId] = Path.Combine(dir, "pack.yaml");
            }

            // Check subdirectories
            foreach (var subdir in Directory.EnumerateDirectories(dir))
            {
                var packYaml = Path.Combine(subdir, "pack.yaml");
                if (!File.Exists(packYaml)) continue;
                var packId = Path.GetFileName(subdir);
                if (!found.ContainsKey(packId))
                    found[packId] = packYaml;
            }
        }
        return found;
    }

    private Pack? LoadAndValidate(string packYamlPath, List<string> errors)
    {
        Pack pack;
        try { pack = _loader.Load(packYamlPath); }
        catch (Exception ex)
        {
            errors.Add($"Failed to load pack at '{packYamlPath}': {ex.Message}");
            return null;
        }

        var result = _validator.Validate(pack);
        if (!result.IsValid)
        {
            foreach (var e in result.Errors)
                errors.Add($"Pack '{pack.PackId}': {e}");
            return null;
        }
        return pack;
    }

    private static bool IsApplicable(Pack pack, IReadOnlyCollection<string> presentMetrics)
    {
        if (pack.Applicability is null) return false;
        if (pack.Applicability.Always) return true;

        if (pack.Applicability.RequiresAll.Count > 0)
            return pack.Applicability.RequiresAll.All(m =>
                presentMetrics.Contains(m, StringComparer.OrdinalIgnoreCase));

        if (pack.Applicability.RequiresAny.Count > 0)
            return pack.Applicability.RequiresAny.Any(m =>
                presentMetrics.Contains(m, StringComparer.OrdinalIgnoreCase));

        return false;
    }

    private static IEnumerable<string> BuildSearchPaths(IReadOnlyList<string> userDirs)
    {
        // Order: explicit --pack-dir → built-in next to exe → .\packs CWD
        foreach (var d in userDirs) yield return d;

        var exeDir = Path.GetDirectoryName(typeof(PackResolver).Assembly.Location) ?? ".";
        yield return Path.Combine(exeDir, "packs", "thresholds");

        yield return Path.Combine(Directory.GetCurrentDirectory(), "packs", "thresholds");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "packs");
    }
}
