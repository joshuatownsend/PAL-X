using System.Threading.Channels;
using Pal.Application.Persistence;
using Pal.Application.Storage;

namespace Pal.Api.Endpoints;

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/analysis", async (
            Guid workspaceId,
            CreateAnalysisRequest req,
            IAnalysisRepository analysis,
            IUploadRepository uploads,
            Channel<Guid> channel) =>
        {
            if (req.Packs is null || req.Packs.Count == 0)
                return Results.BadRequest("At least one pack is required");

            var upload = await uploads.GetAsync(req.UploadId);
            if (upload is null)
                return Results.NotFound($"Upload {req.UploadId} not found");

            var job = await analysis.CreateJobAsync(req.UploadId, req.Packs, req.IncludeDataset);
            channel.Writer.TryWrite(job.Id);

            return Results.Accepted($"/api/workspaces/{workspaceId}/analysis/{job.Id}", new { analysisId = job.Id, status = job.Status });
        })
        .WithName("CreateAnalysis")
        .WithTags("Analysis");

        app.MapGet("/analysis", async (string? status, IAnalysisRepository analysis) =>
        {
            var jobs = await analysis.ListJobsAsync(status);
            return Results.Ok(new { items = jobs });
        })
        .WithName("ListAnalysis")
        .WithTags("Analysis");

        app.MapGet("/analysis/{id:guid}", async (Guid id, IAnalysisRepository analysis) =>
        {
            var job = await analysis.GetJobAsync(id);
            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("GetAnalysis")
        .WithTags("Analysis");

        app.MapGet("/analysis/{id:guid}/results", async (Guid id, IAnalysisRepository analysis) =>
        {
            var job = await analysis.GetJobAsync(id);
            if (job is null) return Results.NotFound();
            if (job.Status != "completed") return Results.Problem($"Job is {job.Status}", statusCode: 409);

            var result = await analysis.GetResultAsync(id);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetResults")
        .WithTags("Analysis");

        app.MapGet("/analysis/{id:guid}/report", async (Guid id, string? format, IAnalysisRepository analysis, IStorageProvider storage) =>
        {
            format ??= "html";
            if (format != "html" && format != "json" && format != "markdown")
                return Results.BadRequest("format must be 'html', 'json', or 'markdown'");

            var job = await analysis.GetJobAsync(id);
            if (job is null) return Results.NotFound();
            if (job.Status != "completed") return Results.Problem($"Job is {job.Status}", statusCode: 409);

            var reports = await analysis.GetReportsAsync(id);
            var report = reports.FirstOrDefault(r => r.Format == format);
            if (report is null) return Results.NotFound($"No {format} report for job {id}");

            string contentType = format switch
            {
                "html"     => "text/html; charset=utf-8",
                "markdown" => "text/markdown; charset=utf-8",
                _          => "application/json; charset=utf-8"
            };
            string ext = format == "markdown" ? "md" : format;
            var stream = storage.OpenReport(report.StoragePath);
            return Results.Stream(stream, contentType,
                fileDownloadName: $"pal-report-{id:N}.{ext}");
        })
        .WithName("GetReport")
        .WithTags("Analysis");

        app.MapGet("/analysis/{id:guid}/dataset", async (Guid id, IAnalysisRepository analysis, IStorageProvider storage) =>
        {
            var job = await analysis.GetJobAsync(id);
            if (job is null) return Results.NotFound();
            if (job.Status != "completed") return Results.Problem($"Job is {job.Status}", statusCode: 409);

            var artifact = await analysis.GetDatasetArtifactAsync(id);
            if (artifact is null)
                return Results.NotFound("No dataset artifact for this job (submit with includeDataset: true to generate one)");

            var stream = storage.OpenDataset(artifact.StoragePath);
            string contentType = artifact.Compressed ? "application/gzip" : "application/json; charset=utf-8";
            string fileName = artifact.Compressed ? $"pal-dataset-{id:N}.json.gz" : $"pal-dataset-{id:N}.json";
            return Results.Stream(stream, contentType, fileDownloadName: fileName);
        })
        .WithName("GetDataset")
        .WithTags("Analysis");
    }

    private sealed record CreateAnalysisRequest(Guid UploadId, IReadOnlyList<string> Packs, bool IncludeDataset = false);
}
