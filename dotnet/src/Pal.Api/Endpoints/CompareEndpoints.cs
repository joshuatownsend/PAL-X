using Pal.Application.Compare;
using Pal.Application.Persistence;

namespace Pal.Api.Endpoints;

public static class CompareEndpoints
{
    public static void MapCompareEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/analysis/{id:guid}/baseline", async (
            Guid id,
            SetBaselineRequest req,
            IAnalysisRepository analysis) =>
        {
            var job = await analysis.GetJobAsync(id);
            if (job is null) return Results.NotFound();
            if (job.Status != "completed")
                return Results.BadRequest("Only completed jobs can be designated as baselines");

            await analysis.SetBaselineAsync(id, req.IsBaseline, req.Label);
            return Results.NoContent();
        })
        .WithName("SetBaseline")
        .WithTags("Compare");

        app.MapGet("/analysis/baselines", async (IAnalysisRepository analysis) =>
        {
            var baselines = await analysis.ListBaselinesAsync();
            return Results.Ok(new { items = baselines });
        })
        .WithName("ListBaselines")
        .WithTags("Compare");

        app.MapPost("/compare", async (
            CreateCompareRequest req,
            IAnalysisRepository analysis,
            ICompareRepository compare,
            CompareRunner runner) =>
        {
            var baseline = await analysis.GetJobAsync(req.BaselineJobId);
            if (baseline is null) return Results.NotFound($"Baseline job {req.BaselineJobId} not found");
            if (baseline.Status != "completed")
                return Results.BadRequest($"Baseline job {req.BaselineJobId} is not completed");

            var candidate = await analysis.GetJobAsync(req.CandidateJobId);
            if (candidate is null) return Results.NotFound($"Candidate job {req.CandidateJobId} not found");
            if (candidate.Status != "completed")
                return Results.BadRequest($"Candidate job {req.CandidateJobId} is not completed");

            var baselineResult = await analysis.GetResultAsync(req.BaselineJobId);
            var candidateResult = await analysis.GetResultAsync(req.CandidateJobId);
            if (baselineResult is null || candidateResult is null)
                return Results.Problem("Result data missing for one or both jobs", statusCode: 500);

            var diff = runner.Run(
                req.BaselineJobId, baselineResult.FindingsJson,
                req.CandidateJobId, candidateResult.FindingsJson);

            var saved = await compare.CreateAsync(req.BaselineJobId, req.CandidateJobId, diff);
            return Results.Created($"/compare/{saved.Id}", saved);
        })
        .WithName("CreateCompare")
        .WithTags("Compare");

        app.MapGet("/compare", async (ICompareRepository compare) =>
        {
            var results = await compare.ListAsync();
            return Results.Ok(new { items = results });
        })
        .WithName("ListCompare")
        .WithTags("Compare");

        app.MapGet("/compare/{id:guid}", async (Guid id, ICompareRepository compare) =>
        {
            var result = await compare.GetAsync(id);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetCompare")
        .WithTags("Compare");
    }

    private sealed record SetBaselineRequest(bool IsBaseline, string? Label);
    private sealed record CreateCompareRequest(Guid BaselineJobId, Guid CandidateJobId);
}
