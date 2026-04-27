namespace Pal.Application.Compare;

public interface IAutoCompareService
{
    Task RunAndPersistAsync(Guid baselineJobId, Guid candidateJobId, Guid workspaceId, CancellationToken ct = default);
}
