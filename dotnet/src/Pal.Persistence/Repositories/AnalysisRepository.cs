using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class AnalysisRepository : IAnalysisRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public AnalysisRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<AnalysisJobDto> CreateJobAsync(Guid uploadId, IReadOnlyList<string> packIds, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var job = new AnalysisJobEntity
        {
            Id = Guid.NewGuid(),
            UploadId = uploadId,
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AnalysisJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return ToDto(job, []);
    }

    public async Task<AnalysisJobDto?> GetJobAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var job = await db.AnalysisJobs
            .Include(j => j.Packs)
            .FirstOrDefaultAsync(j => j.Id == id, ct);
        return job is null ? null : ToDto(job, job.Packs.Select(ToPackDto).ToList());
    }

    public async Task<IReadOnlyList<AnalysisJobDto>> ListJobsAsync(string? statusFilter, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.AnalysisJobs.Include(j => j.Packs).AsQueryable();
        if (statusFilter is not null)
            query = query.Where(j => j.Status == statusFilter);
        var jobs = await query.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
        return jobs.Select(j => ToDto(j, j.Packs.Select(ToPackDto).ToList())).ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetQueuedJobIdsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AnalysisJobs
            .Where(j => j.Status == "queued")
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> TryClaimJobAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.AnalysisJobs
            .Where(j => j.Id == id && j.Status == "queued")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "running")
                .SetProperty(j => j.StartedAt, DateTimeOffset.UtcNow), ct);
        return rows > 0;
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.AnalysisJobs
            .Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "completed")
                .SetProperty(j => j.CompletedAt, DateTimeOffset.UtcNow), ct);
    }

    public async Task MarkFailedAsync(Guid id, string reason, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.AnalysisJobs
            .Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "failed")
                .SetProperty(j => j.FailureReason, reason)
                .SetProperty(j => j.CompletedAt, DateTimeOffset.UtcNow), ct);
    }

    public async Task ResetOrphanedJobsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.AnalysisJobs
            .Where(j => j.Status == "running")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "queued")
                .SetProperty(j => j.StartedAt, (DateTimeOffset?)null)
                .SetProperty(j => j.FailureReason, "worker restart during execution"), ct);
    }

    public async Task SaveResultAsync(Guid jobId, string summaryJson, string findingsJson, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var result = new AnalysisResultEntity
        {
            AnalysisJobId = jobId,
            SummaryJson = summaryJson,
            FindingsJson = findingsJson,
            GeneratedAt = DateTimeOffset.UtcNow
        };
        db.AnalysisResults.Add(result);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveReportAsync(Guid jobId, string format, string storagePath, long sizeBytes, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var report = new AnalysisReportEntity
        {
            Id = Guid.NewGuid(),
            AnalysisJobId = jobId,
            Format = format,
            StoragePath = storagePath,
            SizeBytes = sizeBytes,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AnalysisReports.Add(report);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AnalysisResultDto?> GetResultAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.AnalysisResults.FindAsync([jobId], ct);
        return e is null ? null : new AnalysisResultDto
        {
            AnalysisJobId = e.AnalysisJobId,
            SummaryJson = e.SummaryJson,
            FindingsJson = e.FindingsJson,
            GeneratedAt = e.GeneratedAt
        };
    }

    public async Task<IReadOnlyList<AnalysisReportDto>> GetReportsAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AnalysisReports
            .Where(r => r.AnalysisJobId == jobId)
            .Select(r => new AnalysisReportDto
            {
                Id = r.Id,
                AnalysisJobId = r.AnalysisJobId,
                Format = r.Format,
                StoragePath = r.StoragePath,
                SizeBytes = r.SizeBytes,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task SetJobPackVersionsAsync(Guid jobId, IReadOnlyList<JobPackDto> packs, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var p in packs)
        {
            db.AnalysisJobPacks.Add(new AnalysisJobPackEntity
            {
                AnalysisJobId = jobId,
                PackId = p.PackId,
                PackVersion = p.PackVersion
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static AnalysisJobDto ToDto(AnalysisJobEntity j, IReadOnlyList<JobPackDto> packs) => new()
    {
        Id = j.Id,
        UploadId = j.UploadId,
        Status = j.Status,
        CreatedAt = j.CreatedAt,
        StartedAt = j.StartedAt,
        CompletedAt = j.CompletedAt,
        FailureReason = j.FailureReason,
        Packs = packs
    };

    private static JobPackDto ToPackDto(AnalysisJobPackEntity p) => new()
    {
        PackId = p.PackId,
        PackVersion = p.PackVersion
    };
}
