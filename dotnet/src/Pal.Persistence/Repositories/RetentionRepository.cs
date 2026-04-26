using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class RetentionRepository : IRetentionRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public RetentionRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<RetentionRunResult> PurgeJobsAsync(int jobRetentionDays, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-jobRetentionDays);
        await using var db = await _factory.CreateDbContextAsync(ct);

        // 1. Identify purgeable jobs: non-baseline, terminal state, completed before cutoff.
        //    Jobs stuck in 'running'/'queued' are never purged — they may still be in progress.
        var jobIds = await db.AnalysisJobs
            .Where(j => !j.IsBaseline
                     && (j.Status == "completed" || j.Status == "failed")
                     && j.CompletedAt < cutoff)
            .Select(j => j.Id)
            .ToListAsync(ct);

        if (jobIds.Count == 0)
            return RetentionRunResult.Empty;

        // 2. Capture upload IDs before deleting jobs so we can check for orphans afterward.
        var uploadIds = await db.AnalysisJobs
            .Where(j => jobIds.Contains(j.Id))
            .Select(j => j.UploadId)
            .Distinct()
            .ToListAsync(ct);

        // 3. Delete compare_results first — both FKs are Restrict, which blocks job deletion.
        var compareDeleted = await db.CompareResults
            .Where(c => jobIds.Contains(c.BaselineJobId) || jobIds.Contains(c.CandidateJobId))
            .ExecuteDeleteAsync(ct);

        // 4. Bulk-delete jobs. DB-level CASCADE fires for: analysis_job_packs, analysis_reports,
        //    analysis_results. EF change-tracker is not involved — ExecuteDeleteAsync bypasses it.
        var jobsDeleted = await db.AnalysisJobs
            .Where(j => jobIds.Contains(j.Id))
            .ExecuteDeleteAsync(ct);

        // 5. Find uploads now fully orphaned (no remaining analysis_jobs reference them).
        var orphanedUploads = await db.Uploads
            .Where(u => uploadIds.Contains(u.Id) && !db.AnalysisJobs.Any(j => j.UploadId == u.Id))
            .Select(u => new { u.Id, u.Sha256 })
            .ToListAsync(ct);

        var orphanedIds = orphanedUploads.Select(u => u.Id).ToList();
        if (orphanedIds.Count > 0)
            await db.Uploads.Where(u => orphanedIds.Contains(u.Id)).ExecuteDeleteAsync(ct);

        // 6. Write a system audit event so operators can see retention runs in the audit trail.
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

        return new RetentionRunResult(
            jobsDeleted,
            compareDeleted,
            jobIds,
            orphanedUploads.Select(u => u.Sha256).ToList());
    }

    public async Task<int> PurgeAuditEventsAsync(int auditRetentionDays, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-auditRetentionDays);
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AuditEvents
            .Where(e => e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
