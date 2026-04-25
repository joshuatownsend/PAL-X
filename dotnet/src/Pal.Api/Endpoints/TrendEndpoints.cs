using Pal.Application.Persistence;
using Pal.Application.Trends;

namespace Pal.Api.Endpoints;

public static class TrendEndpoints
{
    public static void MapTrendEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/trends", async (
            int? last,
            IAnalysisRepository analysis,
            TrendAnalyzer analyzer) =>
        {
            int window = Math.Clamp(last ?? 10, 1, 100);

            var jobs = await analysis.ListJobsAsync("completed");
            var recent = jobs.Take(window).ToList();

            if (recent.Count == 0)
                return Results.Ok(new TrendResultDto
                {
                    JobCount = 0,
                    WindowStart = default,
                    WindowEnd = default,
                    Trends = []
                });

            // Results must be loaded per job; order oldest-first for the analyzer
            var entries = new List<TrendJobEntryDto>(recent.Count);
            foreach (var job in Enumerable.Reverse(recent))
            {
                var result = await analysis.GetResultAsync(job.Id);
                if (result is null) continue;
                entries.Add(new TrendJobEntryDto
                {
                    JobId = job.Id,
                    CompletedAt = job.CompletedAt ?? job.CreatedAt,
                    FindingsJson = result.FindingsJson
                });
            }

            return Results.Ok(analyzer.Analyze(entries));
        })
        .WithName("GetTrends")
        .WithTags("Trends");
    }
}
