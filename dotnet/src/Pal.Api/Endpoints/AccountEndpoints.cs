using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pal.Api.Auth;
using Pal.Application.Auth;
using Pal.Persistence.Entities;

namespace Pal.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/account/login", async (
            LoginRequest req,
            SignInManager<ApplicationUser> signIn,
            HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var result = await signIn.PasswordSignInAsync(req.Email, req.Password, req.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
                return Results.Ok();
            if (result.IsLockedOut)
                return Results.Problem("Account locked out. Try again later.", statusCode: 429);

            return Results.Unauthorized();
        }).AllowAnonymous().WithTags("Account");

        app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok();
        }).RequireAuthorization().WithTags("Account");

        app.MapGet("/account/me", (ClaimsPrincipal user) =>
            Results.Ok(new
            {
                id = user.FindFirstValue(ClaimTypes.NameIdentifier),
                email = user.FindFirstValue(ClaimTypes.Email),
                roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value),
            })
        ).RequireAuthorization().WithName("GetCurrentUser").WithTags("Account");

        app.MapPost("/account/users", async (
            CreateUserRequest req,
            UserManager<ApplicationUser> userManager) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var role = Roles.All.Contains(req.Role) ? req.Role : Roles.Viewer;

            var user = new ApplicationUser
            {
                UserName = req.Email,
                Email = req.Email,
                DisplayName = req.DisplayName,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            await userManager.AddToRoleAsync(user, role);
            return Results.Ok(new { user.Id, user.Email, Role = role });
        }).RequireAuthorization(Roles.Admin).WithTags("Account");

        app.MapGet("/account/users", async (UserManager<ApplicationUser> userManager) =>
        {
            var users = await userManager.Users
                .Select(u => new { u.Id, u.Email, u.DisplayName })
                .ToListAsync();
            return Results.Ok(users);
        }).RequireAuthorization(Roles.Admin).WithTags("Account");

        app.MapDelete("/account/users/{id}", async (string id, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            await userManager.DeleteAsync(user);
            return Results.NoContent();
        }).RequireAuthorization(Roles.Admin).WithTags("Account");
    }

    private sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
    private sealed record CreateUserRequest(string Email, string Password, string Role, string? DisplayName);
}
