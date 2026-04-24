using Pal.Application.Persistence;
using Pal.Packs;

namespace Pal.Api.Services;

public sealed class PackRegistrySyncService
{
    private readonly IPackRepository _packs;
    private readonly ILogger<PackRegistrySyncService> _logger;

    public PackRegistrySyncService(IPackRepository packs, ILogger<PackRegistrySyncService> logger)
    {
        _packs = packs;
        _logger = logger;
    }

    public async Task SyncAsync(string packsDirectory, CancellationToken ct = default)
    {
        if (!Directory.Exists(packsDirectory))
        {
            _logger.LogWarning("Packs directory not found: {Dir}", packsDirectory);
            return;
        }

        var loader = new PackLoader();
        var yamlFiles = Directory
            .EnumerateFiles(packsDirectory, "pack.yaml", SearchOption.AllDirectories)
            .ToList();

        _logger.LogInformation("Syncing {Count} pack(s) from {Dir}", yamlFiles.Count, packsDirectory);

        foreach (var yamlPath in yamlFiles)
        {
            try
            {
                var pack = loader.Load(yamlPath);
                await _packs.UpsertPackAsync(pack.PackId, pack.Version, pack.PackName, yamlPath, ct);
                _logger.LogDebug("Synced pack {PackId} v{Version}", pack.PackId, pack.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync pack from {YamlPath}", yamlPath);
            }
        }
    }
}
