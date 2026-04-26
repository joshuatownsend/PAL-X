using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class RetentionRepository : IRetentionRepository
{
    // Bound each run to avoid huge IN-lists when retention is first enabled on an old instance.
    // Subsequent daily runs will catch up.
    private const int JobBatchSize = 1000;

    private readonly IDbContextFactory<PalDbContext> _factory;

    public RetentionRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<RetentionRunResult> PurgeJobsAsync(int jobRetentionDays, CancellationToken ct = default)
    {
        if (jobRetentionDays <= 0)
            return RetentionRunResult.Empty;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-jobRetentionDays);
        await using var db = await _factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1. Snapshot candidate IDs (bounded) for storage cleanup and driving FK-safe deletes.
        //    Take() bounds the IN-list and ensures each daily run makes bounded progress.
        var jobIds = await db.AnalysisJobs
            .Where(j => !j.IsBaseline
                     && (j.Status == "completed" || j.Status == "failed")
                     && j.CompletedAt < cutoff)
            .OrderBy(j => j.CompletedAt)
            .Take(JobBatchSize)
            .Select(j => j.Id)
            .ToListAsync(ct);

        if (jobIds.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return RetentionRunResult.Empty;
        }

        // 2. Capture upload IDs before deletion to find orphans afterward.
        var uploadIds = await db.AnalysisJobs
            .Where(j => jobIds.Contains(j.Id))
            .Select(j => j.UploadId)
            .Distinct()
            .ToListAsync(ct);

        // 3. Delete compare_results first — both FKs are Restrict, which blocks job deletion.
        var compareDeleted = await db.CompareResults
            .Where(c => jobIds.Contains(c.BaselineJobId) || jobIds.Contains(c.CandidateJobId))
            .ExecuteDeleteAsync(ct);

        // 4. Bulk-delete jobs, rechecking !IsBaseline at delete time to guard against a concurrent
        //    baseline promotion that occurred after the snapshot at step 1.
        //    DB-level CASCADE fires for: analysis_job_packs, analysis_reports, analysis_results.
        var jobsDeleted = await db.AnalysisJobs
            .Where(j => jobIds.Contains(j.Id) && !j.IsBaseline)
            .ExecuteDeleteAsync(ct);

        // 5. Find uploads now fully orphaned (no remaining analysis_jobs reference them).
        var orphanedUploads = await db.Uploads
            .Where(u => uploadIds.Contains(u.Id) && !db.AnalysisJobs.Any(j => j.UploadId == u.Id))
            .Select(u => new { u.Id, u.Sha256 })
            .ToListAsync(ct);

        // 6. Delete orphaned uploads, rechecking the join condition at delete time to guard
        //    against a concurrent job creation that attached one of these uploads after step 5.
        //    (analysis_jobs.upload_id is ON DELETE CASCADE, so deleting an upload still referenced
        //    by a new job would cascade-delete that job — re-check here prevents that.)
        if (orphanedUploads.Count > 0)
        {
            var orphanedIds = orphanedUploads.Select(u => u.Id).ToList();
            await db.Uploads
                .Where(u => orphanedIds.Contains(u.Id) && !db.AnalysisJobs.Any(j => j.UploadId == u.Id))
                .ExecuteDeleteAsync(ct);
        }

        // 7. Write audit event so operators can see retention runs in the audit trail.
        db.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            EventType = "retention.jobs_purged",
            EntityId = "system",
            EventJson = JsonSerializer.Serialize(new
            {
                jobs_deleted = jobsDeleted,
                compare_results_deleted = compareDeleted,
                uploads_deleted = orphanedUploads.Count,
                cutoff_utc = cutoff
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return new RetentionRunResult(
            jobsDeleted,
            compareDeleted,
            jobIds,
            orphanedUploads.Select(u => u.Sha256).ToList());
    }

    public async Task<int> PurgeAuditEventsAsync(int auditRetentionDays, CancellationToken ct = default)
    {
        if (auditRetentionDays <= 0)
            return 0;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-auditRetentionDays);
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AuditEvents
            .Where(e => e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
