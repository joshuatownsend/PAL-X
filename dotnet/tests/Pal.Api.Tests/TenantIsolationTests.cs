using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class TenantIsolationTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private IDbContextFactory<PalDbContext> DbFactory =>
        factory.Services.GetRequiredService<IDbContextFactory<PalDbContext>>();

    // Seeds a workspace in the given org, an upload, and one job.
    // Returns the new workspace id and the seeded job id.
    // Saves in three rounds (workspace → upload → job) to satisfy FK ordering constraints.
    private async Task<(Guid WorkspaceId, Guid JobId)> SeedWorkspaceWithJobAsync(
        Guid orgId, CancellationToken ct)
    {
        await using var db = await DbFactory.CreateDbContextAsync(ct);

        // Round 1: workspace (FK: org must already exist — DefaultTenant.OrgId is seeded by migrations)
        var workspaceId = Guid.NewGuid();
        db.Workspaces.Add(new WorkspaceEntity
        {
            Id = workspaceId,
            OrgId = orgId,
            Name = $"ws-{workspaceId:N}",
            Slug = $"ws-{workspaceId:N}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // Round 2: upload (FK: workspace_id)
        var uploadId = Guid.NewGuid();
        db.Uploads.Add(new UploadEntity
        {
            Id = uploadId,
            WorkspaceId = workspaceId,
            FileName = $"iso-{uploadId:N}.csv",
            SourceType = "csv",
            SizeBytes = 100,
            Sha256 = Guid.NewGuid().ToString("N"),
            StoragePath = $"uploads/test/{uploadId:N}.csv",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // Round 3: job (FK: workspace_id + upload_id)
        var jobId = Guid.NewGuid();
        db.AnalysisJobs.Add(new AnalysisJobEntity
        {
            Id = jobId,
            WorkspaceId = workspaceId,
            UploadId = uploadId,
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return (workspaceId, jobId);
    }

    private async Task<Guid> SeedForeignOrgWorkspaceAsync(CancellationToken ct)
    {
        await using var db = await DbFactory.CreateDbContextAsync(ct);

        // Round 1: org
        var orgId = Guid.NewGuid();
        db.Orgs.Add(new OrgEntity
        {
            Id = orgId,
            Name = $"org-{orgId:N}",
            Slug = $"org-{orgId:N}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // Round 2: workspace (FK: org_id)
        // Intentionally NO OrgMembership for TestUserId — caller is a stranger.
        var workspaceId = Guid.NewGuid();
        db.Workspaces.Add(new WorkspaceEntity
        {
            Id = workspaceId,
            OrgId = orgId,
            Name = $"ws-{workspaceId:N}",
            Slug = $"ws-{workspaceId:N}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return workspaceId;
    }

    [Fact]
    public async Task Job_InOtherWorkspaceSameOrg_NotInDefaultWorkspaceList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, otherJobId) = await SeedWorkspaceWithJobAsync(DefaultTenant.OrgId, ct);

        var resp = await _client.GetAsync($"{PalApiFactory.WsBase}/analysis", ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var ids = body.GetProperty("items").EnumerateArray()
            .Select(j => j.GetProperty("id").GetString())
            .ToList();
        Assert.DoesNotContain(otherJobId.ToString(), ids);
    }

    [Fact]
    public async Task Job_InOtherWorkspaceSameOrg_NotRetrievableByIdFromDefaultWorkspace()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, otherJobId) = await SeedWorkspaceWithJobAsync(DefaultTenant.OrgId, ct);

        var resp = await _client.GetAsync($"{PalApiFactory.WsBase}/analysis/{otherJobId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Job_IsVisibleUnderItsOwnWorkspaceRoute_Control()
    {
        // Control: the same job IS reachable from its own workspace route,
        // proving it is only *hidden* by tenancy, not absent.
        var ct = TestContext.Current.CancellationToken;
        var (otherWsId, otherJobId) = await SeedWorkspaceWithJobAsync(DefaultTenant.OrgId, ct);

        var resp = await _client.GetAsync($"/api/workspaces/{otherWsId}/analysis/{otherJobId}", ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Workspace_InForeignOrg_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var foreignWsId = await SeedForeignOrgWorkspaceAsync(ct);

        var resp = await _client.GetAsync($"/api/workspaces/{foreignWsId}/analysis", ct);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Workspace_Nonexistent_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var resp = await _client.GetAsync($"/api/workspaces/{Guid.NewGuid()}/analysis", ct);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
