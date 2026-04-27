using System.Security.Claims;
using System.Text.Encodings.Web;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pal.Application.Persistence;
using Pal.Persistence;
using Pal.Persistence.Entities;
using Testcontainers.PostgreSql;

namespace Pal.Api.Tests;

public sealed class PalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestUserId = "test-user-id";
    public static readonly string WsBase = $"/api/workspaces/{DefaultTenant.WorkspaceId}";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pal_test")
        .WithUsername("pal")
        .WithPassword("paltest")
        .Build();

    // Temp directories for test isolation
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"pal-test-{Guid.NewGuid():N}");
    private readonly string _packsRoot = Path.Combine(Path.GetTempPath(), $"pal-packs-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(_packsRoot);
    }

    // WebApplicationFactory<T>.DisposeAsync() returns ValueTask (IAsyncDisposable);
    // IAsyncLifetime.DisposeAsync() requires Task — incompatible return types, so `new` is the only option.
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        try { Directory.Delete(_storageRoot, recursive: true); } catch { /* best-effort */ }
        try { Directory.Delete(_packsRoot, recursive: true); } catch { /* best-effort */ }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Storage:LocalRoot"] = _storageRoot,
                ["Packs:Directory"] = _packsRoot,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Register the test scheme handler.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // PostConfigure runs AFTER all Configure<AuthenticationOptions> registrations, including
            // those set by AddIdentity (which claims DefaultAuthenticateScheme/DefaultChallengeScheme).
            // This guarantees our test scheme wins.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultSignInScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    // Seed the test auth identity user + org membership after migrations have run.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        SeedTestUserAsync(host.Services).GetAwaiter().GetResult();
        return host;
    }

    private static async Task SeedTestUserAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByIdAsync(TestUserId) is null)
        {
            var user = new ApplicationUser
            {
                Id = TestUserId,
                UserName = "test@pal.local",
                Email = "test@pal.local",
                EmailConfirmed = true,
            };
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Test user seed failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var orgRepo = sp.GetRequiredService<IOrgRepository>();
        await orgRepo.UpsertMembershipAsync(DefaultTenant.OrgId, TestUserId, "admin");
    }

    /// <summary>Creates an authenticated client with the given role (default: Admin).</summary>
    public HttpClient CreateClient(string role = "Admin")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return client;
    }
}

/// <summary>
/// In-process auth handler for integration tests. Reads role from a request header so tests
/// can exercise different authorization levels without real Identity infrastructure.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers.TryGetValue(RoleHeader, out var v) ? v.ToString() : "Admin";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, PalApiFactory.TestUserId),
            new Claim(ClaimTypes.Name, "test@pal.local"),
            new Claim(ClaimTypes.Email, "test@pal.local"),
            new Claim(ClaimTypes.Role, role),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
