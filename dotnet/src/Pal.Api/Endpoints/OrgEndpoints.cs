using Microsoft.EntityFrameworkCore;
using Pal.Api.Auth;
using Pal.Application.Persistence;

namespace Pal.Api.Endpoints;

public static class OrgEndpoints
{
    public static void MapOrgEndpoints(this IEndpointRouteBuilder app)
    {
        var orgs = app.MapGroup("/api/orgs").RequireAuthorization(Roles.Admin);

        orgs.MapGet("/", async (IOrgRepository repo) =>
            Results.Ok(new { items = await repo.ListAsync() }))
        .WithName("ListOrgs")
        .WithTags("Orgs");

        orgs.MapPost("/", async (CreateOrgRequest req, IOrgRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Slug))
                return Results.BadRequest("Name and Slug are required.");

            try
            {
                var org = await repo.CreateAsync(req.Name, req.Slug);
                return Results.Created($"/api/orgs/{org.Id}", org);
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"Slug '{req.Slug}' is already taken.");
            }
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
            Results.Ok(new { items = await repo.ListWorkspacesAsync(orgId) }))
        .WithName("ListWorkspaces")
        .WithTags("Orgs");

        orgs.MapPost("/{orgId:guid}/workspaces", async (Guid orgId, CreateWorkspaceRequest req, IOrgRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Slug))
                return Results.BadRequest("Name and Slug are required.");

            var org = await repo.GetAsync(orgId);
            if (org is null) return Results.NotFound($"Org {orgId} not found.");

            try
            {
                var workspace = await repo.CreateWorkspaceAsync(orgId, req.Name, req.Slug);
                return Results.Created($"/api/orgs/{orgId}/workspaces/{workspace.Id}", workspace);
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"Slug '{req.Slug}' is already taken in this org.");
            }
        })
        .WithName("CreateWorkspace")
        .WithTags("Orgs");

        orgs.MapGet("/{orgId:guid}/members", async (Guid orgId, IOrgRepository repo) =>
            Results.Ok(new { items = await repo.ListMembersAsync(orgId) }))
        .WithName("ListOrgMembers")
        .WithTags("Orgs");

        orgs.MapPut("/{orgId:guid}/members/{userId}", async (Guid orgId, string userId, UpsertMemberRequest req, IOrgRepository repo) =>
        {
            if (!Roles.All.Contains(req.Role))
                return Results.BadRequest($"Role must be one of: {string.Join(", ", Roles.All)}.");

            var org = await repo.GetAsync(orgId);
            if (org is null) return Results.NotFound($"Org {orgId} not found.");
            await repo.UpsertMembershipAsync(orgId, userId, req.Role);
            return Results.NoContent();
        })
        .WithName("UpsertOrgMember")
        .WithTags("Orgs");

        orgs.MapDelete("/{orgId:guid}/members/{userId}", async (Guid orgId, string userId, IOrgRepository repo) =>
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
