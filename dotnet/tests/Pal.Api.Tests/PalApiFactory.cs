using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Pal.Api.Tests;

public sealed class PalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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
        Directory.Delete(_storageRoot, recursive: true);
        Directory.Delete(_packsRoot, recursive: true);
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
    }
}
