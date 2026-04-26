using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Pal.Application.Auth;
using Pal.Persistence.Entities;

namespace Pal.Api.Auth;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITokenRepository tokens,
    UserManager<ApplicationUser> userManager)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return AuthenticateResult.NoResult();

        var header = authHeader.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var rawToken = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(rawToken))
            return AuthenticateResult.Fail("Empty token");

        var hash = TokenHasher.Hash(rawToken);
        var token = await tokens.FindByHashAsync(hash);

        if (token is null)
            return AuthenticateResult.Fail("Invalid token");

        if (token.ExpiresAt is { } exp && exp < DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("Token expired");

        var user = await userManager.FindByIdAsync(token.UserId);
        if (user is null)
            return AuthenticateResult.Fail("User not found");

        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new(ClaimTypes.Email, user.Email ?? ""),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        await tokens.TouchLastUsedAsync(token.Id, DateTimeOffset.UtcNow);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
