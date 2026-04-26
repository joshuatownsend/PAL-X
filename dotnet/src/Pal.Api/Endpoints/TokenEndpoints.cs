using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Pal.Api.Auth;
using Pal.Application.Auth;

namespace Pal.Api.Endpoints;

public static class TokenEndpoints
{
    public static void MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tokens").RequireAuthorization().WithTags("Tokens");

        group.MapGet("", async (ClaimsPrincipal user, ITokenRepository repo) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tokens = await repo.ListByUserAsync(userId);
            return Results.Ok(tokens.Select(t => new
            {
                t.Id, t.Name, t.CreatedAt, t.LastUsedAt, t.ExpiresAt,
            }));
        }).WithName("ListTokens");

        group.MapPost("", async (CreateTokenRequest req, ClaimsPrincipal user, ITokenRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var rawToken = $"pal_{WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}";
            var hash = TokenHasher.Hash(rawToken);

            var token = await repo.CreateAsync(userId, req.Name, hash, req.ExpiresAt);
            return Results.Ok(new
            {
                token.Id, token.Name, token.CreatedAt, token.ExpiresAt,
                token = rawToken,  // returned exactly once
            });
        }).WithName("CreateToken");

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, ITokenRepository repo) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var deleted = await repo.DeleteAsync(id, userId);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteToken");
    }

    private sealed record CreateTokenRequest(string Name, DateTimeOffset? ExpiresAt);
}
