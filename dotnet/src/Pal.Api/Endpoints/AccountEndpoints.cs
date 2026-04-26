using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pal.Api.Auth;
using Pal.Application.Auth;
using Pal.Persistence.Entities;

namespace Pal.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // Form POST so the browser sends credentials and receives the Set-Cookie response directly.
        // Antiforgery disabled: credentials in the form body already prevent CSRF.
        app.MapPost("/account/login", async (
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] bool rememberMe,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Results.Redirect("/account/login?error=invalid");

            var result = await signIn.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
                return Results.Redirect("/jobs");
            if (result.IsLockedOut)
                return Results.Redirect("/account/login?error=locked");

            return Results.Redirect("/account/login?error=invalid");
        }).AllowAnonymous().DisableAntiforgery().WithTags("Account");

        // Browser-navigable logout so the browser sends its cookie and receives the clearing Set-Cookie.
        app.MapGet("/account/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Redirect("/account/login");
        }).WithTags("Account");

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

            var createResult = await userManager.CreateAsync(user, req.Password);
            if (!createResult.Succeeded)
                return Results.BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
                return Results.BadRequest(new { errors = roleResult.Errors.Select(e => e.Description) });

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

            var result = await userManager.DeleteAsync(user);
            return result.Succeeded ? Results.NoContent()
                : Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }).RequireAuthorization(Roles.Admin).WithTags("Account");
    }

    private sealed record CreateUserRequest(string Email, string Password, string Role, string? DisplayName);
}
