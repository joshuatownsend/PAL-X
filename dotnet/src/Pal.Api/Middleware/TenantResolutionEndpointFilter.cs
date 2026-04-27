using Microsoft.EntityFrameworkCore;
using Pal.Persistence;

namespace Pal.Api.Middleware;

/// <summary>
/// Endpoint filter applied to the /api/workspaces/{workspaceId} group.
/// Validates the workspace exists, that the caller is a member of its org, then sets
/// ITenantContext.WorkspaceId for the duration of the request so EF query filters scope
/// all reads/writes to this workspace.
/// </summary>
public sealed class TenantResolutionEndpointFilter : IEndpointFilter
{
    private readonly IDbContextFactory<PalDbContext> _factory;
    private readonly ITenantContext _tenant;

    public TenantResolutionEndpointFilter(IDbContextFactory<PalDbContext> factory, ITenantContext tenant)
    {
        _factory = factory;
        _tenant = tenant;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue("workspaceId", out var raw)
            || !Guid.TryParse(raw?.ToString(), out var workspaceId))
        {
            return Results.BadRequest("workspaceId route parameter is required and must be a GUID.");
        }

        await using var db = await _factory.CreateDbContextAsync(context.HttpContext.RequestAborted);

        // Verify workspace exists (unfiltered — WorkspaceId is not set yet).
        var workspace = await db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId, context.HttpContext.RequestAborted);

        if (workspace is null)
            return Results.NotFound($"Workspace {workspaceId} not found.");

        // Verify caller is a member of the workspace's org.
        var userId = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            var isMember = await db.OrgMemberships
                .AsNoTracking()
                .AnyAsync(m => m.OrgId == workspace.OrgId && m.UserId == userId,
                    context.HttpContext.RequestAborted);

            if (!isMember)
                return Results.Forbid();
        }

        using var _ = _tenant.SetWorkspace(workspaceId);
        return await next(context);
    }
}
