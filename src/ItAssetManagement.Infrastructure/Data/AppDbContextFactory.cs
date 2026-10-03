using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ItAssetManagement.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            // Offline model generation only. Deliberately no host/credential: cannot update a live DB.
            .UseNpgsql(options => options.MigrationsHistoryTable("ef_migrations_history", "public"))
            .Options);
}
