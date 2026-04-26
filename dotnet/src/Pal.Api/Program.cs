using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Pal.Api.Components;
using Pal.Api.Endpoints;
using Pal.Api.Services;
using Pal.Api.Worker;
using Pal.Application.Analysis;
using Pal.Application.Compare;
using Pal.Application.Persistence;
using Pal.Application.Correlation;
using Pal.Application.Trends;
using Pal.Application.Storage;
using Pal.Persistence;
using Pal.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Upload size limits: 512 MB
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 536_870_912);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 536_870_912);

// Postgres — DbContextFactory for Blazor Server + BackgroundService safety
builder.Services.AddDbContextFactory<PalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
           .UseSnakeCaseNamingConvention());

// Local disk storage
var storageRoot = Path.GetFullPath(
    builder.Configuration["Storage:LocalRoot"] ?? "data/storage");
builder.Services.AddSingleton<IStorageProvider>(_ => new LocalDiskStorageProvider(storageRoot));

// Repositories (singleton: stateless, use factory per call — safe for all lifetimes)
builder.Services.AddSingleton<IUploadRepository, UploadRepository>();
builder.Services.AddSingleton<IAnalysisRepository, AnalysisRepository>();
builder.Services.AddSingleton<IPackRepository, PackRepository>();
builder.Services.AddSingleton<ICompareRepository, CompareRepository>();
builder.Services.AddSingleton<CompareRunner>();
builder.Services.AddSingleton<TrendAnalyzer>();
builder.Services.AddSingleton<TrendService>();
builder.Services.AddSingleton<CorrelationAnalyzer>();
builder.Services.AddSingleton<CorrelationService>();

// Analysis runner and worker channel
builder.Services.AddSingleton<IAnalysisRunner, AnalysisRunner>();
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true }));
builder.Services.AddHostedService<AnalysisWorker>();

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
app.UseAntiforgery();

// REST API endpoints
app.MapHealthEndpoints();
app.MapPackEndpoints();
app.MapUploadEndpoints();
app.MapAnalysisEndpoints();
app.MapCompareEndpoints();
app.MapTrendEndpoints();
app.MapCorrelationEndpoints();

// Blazor Server UI
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests
public partial class Program { }
