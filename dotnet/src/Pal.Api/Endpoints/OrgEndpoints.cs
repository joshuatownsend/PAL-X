using System.Security.Claims;
using Pal.Application.Persistence;
using Pal.Persistence;

namespace Pal.Api.Endpoints;

public static class OrgEndpoints
{
    public static void MapOrgEndpoints(this IEndpointRouteBuilder app)
    {
        var orgs = app.MapGroup("/api/orgs");

        orgs.MapGet("/", async (IOrgRepository repo) =>
        {
            var list = await repo.ListAsync();
            return Results.Ok(new { items = list });
        })
        .WithName("ListOrgs")
        .WithTags("Orgs");

        orgs.MapPost("/", async (CreateOrgRequest req, IOrgRepository repo) =>
        {
            var org = await repo.CreateAsync(req.Name, req.Slug);
            return Results.Created($"/api/orgs/{org.Id}", org);
        })
        .WithName("CreateOrg")
        .WithTags("Orgs");

        orgs.MapGet("/{orgId:guid}", async (Guid orgId, IOrgRepository repo) =>
        {
            var org = await repo.GetAsync(orgId);
            return org is null ? Results.NotFound() : Results.Ok(org);
        })
        .WithName("GetOrg")
        .WithTags("Orgs");

        orgs.MapGet("/{orgId:guid}/workspaces", async (Guid orgId, IOrgRepository repo) =>
        {
            var workspaces = await repo.ListWorkspacesAsync(orgId);
            return Results.Ok(new { items = workspaces });
        })
        .WithName("ListWorkspaces")
        .WithTags("Orgs");

        orgs.MapPost("/{orgId:guid}/workspaces", async (Guid orgId, CreateWorkspaceRequest req, IOrgRepository repo) =>
        {
            var org = await repo.GetAsync(orgId);
            if (org is null) return Results.NotFound($"Org {orgId} not found.");
            var workspace = await repo.CreateWorkspaceAsync(orgId, req.Name, req.Slug);
            return Results.Created($"/api/orgs/{orgId}/workspaces/{workspace.Id}", workspace);
        })
        .WithName("CreateWorkspace")
        .WithTags("Orgs");

        orgs.MapGet("/{orgId:guid}/members", async (Guid orgId, IOrgRepository repo) =>
        {
            var members = await repo.ListMembersAsync(orgId);
            return Results.Ok(new { items = members });
        })
        .WithName("ListOrgMembers")
        .WithTags("Orgs");

        orgs.MapPut("/{orgId:guid}/members/{userId}", async (
            Guid orgId, string userId, UpsertMemberRequest req,
            IOrgRepository repo, ClaimsPrincipal user) =>
        {
            var org = await repo.GetAsync(orgId);
            if (org is null) return Results.NotFound($"Org {orgId} not found.");
            await repo.UpsertMembershipAsync(orgId, userId, req.Role);
            return Results.NoContent();
        })
        .WithName("UpsertOrgMember")
        .WithTags("Orgs");

        orgs.MapDelete("/{orgId:guid}/members/{userId}", async (
            Guid orgId, string userId, IOrgRepository repo, ClaimsPrincipal user) =>
        {
            var removed = await repo.RemoveMembershipAsync(orgId, userId);
            return removed ? Results.NoContent() : Results.NotFound();
        })
        .WithName("RemoveOrgMember")
        .WithTags("Orgs");
    }

    private sealed record CreateOrgRequest(string Name, string Slug);
    private sealed record CreateWorkspaceRequest(string Name, string Slug);
    private sealed record UpsertMemberRequest(string Role);
}
