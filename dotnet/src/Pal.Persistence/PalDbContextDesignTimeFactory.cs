using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pal.Persistence;

public sealed class PalDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PalDbContext>
{
    public PalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PalDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pal;Username=pal;Password=paldev")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PalDbContext(options);
    }
}
