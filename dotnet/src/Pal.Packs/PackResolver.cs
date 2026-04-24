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
                if (pack is not null) selected.Add((pack, "explicit"));
            }
        }
        else
        {
            // Default: always load windows-core if present
            if (availablePacks.TryGetValue("windows-core", out var wcPath))
            {
                var pack = LoadAndValidate(wcPath, errors);
                if (pack is not null) selected.Add((pack, "auto"));
            }

            if (autoResolve && presentMetrics is not null)
            {
                foreach (var (id, path) in availablePacks)
                {
                    if (id == "windows-core") continue;
                    if (selected.Any(s => s.pack.PackId == id)) continue;

                    var pack = LoadAndValidate(path, errors);
                    if (pack is null) continue;

                    if (IsApplicable(pack, presentMetrics))
                        selected.Add((pack, "auto"));
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
