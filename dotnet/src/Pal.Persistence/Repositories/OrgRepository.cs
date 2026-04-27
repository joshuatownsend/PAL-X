using Microsoft.EntityFrameworkCore;
using Pal.Application.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Persistence.Repositories;

public sealed class OrgRepository : IOrgRepository
{
    private readonly IDbContextFactory<PalDbContext> _factory;

    public OrgRepository(IDbContextFactory<PalDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<OrgDto>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Orgs.OrderBy(o => o.Name).Select(o => ToDto(o)).ToListAsync(ct);
    }

    public async Task<OrgDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Orgs.FindAsync([id], ct);
        return e is null ? null : ToDto(e);
    }

    public async Task<OrgDto> CreateAsync(string name, string slug, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var org = new OrgEntity { Id = Guid.NewGuid(), Name = name, Slug = slug, CreatedAt = DateTimeOffset.UtcNow };
        db.Orgs.Add(org);
        await db.SaveChangesAsync(ct);
        return ToDto(org);
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspacesAsync(Guid orgId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Workspaces
            .Where(w => w.OrgId == orgId)
            .OrderBy(w => w.Name)
            .Select(w => ToWsDto(w))
            .ToListAsync(ct);
    }

    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid workspaceId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Workspaces.FindAsync([workspaceId], ct);
        return e is null ? null : ToWsDto(e);
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(Guid orgId, string name, string slug, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var ws = new WorkspaceEntity { Id = Guid.NewGuid(), OrgId = orgId, Name = name, Slug = slug, CreatedAt = DateTimeOffset.UtcNow };
        db.Workspaces.Add(ws);
        await db.SaveChangesAsync(ct);
        return ToWsDto(ws);
    }

    public async Task<IReadOnlyList<OrgMembershipDto>> ListMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.OrgMemberships
            .Where(m => m.OrgId == orgId)
            .Select(m => ToMemberDto(m))
            .ToListAsync(ct);
    }

    public async Task<OrgMembershipDto?> GetMembershipAsync(Guid orgId, string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.OrgMemberships.FindAsync([orgId, userId], ct);
        return e is null ? null : ToMemberDto(e);
    }

    public async Task UpsertMembershipAsync(Guid orgId, string userId, string role, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var existing = await db.OrgMemberships.FindAsync([orgId, userId], ct);
        if (existing is not null)
        {
            existing.Role = role;
        }
        else
        {
            db.OrgMemberships.Add(new OrgMembershipEntity
            {
                OrgId = orgId, UserId = userId, Role = role,
                JoinedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveMembershipAsync(Guid orgId, string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.OrgMemberships
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    private static OrgDto ToDto(OrgEntity e) => new() { Id = e.Id, Name = e.Name, Slug = e.Slug, CreatedAt = e.CreatedAt };
    private static WorkspaceDto ToWsDto(WorkspaceEntity e) => new() { Id = e.Id, OrgId = e.OrgId, Name = e.Name, Slug = e.Slug, CreatedAt = e.CreatedAt };
    private static OrgMembershipDto ToMemberDto(OrgMembershipEntity e) => new() { OrgId = e.OrgId, UserId = e.UserId, Role = e.Role, JoinedAt = e.JoinedAt };
}
