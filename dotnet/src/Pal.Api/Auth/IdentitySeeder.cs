using Microsoft.AspNetCore.Identity;
using Pal.Application.Persistence;
using Pal.Persistence;
using Pal.Persistence.Entities;

namespace Pal.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Analyst = "Analyst";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Admin, Analyst, Viewer };
}

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { Roles.Admin, Roles.Analyst, Roles.Viewer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminPassword = Environment.GetEnvironmentVariable("PAL_BOOTSTRAP_ADMIN_PASSWORD")
            ?? config["Auth:BootstrapAdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
            return;

        var adminEmail = "admin@pal.local";
        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            DisplayName = "Admin",
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            logger.LogError("Bootstrap admin creation failed: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        await services.GetRequiredService<IOrgRepository>()
            .UpsertMembershipAsync(DefaultTenant.OrgId, admin.Id, "admin");
        logger.LogInformation("Bootstrap admin account created: {Email}", adminEmail);
    }
}
