using Pal.Application.Persistence;

namespace Pal.Application.Ingestion;

public interface IIngestionScheduleService
{
    Task<IReadOnlyList<IngestionScheduleDto>> ListAsync(CancellationToken ct = default);
    Task<IngestionScheduleDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IngestionScheduleDto> CreateAsync(
        string name,
        int intervalMinutes,
        string sourceConfigJson,
        IReadOnlyList<string> packIds,
        bool enabled,
        CancellationToken ct = default);

    Task<bool> UpdateAsync(
        Guid id,
        string name,
        int intervalMinutes,
        string sourceConfigJson,
        IReadOnlyList<string> packIds,
        bool enabled,
        CancellationToken ct = default);

    Task<bool> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class IngestionScheduleValidationException : Exception
{
    public IngestionScheduleValidationException(string message) : base(message) { }
}
