using System.IO.Compression;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Application.Storage;
using Pal.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class DatasetEndpointTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly string Analysis = $"{PalApiFactory.WsBase}/analysis";

    private IDbContextFactory<PalDbContext> DbFactory =>
        factory.Services.GetRequiredService<IDbContextFactory<PalDbContext>>();

    private IStorageProvider Storage =>
        factory.Services.GetRequiredService<IStorageProvider>();

    [Fact]
    public async Task Get_Dataset_UnknownJob_Returns404()
    {
        var resp = await _client.GetAsync($"{Analysis}/{Guid.NewGuid()}/dataset", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Dataset_IncompleteJob_Returns409()
    {
        await using var db = await DbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var upload = MakeUpload();
        var job = MakeJob(upload.Id, "queued");
        db.Uploads.Add(upload);
        db.AnalysisJobs.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resp = await _client.GetAsync($"{Analysis}/{job.Id}/dataset", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Dataset_CompletedJob_NoArtifact_Returns404()
    {
        await using var db = await DbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var upload = MakeUpload();
        var job = MakeJob(upload.Id, "completed");
        db.Uploads.Add(upload);
        db.AnalysisJobs.Add(job);
        db.AnalysisResults.Add(new AnalysisResultEntity
        {
            AnalysisJobId = job.Id,
            SummaryJson = "{\"status\":\"healthy\"}",
            FindingsJson = "[]",
            GeneratedAt = DateTimeOffset.UtcNow
            // DatasetStoragePath intentionally not set
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resp = await _client.GetAsync($"{Analysis}/{job.Id}/dataset", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Dataset_WithArtifact_Returns200_GzipContent()
    {
        await using var db = await DbFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);

        var upload = MakeUpload();
        var job = MakeJob(upload.Id, "completed");
        db.Uploads.Add(upload);
        db.AnalysisJobs.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dsPath = await Storage.WriteDatasetAsync(job.Id, async (stream, ct) =>
        {
            await using var gz = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
            await gz.WriteAsync("{\"series\":[]}"u8.ToArray(), ct);
            await gz.FlushAsync(ct);
        }, TestContext.Current.CancellationToken);

        db.AnalysisResults.Add(new AnalysisResultEntity
        {
            AnalysisJobId = job.Id,
            SummaryJson = "{\"status\":\"healthy\"}",
            FindingsJson = "[]",
            GeneratedAt = DateTimeOffset.UtcNow,
            DatasetStoragePath = dsPath,
            DatasetByteLength = 50,
            DatasetCompressed = true
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var resp = await _client.GetAsync($"{Analysis}/{job.Id}/dataset", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/gzip", resp.Content.Headers.ContentType?.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(bytes);
    }

    private static UploadEntity MakeUpload() => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = DefaultTenant.WorkspaceId,
        FileName = $"test-{Guid.NewGuid():N}.csv",
        SourceType = "csv",
        SizeBytes = 100,
        Sha256 = Guid.NewGuid().ToString("N"),
        StoragePath = $"uploads/test/{Guid.NewGuid():N}.csv",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static AnalysisJobEntity MakeJob(Guid uploadId, string status) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = DefaultTenant.WorkspaceId,
        UploadId = uploadId,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        CompletedAt = status == "completed" ? DateTimeOffset.UtcNow : null
    };
}
