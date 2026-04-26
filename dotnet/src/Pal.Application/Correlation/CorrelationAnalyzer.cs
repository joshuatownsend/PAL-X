using Pal.Application.Persistence;

namespace Pal.Application.Correlation;

public sealed class CorrelationAnalyzer
{
    private const int DefaultMaxPairs = 20;

    public CorrelationResultDto Analyze(TrendResultDto trendResult, int maxPairs = DefaultMaxPairs)
    {
        var empty = new CorrelationResultDto
        {
            JobCount = trendResult.JobCount,
            WindowStart = trendResult.WindowStart,
            WindowEnd = trendResult.WindowEnd,
            Pairs = []
        };

        if (trendResult.JobCount == 0) return empty;

        // Only keys present in ≥2 runs are meaningful correlation candidates
        var eligible = trendResult.Trends.Where(f => f.RunCount >= 2).ToList();
        if (eligible.Count < 2) return empty;

        var pairs = new List<CorrelationPairDto>();

        for (int i = 0; i < eligible.Count; i++)
        {
            for (int j = i + 1; j < eligible.Count; j++)
            {
                var a = eligible[i];
                var b = eligible[j];

                int coRunCount = CountCoPresent(a.RunPoints, b.RunPoints);
                if (coRunCount < 2) continue;

                pairs.Add(new CorrelationPairDto
                {
                    KeyA = a.CorrelationKey,
                    KeyB = b.CorrelationKey,
                    DirectionA = a.Direction,
                    DirectionB = b.Direction,
                    CoRunCount = coRunCount,
                    TotalRuns = trendResult.JobCount,
                    CoScore = (double)coRunCount / trendResult.JobCount
                });
            }
        }

        pairs.Sort(ComparePairs);

        return new CorrelationResultDto
        {
            JobCount = trendResult.JobCount,
            WindowStart = trendResult.WindowStart,
            WindowEnd = trendResult.WindowEnd,
            Pairs = pairs.Take(maxPairs).ToList()
        };
    }

    // TrendAnalyzer builds all RunPoints lists in the same job iteration order, so index i
    // refers to the same job across all keys — no dictionary needed.
    private static int CountCoPresent(IReadOnlyList<TrendRunPointDto> aPoints, IReadOnlyList<TrendRunPointDto> bPoints)
    {
        int count = 0;
        for (int i = 0; i < aPoints.Count; i++)
            if (aPoints[i].Severity is not null && bPoints[i].Severity is not null) count++;
        return count;
    }

    private static int ComparePairs(CorrelationPairDto x, CorrelationPairDto y)
    {
        // Both-worsening pairs surface first — most actionable
        bool xBothWorse = x.DirectionA == "worsening" && x.DirectionB == "worsening";
        bool yBothWorse = y.DirectionA == "worsening" && y.DirectionB == "worsening";
        if (xBothWorse != yBothWorse) return xBothWorse ? -1 : 1;

        // Direction-matched before mismatched — "moving together" is more signal
        bool xDirMatch = string.Equals(x.DirectionA, x.DirectionB, StringComparison.OrdinalIgnoreCase);
        bool yDirMatch = string.Equals(y.DirectionA, y.DirectionB, StringComparison.OrdinalIgnoreCase);
        if (xDirMatch != yDirMatch) return xDirMatch ? -1 : 1;

        // Higher co-occurrence score first
        int c = y.CoScore.CompareTo(x.CoScore);
        if (c != 0) return c;

        // Alphabetical for deterministic output
        c = string.Compare(x.KeyA, y.KeyA, StringComparison.Ordinal);
        return c != 0 ? c : string.Compare(x.KeyB, y.KeyB, StringComparison.Ordinal);
    }
}
