namespace Pal.Application.Persistence;

public interface ICompareRepository
{
    Task<CompareResultDto> CreateAsync(Guid baselineJobId, Guid candidateJobId, CompareResultDto result, CancellationToken ct = default);
    Task<CompareResultDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CompareResultDto>> ListAsync(CancellationToken ct = default);
}
