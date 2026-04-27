using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pal.Api.Auth;
using Pal.Api.Components;
using Pal.Api.Endpoints;
using Pal.Api.Middleware;
using Pal.Api.Services;
using Pal.Api.Worker;
using Pal.Application.Alerts;
using Pal.Application.Analysis;
using Pal.Application.Auth;
using Pal.Application.Compare;
using Pal.Application.Diagnostics;
using Pal.Application.Persistence;
using Pal.Application.Correlation;
using Pal.Application.Trends;
using Pal.Application.Storage;
using Pal.Application.Webhooks;
using Pal.Persistence;
using Pal.Persistence.Entities;
using Pal.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Tenant context: singleton with AsyncLocal storage for per-request workspace isolation
builder.Services.AddSingleton<TenantContext>();
builder.Services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// Upload size limits: 512 MB
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 536_870_912);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 536_870_912);

// Postgres — DbContextFactory for Blazor Server + BackgroundService safety
builder.Services.AddDbContextFactory<PalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
           .UseSnakeCaseNamingConvention());

// Identity requires a scoped DbContext (not factory), so register a scoped resolver too
builder.Services.AddDbContext<PalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
           .UseSnakeCaseNamingConvention());

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 10;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<PalDbContext>()
    .AddDefaultTokenProviders();

// Cookie auth (browser / Blazor) + API key scheme (CLI)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { })
    .AddPolicyScheme("CookieOrApiKey", "Cookie or API key", options =>
    {
        // ForwardDefaultSelector applies to all operations (authenticate, challenge, forbid)
        // when no per-operation override is set.
        options.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.Authorization.ToString()
               .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? ApiKeyAuthenticationHandler.SchemeName
                : IdentityConstants.ApplicationScheme;
    });

// PostConfigure wins over AddIdentity's defaults; keep DefaultSignInScheme untouched
// so SignInManager.PasswordSignInAsync can still write the cookie.
builder.Services.PostConfigure<AuthenticationOptions>(options =>
{
    options.DefaultScheme = "CookieOrApiKey";
    options.DefaultAuthenticateScheme = "CookieOrApiKey";
    options.DefaultChallengeScheme = "CookieOrApiKey";
});

// Authorization policies — no explicit scheme list so the active default scheme is used,
// which lets tests swap in TestAuthHandler without touching policy registration.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Roles.Analyst, p => p.RequireRole(Roles.Admin, Roles.Analyst));

// Local disk storage
var storageRoot = Path.GetFullPath(
    builder.Configuration["Storage:LocalRoot"] ?? "data/storage");
builder.Services.AddSingleton<IStorageProvider>(_ => new LocalDiskStorageProvider(storageRoot));

// Repositories (singleton: stateless, use factory per call — safe for all lifetimes)
builder.Services.AddSingleton<IOrgRepository, OrgRepository>();
builder.Services.AddSingleton<IUploadRepository, UploadRepository>();
builder.Services.AddSingleton<IAnalysisRepository, AnalysisRepository>();
builder.Services.AddSingleton<IPackRepository, PackRepository>();
builder.Services.AddSingleton<ICompareRepository, CompareRepository>();
builder.Services.AddSingleton<IAlertRepository, AlertRepository>();
builder.Services.AddSingleton<IWebhookSinkRepository, WebhookSinkRepository>();
builder.Services.AddSingleton<IWebhookSinkService, WebhookSinkService>();
builder.Services.AddSingleton<ITokenRepository, TokenRepository>();
builder.Services.AddSingleton<IRetentionRepository, RetentionRepository>();
builder.Services.AddHttpClient("pal-webhook")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<CompareRunner>();
builder.Services.AddSingleton<IAutoCompareService, AutoCompareService>();
builder.Services.AddSingleton<TrendAnalyzer>();
builder.Services.AddSingleton<TrendService>();
builder.Services.AddSingleton<CorrelationAnalyzer>();
builder.Services.AddSingleton<CorrelationService>();
builder.Services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

// Analysis runner and worker channel
builder.Services.AddSingleton<IAnalysisRunner, AnalysisRunner>();
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true }));
builder.Services.AddHostedService<AnalysisWorker>();
builder.Services.AddHostedService<RetentionWorker>();

// Pack registry sync
builder.Services.AddSingleton<PackRegistrySyncService>();

// Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "PAL API", Version = "v1" }));

var app = builder.Build();

// Run EF migrations on startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PalDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// Seed roles + bootstrap admin
await using (var scope = app.Services.CreateAsyncScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration,
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

// Sync pack registry from disk
var packSync = app.Services.GetRequiredService<PackRegistrySyncService>();
var packsDir = Path.GetFullPath(app.Configuration["Packs:Directory"] ?? "packs/thresholds");
await packSync.SyncAsync(packsDir);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAuthentication();   // must precede UseAuthorization
app.UseAuthorization();
app.UseAntiforgery();

// REST API endpoints — global (no workspace scope)
app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapPackEndpoints();
app.MapOrgEndpoints();

// Workspace-scoped endpoints — all routes prefixed with /api/workspaces/{workspaceId}
var wsGroup = app.MapGroup("/api/workspaces/{workspaceId:guid}")
    .AddEndpointFilter<TenantResolutionEndpointFilter>();

wsGroup.MapUploadEndpoints();
wsGroup.MapAnalysisEndpoints();
wsGroup.MapCompareEndpoints();
wsGroup.MapTrendEndpoints();
wsGroup.MapCorrelationEndpoints();
wsGroup.MapAlertEndpoints();
wsGroup.MapWebhookEndpoints();
wsGroup.MapTokenEndpoints();

// Blazor Server UI
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests
public partial class Program { }
