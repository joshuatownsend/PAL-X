using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class AnalysisRepository : IAnalysisRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;
    private readonly ITenantContext _tenant;

    public AnalysisRepository(IDbContextFactory<PalDbContext> factory, ITenantContext tenant)
    {
        _factory = factory;
        _tenant = tenant;
    }

    public async Task<AnalysisJobDto> CreateJobAsync(Guid uploadId, IReadOnlyList<string> packIds, bool includeDataset = false, Guid? selectedBaselineId = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var job = new AnalysisJobEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _tenant.WorkspaceId ?? throw new InvalidOperationException("Tenant workspace is not set. Ensure the request passes through the workspace route group."),
            UploadId = uploadId,
            Status = "queued",
            OptionsJson = JsonSerializer.Serialize(new { requestedPacks = packIds, includeDataset, selectedBaselineId }),
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
                .SetProperty(j => j.CompletedAt, DateTimeOffset.UtcNow)
                .SetProperty(j => j.FailureReason, (string?)null), ct);
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
        await db.AnalysisResults.Where(r => r.AnalysisJobId == jobId).ExecuteDeleteAsync(ct);
        db.AnalysisResults.Add(new AnalysisResultEntity
        {
            AnalysisJobId = jobId,
            SummaryJson = summaryJson,
            FindingsJson = findingsJson,
            GeneratedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveReportAsync(Guid jobId, string format, string storagePath, long sizeBytes, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.AnalysisReports
            .Where(r => r.AnalysisJobId == jobId && r.Format == format)
            .ExecuteDeleteAsync(ct);
        db.AnalysisReports.Add(new AnalysisReportEntity
        {
            Id = Guid.NewGuid(),
            AnalysisJobId = jobId,
            Format = format,
            StoragePath = storagePath,
            SizeBytes = sizeBytes,
            CreatedAt = DateTimeOffset.UtcNow
        });
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

    public async Task<IReadOnlyList<AnalysisResultDto>> GetResultsAsync(IEnumerable<Guid> jobIds, CancellationToken ct = default)
    {
        var ids = jobIds.ToList();
        if (ids.Count == 0) return [];
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.AnalysisResults
            .Where(r => ids.Contains(r.AnalysisJobId))
            .Select(r => new AnalysisResultDto
            {
                AnalysisJobId = r.AnalysisJobId,
                SummaryJson = r.SummaryJson,
                FindingsJson = r.FindingsJson,
                GeneratedAt = r.GeneratedAt
            })
            .ToListAsync(ct);
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
        await db.AnalysisJobPacks.Where(p => p.AnalysisJobId == jobId).ExecuteDeleteAsync(ct);
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

    public async Task SetBaselineAsync(Guid jobId, bool isBaseline, string? label, string? type = null, string? contextJson = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.AnalysisJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.IsBaseline, isBaseline)
                .SetProperty(j => j.BaselineLabel, isBaseline ? label : null)
                .SetProperty(j => j.BaselineType, isBaseline ? type : null)
                .SetProperty(j => j.BaselineContextJson, isBaseline ? contextJson : null), ct);
    }

    public async Task<IReadOnlyList<AnalysisJobDto>> ListBaselinesAsync(string? type = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.AnalysisJobs.Include(j => j.Packs).Where(j => j.IsBaseline);
        if (type is not null)
            query = query.Where(j => j.BaselineType == type);
        var jobs = await query.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
        return jobs.Select(j => ToDto(j, j.Packs.Select(ToPackDto).ToList())).ToList();
    }

    public async Task<IReadOnlyList<AnalysisJobDto>> GetBaselineVersionsAsync(string type, string contextJson, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var jobs = await db.AnalysisJobs
            .Include(j => j.Packs)
            .Where(j => j.IsBaseline && j.BaselineType == type && j.BaselineContextJson == contextJson)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(ct);
        return jobs.Select(j => ToDto(j, j.Packs.Select(ToPackDto).ToList())).ToList();
    }

    public async Task SaveDatasetArtifactAsync(Guid jobId, string storagePath, long byteLength, bool compressed, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.AnalysisResults
            .Where(r => r.AnalysisJobId == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.DatasetStoragePath, storagePath)
                .SetProperty(r => r.DatasetByteLength, byteLength)
                .SetProperty(r => r.DatasetCompressed, compressed), ct);
    }

    public async Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.AnalysisResults.FindAsync([jobId], ct);
        if (e?.DatasetStoragePath is null) return null;
        return new DatasetArtifactDto
        {
            StoragePath = e.DatasetStoragePath,
            ByteLength = e.DatasetByteLength ?? 0,
            Compressed = e.DatasetCompressed ?? false
        };
    }

    private static AnalysisJobDto ToDto(AnalysisJobEntity j, IReadOnlyList<JobPackDto> packs) => new()
    {
        Id = j.Id,
        WorkspaceId = j.WorkspaceId,
        UploadId = j.UploadId,
        Status = j.Status,
        OptionsJson = j.OptionsJson,
        CreatedAt = j.CreatedAt,
        StartedAt = j.StartedAt,
        CompletedAt = j.CompletedAt,
        FailureReason = j.FailureReason,
        Packs = packs,
        IsBaseline = j.IsBaseline,
        BaselineLabel = j.BaselineLabel,
        BaselineType = j.BaselineType,
        BaselineContextJson = j.BaselineContextJson
    };

    private static JobPackDto ToPackDto(AnalysisJobPackEntity p) => new()
    {
        PackId = p.PackId,
        PackVersion = p.PackVersion
    };
}
