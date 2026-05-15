using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Application.Persistence;
using Pal.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class AnalysisRepositoryTests(PalApiFactory factory)
{
    // The Pal.Api.Tests collection shares one Postgres container, so tests can't assume
    // the absolute position of their seeded rows in the result. All assertions filter the
    // result down to this test's own seeded job IDs and check ordering within that subset.

    private IAnalysisRepository Repo =>
        factory.Services.GetRequiredService<IAnalysisRepository>();

    private IDbContextFactory<PalDbContext> DbFactory =>
        factory.Services.GetRequiredService<IDbContextFactory<PalDbContext>>();

    private ITenantContext Tenant =>
        factory.Services.GetRequiredService<ITenantContext>();

    [Fact]
    public async Task GetRecentCompletedJobs_ZeroLimit_ReturnsEmpty_WithoutDbContext()
    {
        var result = await Repo.GetRecentCompletedJobsAsync(0);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentCompletedJobs_FiltersToCompletedOnly_AndOrdersNewestFirst()
    {
        using var _ = Tenant.SetWorkspace(DefaultTenant.WorkspaceId);
        var upload = await SeedUploadAsync();

        var now = DateTimeOffset.UtcNow;
        var completedNewest = await SeedJobAsync(upload.Id, "completed", createdAt: now.AddMinutes(-2), completedAt: now.AddMinutes(-1));
        var completedMiddle = await SeedJobAsync(upload.Id, "completed", createdAt: now.AddMinutes(-11), completedAt: now.AddMinutes(-10));
        var completedOldest = await SeedJobAsync(upload.Id, "completed", createdAt: now.AddMinutes(-101), completedAt: now.AddMinutes(-100));
        var queuedJob = await SeedJobAsync(upload.Id, "queued", createdAt: now.AddMinutes(-1), completedAt: null);
        var failedJob = await SeedJobAsync(upload.Id, "failed", createdAt: now.AddMinutes(-4), completedAt: now.AddMinutes(-3));

        var result = await Repo.GetRecentCompletedJobsAsync(limit: int.MaxValue);
        var myIds = result.Select(j => j.Id)
            .Where(id => id == completedNewest.Id || id == completedMiddle.Id || id == completedOldest.Id
                      || id == queuedJob.Id || id == failedJob.Id)
            .ToList();

        Assert.Equal(new[] { completedNewest.Id, completedMiddle.Id, completedOldest.Id }, myIds);
    }

    [Fact]
    public async Task GetRecentCompletedJobs_TieOnCompletedAt_IsDeterministicByIdDescending()
    {
        using var _ = Tenant.SetWorkspace(DefaultTenant.WorkspaceId);
        var upload = await SeedUploadAsync();

        var sharedCompleted = DateTimeOffset.UtcNow.AddMinutes(-5);
        var sharedCreated = sharedCompleted.AddSeconds(-30);
        var smallerId = new Guid("00000000-0000-0000-0000-000000000001");
        var largerId = new Guid("ffffffff-ffff-ffff-ffff-fffffffffffe");
        await SeedJobAsync(upload.Id, "completed", completedAt: sharedCompleted, createdAt: sharedCreated, id: smallerId);
        await SeedJobAsync(upload.Id, "completed", completedAt: sharedCompleted, createdAt: sharedCreated, id: largerId);

        var firstCall = await Repo.GetRecentCompletedJobsAsync(limit: int.MaxValue);
        var secondCall = await Repo.GetRecentCompletedJobsAsync(limit: int.MaxValue);

        var firstPair = firstCall.Select(j => j.Id).Where(id => id == smallerId || id == largerId).ToList();
        var secondPair = secondCall.Select(j => j.Id).Where(id => id == smallerId || id == largerId).ToList();

        Assert.Equal(new[] { largerId, smallerId }, firstPair);
        Assert.Equal(firstPair, secondPair);
    }

    [Fact]
    public async Task GetRecentCompletedJobs_OrdersByCompletedAtNotCreatedAt()
    {
        using var _ = Tenant.SetWorkspace(DefaultTenant.WorkspaceId);
        var upload = await SeedUploadAsync();

        var now = DateTimeOffset.UtcNow;
        // Old-created but recently-completed should win over newly-created but older-completed.
        var slowJob = await SeedJobAsync(upload.Id, "completed",
            createdAt: now.AddHours(-2),
            completedAt: now.AddMinutes(-1));
        var quickJob = await SeedJobAsync(upload.Id, "completed",
            createdAt: now.AddMinutes(-30),
            completedAt: now.AddMinutes(-25));

        var result = await Repo.GetRecentCompletedJobsAsync(limit: int.MaxValue);
        var myIds = result.Select(j => j.Id)
            .Where(id => id == slowJob.Id || id == quickJob.Id)
            .ToList();

        Assert.Equal(new[] { slowJob.Id, quickJob.Id }, myIds);
    }

    [Fact]
    public async Task GetRecentCompletedJobs_AppliesLimit()
    {
        using var _ = Tenant.SetWorkspace(DefaultTenant.WorkspaceId);

        // Snapshot the current completed-job count, then seed enough to ensure limit truncates.
        var before = await Repo.GetRecentCompletedJobsAsync(limit: int.MaxValue);
        var upload = await SeedUploadAsync();
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 3; i++)
            await SeedJobAsync(upload.Id, "completed", createdAt: now.AddMinutes(-i - 1), completedAt: now.AddMinutes(-i));

        var limited = await Repo.GetRecentCompletedJobsAsync(limit: before.Count + 1);

        Assert.Equal(before.Count + 1, limited.Count);
    }

    private async Task<UploadEntity> SeedUploadAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var upload = new UploadEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = DefaultTenant.WorkspaceId,
            FileName = $"recent-completed-{Guid.NewGuid():N}.csv",
            SourceType = "csv",
            SizeBytes = 100,
            Sha256 = Guid.NewGuid().ToString("N"),
            StoragePath = $"uploads/test/{Guid.NewGuid():N}.csv",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        db.Uploads.Add(upload);
        await db.SaveChangesAsync();
        return upload;
    }

    private async Task<AnalysisJobEntity> SeedJobAsync(
        Guid uploadId,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt,
        Guid? id = null)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var job = new AnalysisJobEntity
        {
            Id = id ?? Guid.NewGuid(),
            WorkspaceId = DefaultTenant.WorkspaceId,
            UploadId = uploadId,
            Status = status,
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };
        db.AnalysisJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }
}
