using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Application.Persistence;
using Pal.Application.Storage;
using Pal.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class RetentionRepositoryTests(PalApiFactory factory)
{
    private IRetentionRepository Repo =>
        factory.Services.GetRequiredService<IRetentionRepository>();

    private IDbContextFactory<PalDbContext> DbFactory =>
        factory.Services.GetRequiredService<IDbContextFactory<PalDbContext>>();

    // -------------------------------------------------------------------------
    // PurgeJobsAsync guard: days <= 0
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurgeJobs_ZeroDays_ReturnsEmpty_TouchesNothing()
    {
        var result = await Repo.PurgeJobsAsync(0);

        Assert.Equal(RetentionRunResult.Empty, result);
    }

    // -------------------------------------------------------------------------
    // Core deletion behavior
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurgeJobs_DeletesEligibleJobs_LeavesBaselinesAndRecentJobsIntact()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        var upload = MakeUpload();
        db.Uploads.Add(upload);
        await db.SaveChangesAsync();

        // Old completed job — should be purged
        var oldJob = MakeJob(upload.Id, "completed", completedDaysAgo: 10);
        // Recent completed job — should survive (only 1 day retention)
        var recentJob = MakeJob(upload.Id, "completed", completedDaysAgo: 0);
        // Baseline job — should survive regardless of age
        var baselineJob = MakeJob(upload.Id, "completed", completedDaysAgo: 10, isBaseline: true);
        // Queued job — not terminal, should survive
        var queuedJob = MakeJob(upload.Id, "queued", completedDaysAgo: null);

        db.AnalysisJobs.AddRange(oldJob, recentJob, baselineJob, queuedJob);
        await db.SaveChangesAsync();

        var result = await Repo.PurgeJobsAsync(jobRetentionDays: 5);

        Assert.Equal(1, result.JobsDeleted);
        Assert.Contains(oldJob.Id, result.DeletedJobIds);

        await using var verify = await DbFactory.CreateDbContextAsync();
        Assert.Null(await verify.AnalysisJobs.FindAsync(oldJob.Id));
        Assert.NotNull(await verify.AnalysisJobs.FindAsync(recentJob.Id));
        Assert.NotNull(await verify.AnalysisJobs.FindAsync(baselineJob.Id));
        Assert.NotNull(await verify.AnalysisJobs.FindAsync(queuedJob.Id));
    }

    [Fact]
    public async Task PurgeJobs_DeletesCompareResultsForPurgedJobs_BeforeDeletingJobs()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        var upload = MakeUpload();
        db.Uploads.Add(upload);
        await db.SaveChangesAsync();

        var baselineJob = MakeJob(upload.Id, "completed", completedDaysAgo: 10, isBaseline: true);
        var candidateJob = MakeJob(upload.Id, "completed", completedDaysAgo: 10);
        db.AnalysisJobs.AddRange(baselineJob, candidateJob);
        await db.SaveChangesAsync();

        var compare = new CompareResultEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = DefaultTenant.WorkspaceId,
            BaselineJobId = baselineJob.Id,
            CandidateJobId = candidateJob.Id,
            ResultJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        db.CompareResults.Add(compare);
        await db.SaveChangesAsync();

        // candidateJob is eligible (not baseline, old enough). baselineJob is not.
        var result = await Repo.PurgeJobsAsync(jobRetentionDays: 5);

        Assert.Equal(1, result.JobsDeleted);
        Assert.Equal(1, result.CompareResultsDeleted);

        await using var verify = await DbFactory.CreateDbContextAsync();
        Assert.Null(await verify.CompareResults.FindAsync(compare.Id));
        Assert.Null(await verify.AnalysisJobs.FindAsync(candidateJob.Id));
        Assert.NotNull(await verify.AnalysisJobs.FindAsync(baselineJob.Id));
    }

    [Fact]
    public async Task PurgeJobs_DeletesOrphanedUploads_PreservesSharedUploads()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        // uploadA has only old jobs — should be deleted
        var uploadA = MakeUpload();
        // uploadB is shared with a recent job — should survive
        var uploadB = MakeUpload();
        db.Uploads.AddRange(uploadA, uploadB);
        await db.SaveChangesAsync();

        var oldJobA = MakeJob(uploadA.Id, "completed", completedDaysAgo: 10);
        var oldJobB = MakeJob(uploadB.Id, "completed", completedDaysAgo: 10);
        var recentJobB = MakeJob(uploadB.Id, "completed", completedDaysAgo: 0);
        db.AnalysisJobs.AddRange(oldJobA, oldJobB, recentJobB);
        await db.SaveChangesAsync();

        await Repo.PurgeJobsAsync(jobRetentionDays: 5);

        await using var verify = await DbFactory.CreateDbContextAsync();
        Assert.Null(await verify.Uploads.FindAsync(uploadA.Id));
        Assert.NotNull(await verify.Uploads.FindAsync(uploadB.Id));
    }

    [Fact]
    public async Task PurgeJobs_WritesAuditEvent()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        var upload = MakeUpload();
        db.Uploads.Add(upload);
        await db.SaveChangesAsync();

        var oldJob = MakeJob(upload.Id, "completed", completedDaysAgo: 10);
        db.AnalysisJobs.Add(oldJob);
        await db.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        await Repo.PurgeJobsAsync(jobRetentionDays: 5);

        await using var verify = await DbFactory.CreateDbContextAsync();
        var auditEvent = await verify.AuditEvents
            .Where(e => e.EventType == "retention.jobs_purged" && e.CreatedAt >= before)
            .FirstOrDefaultAsync();

        Assert.NotNull(auditEvent);
        Assert.Equal("system", auditEvent.EntityId);
    }

    // -------------------------------------------------------------------------
    // PurgeAuditEventsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurgeAuditEvents_ZeroDays_TouchesNothing()
    {
        var deleted = await Repo.PurgeAuditEventsAsync(0);
        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task PurgeAuditEvents_DeletesOldEvents_LeavesRecentOnes()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        var old = new AuditEventEntity
        {
            Id = Guid.NewGuid(), EventType = "test.old", EntityId = "x",
            EventJson = "{}", CreatedAt = DateTimeOffset.UtcNow.AddDays(-100)
        };
        var recent = new AuditEventEntity
        {
            Id = Guid.NewGuid(), EventType = "test.recent", EntityId = "x",
            EventJson = "{}", CreatedAt = DateTimeOffset.UtcNow
        };
        db.AuditEvents.AddRange(old, recent);
        await db.SaveChangesAsync();

        var deleted = await Repo.PurgeAuditEventsAsync(auditRetentionDays: 30);

        Assert.True(deleted >= 1);

        await using var verify = await DbFactory.CreateDbContextAsync();
        Assert.Null(await verify.AuditEvents.FindAsync(old.Id));
        Assert.NotNull(await verify.AuditEvents.FindAsync(recent.Id));
    }

    // -------------------------------------------------------------------------
    // Dataset artifact retention
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurgeJobs_WithDatasetArtifact_ClearsResultRowOnCascade()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        var upload = MakeUpload();
        var job = MakeJob(upload.Id, "completed", completedDaysAgo: 10);
        db.Uploads.Add(upload);
        db.AnalysisJobs.Add(job);
        await db.SaveChangesAsync();

        var storage = factory.Services.GetRequiredService<IStorageProvider>();
        var dsPath = await storage.WriteDatasetAsync(job.Id, async (stream, ct) =>
        {
            await using var gz = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
            await gz.WriteAsync("{\"series\":[]}"u8.ToArray(), ct);
            await gz.FlushAsync(ct);
        }, default);

        db.AnalysisResults.Add(new AnalysisResultEntity
        {
            AnalysisJobId = job.Id,
            SummaryJson = "{}",
            FindingsJson = "[]",
            GeneratedAt = DateTimeOffset.UtcNow,
            DatasetStoragePath = dsPath,
            DatasetByteLength = 20,
            DatasetCompressed = true
        });
        await db.SaveChangesAsync();

        await Repo.PurgeJobsAsync(jobRetentionDays: 5);

        await using var verify = await DbFactory.CreateDbContextAsync();
        Assert.Null(await verify.AnalysisJobs.FindAsync(job.Id));
        Assert.Null(await verify.AnalysisResults.FindAsync(job.Id));
    }

    [Fact]
    public async Task DeleteJobDatasetDirectory_RemovesGzFile()
    {
        var storage = factory.Services.GetRequiredService<IStorageProvider>();
        var jobId = Guid.NewGuid();

        var dsPath = await storage.WriteDatasetAsync(jobId, async (stream, ct) =>
        {
            await using var gz = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
            await gz.WriteAsync("{\"series\":[]}"u8.ToArray(), ct);
            await gz.FlushAsync(ct);
        }, default);

        var absPath = storage.GetAbsolutePath(dsPath);
        Assert.True(File.Exists(absPath), "Dataset gz file should exist before deletion");

        storage.DeleteJobDatasetDirectory(jobId);
        Assert.False(File.Exists(absPath), "Dataset gz file should be removed after DeleteJobDatasetDirectory");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static UploadEntity MakeUpload() => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = DefaultTenant.WorkspaceId,
        FileName = $"test-{Guid.NewGuid():N}.csv",
        SourceType = "csv",
        SizeBytes = 100,
        Sha256 = Guid.NewGuid().ToString("N"),
        StoragePath = $"uploads/test/{Guid.NewGuid():N}.csv",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
    };

    private static AnalysisJobEntity MakeJob(
        Guid uploadId, string status, int? completedDaysAgo, bool isBaseline = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new AnalysisJobEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = DefaultTenant.WorkspaceId,
            UploadId = uploadId,
            Status = status,
            CreatedAt = now.AddDays(-(completedDaysAgo ?? 0) - 1),
            CompletedAt = completedDaysAgo.HasValue ? now.AddDays(-completedDaysAgo.Value) : null,
            IsBaseline = isBaseline
        };
    }
}
