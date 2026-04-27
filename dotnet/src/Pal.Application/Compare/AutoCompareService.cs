using Pal.Application.Persistence;

namespace Pal.Application.Compare;

public sealed class AutoCompareService : IAutoCompareService
{
    private readonly IAnalysisRepository _analysis;
    private readonly ICompareRepository _compare;
    private readonly CompareRunner _runner;

    public AutoCompareService(IAnalysisRepository analysis, ICompareRepository compare, CompareRunner runner)
    {
        _analysis = analysis;
        _compare = compare;
        _runner = runner;
    }

    public async Task RunAndPersistAsync(Guid baselineJobId, Guid candidateJobId, Guid workspaceId, CancellationToken ct = default)
    {
        var baselineResult = await _analysis.GetResultAsync(baselineJobId, ct);
        var candidateResult = await _analysis.GetResultAsync(candidateJobId, ct);

        if (baselineResult is null || candidateResult is null)
            return;

        var diff = _runner.Run(
            baselineJobId, baselineResult.FindingsJson,
            candidateJobId, candidateResult.FindingsJson);

        await _compare.CreateAsync(baselineJobId, candidateJobId, diff, ct);
    }
}
