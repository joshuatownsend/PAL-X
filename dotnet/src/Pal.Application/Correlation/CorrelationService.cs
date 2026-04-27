using Pal.Application.Persistence;
using Pal.Application.Trends;

namespace Pal.Application.Correlation;

public sealed class CorrelationService(TrendService trends, CorrelationAnalyzer analyzer)
{
    public async Task<CorrelationResultDto> ComputeAsync(int window, CancellationToken ct = default)
    {
        var trendResult = await trends.ComputeAsync(window, ct);
        return analyzer.Analyze(trendResult);
    }

    public async Task<(TrendResultDto Trends, CorrelationResultDto Correlations)> ComputeBothAsync(int window, CancellationToken ct = default)
    {
        var trendResult = await trends.ComputeAsync(window, ct);
        return (trendResult, analyzer.Analyze(trendResult));
    }
}
