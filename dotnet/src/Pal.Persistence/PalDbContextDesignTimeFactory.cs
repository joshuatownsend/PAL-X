using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pal.Persistence;

/// <summary>
/// Allows `dotnet ef migrations add` to create a PalDbContext without the full DI container.
/// Only used at design time — never instantiated at runtime.
/// </summary>
public sealed class PalDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PalDbContext>
{
    public PalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PalDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pal;Username=pal;Password=paldev")
            .UseSnakeCaseNamingConvention()
            .Options;
        // No-op TenantContext: all query filters pass through (WorkspaceId is null)
        return new PalDbContext(options, new TenantContext());
    }
}
