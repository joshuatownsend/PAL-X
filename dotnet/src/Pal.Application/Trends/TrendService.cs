using Pal.Application.Persistence;

namespace Pal.Application.Trends;

public sealed class TrendService(IAnalysisRepository analysis, TrendAnalyzer analyzer)
{
    public async Task<TrendResultDto> ComputeAsync(int window, CancellationToken ct = default)
    {
        int clampedWindow = Math.Clamp(window, 1, 100);
        var jobs = await analysis.ListJobsAsync("completed", ct);

        var recent = jobs
            .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
            .Take(clampedWindow)
            .ToList();

        if (recent.Count == 0)
            return new TrendResultDto { JobCount = 0, WindowStart = default, WindowEnd = default, Trends = [] };

        // Reverse so entries are oldest→newest for the analyzer
        var orderedOldestFirst = Enumerable.Reverse(recent).ToList();
        var resultsList = await analysis.GetResultsAsync(orderedOldestFirst.Select(j => j.Id), ct);
        var resultsByJob = resultsList.ToDictionary(r => r.AnalysisJobId);

        var entries = orderedOldestFirst
            .Where(j => resultsByJob.ContainsKey(j.Id))
            .Select(j => new TrendJobEntryDto
            {
                JobId = j.Id,
                CompletedAt = j.CompletedAt ?? j.CreatedAt,
                FindingsJson = resultsByJob[j.Id].FindingsJson
            })
            .ToList();

        return analyzer.Analyze(entries);
    }
}
