namespace Pal.Application.Persistence;

public interface IPackRepository
{
    Task<IReadOnlyList<PackSummaryDto>> ListPacksAsync(CancellationToken ct = default);
    Task<PackVersionDto?> GetVersionAsync(string packId, string version, CancellationToken ct = default);
    Task<IReadOnlyList<PackVersionDto>> ListVersionsAsync(string packId, CancellationToken ct = default);
    Task<string?> GetVersionYamlPathAsync(string packId, string version, CancellationToken ct = default);
    Task UpsertPackAsync(string packId, string version, string title, string yamlPath, CancellationToken ct = default);
}
