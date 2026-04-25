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

        // Load results concurrently; reverse so entries are oldest→newest for the analyzer
        var orderedOldestFirst = Enumerable.Reverse(recent).ToList();
        var loaded = await Task.WhenAll(orderedOldestFirst.Select(async job => new
        {
            Job = job,
            Result = await analysis.GetResultAsync(job.Id, ct)
        }));

        var entries = loaded
            .Where(x => x.Result is not null)
            .Select(x => new TrendJobEntryDto
            {
                JobId = x.Job.Id,
                CompletedAt = x.Job.CompletedAt ?? x.Job.CreatedAt,
                FindingsJson = x.Result!.FindingsJson
            })
            .ToList();

        return analyzer.Analyze(entries);
    }
}
