using System.Text.Json;
using Pal.Application.Compare;
using Pal.Application.Persistence;

namespace Pal.Api.Endpoints;

public static class CompareEndpoints
{
    private static readonly HashSet<string> ValidBaselineTypes =
        new(StringComparer.OrdinalIgnoreCase) { "machine", "role", "workload", "release" };

    public static void MapCompareEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/analysis/{id:guid}/baseline", async (
            Guid id,
            SetBaselineRequest req,
            IAnalysisRepository analysis) =>
        {
            if (req.IsBaseline && req.Type is not null &&
                !ValidBaselineTypes.Contains(req.Type))
                return Results.BadRequest($"type must be one of: {string.Join(", ", ValidBaselineTypes)}");

            if (req.IsBaseline && req.ContextJson is not null && !IsValidJson(req.ContextJson))
                return Results.BadRequest("contextJson must be valid JSON");

            var job = await analysis.GetJobAsync(id);
            if (job is null) return Results.NotFound();
            if (job.Status != "completed")
                return Results.BadRequest("Only completed jobs can be designated as baselines");

            var normalizedType = req.Type?.ToLowerInvariant();
            var normalizedContextJson = req.ContextJson is not null ? NormalizeJson(req.ContextJson) : null;
            await analysis.SetBaselineAsync(id, req.IsBaseline, req.Label, normalizedType, normalizedContextJson);
            return Results.NoContent();
        })
        .WithName("SetBaseline")
        .WithTags("Compare");

        app.MapGet("/analysis/baselines", async (string? type, IAnalysisRepository analysis) =>
        {
            if (type is not null && !ValidBaselineTypes.Contains(type))
                return Results.BadRequest($"type must be one of: {string.Join(", ", ValidBaselineTypes)}");

            var baselines = await analysis.ListBaselinesAsync(type?.ToLowerInvariant());
            return Results.Ok(new { items = baselines });
        })
        .WithName("ListBaselines")
        .WithTags("Compare");

        app.MapGet("/analysis/baselines/versions", async (
            string type,
            string contextJson,
            IAnalysisRepository analysis) =>
        {
            if (!ValidBaselineTypes.Contains(type))
                return Results.BadRequest($"type must be one of: {string.Join(", ", ValidBaselineTypes)}");

            if (!IsValidJson(contextJson))
                return Results.BadRequest("contextJson must be valid JSON");

            var versions = await analysis.GetBaselineVersionsAsync(type.ToLowerInvariant(), NormalizeJson(contextJson));
            return Results.Ok(new { items = versions });
        })
        .WithName("ListBaselineVersions")
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

        app.MapGet("/compare/list", async (ICompareRepository compare) =>
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

    private static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement);
    }

    private static bool IsValidJson(string json)
    {
        try { NormalizeJson(json); return true; }
        catch { return false; }
    }

    private sealed record SetBaselineRequest(bool IsBaseline, string? Label, string? Type = null, string? ContextJson = null);
    private sealed record CreateCompareRequest(Guid BaselineJobId, Guid CandidateJobId);
}
