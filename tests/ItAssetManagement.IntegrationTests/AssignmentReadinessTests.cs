using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace ItAssetManagement.IntegrationTests;

// Explicit opt-in READ ONLY checks, not MvpFixture: no user/asset fixture seed or DML.
public sealed class AssignmentReadinessTests
{
    [NeonFact]
    public async Task Existing_schema10_shared_refuses_and_schema12_seeded_isolated_is_ready_without_writes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,
            "src/ItAssetManagement.Api/appsettings.Development.json")));
        var secret = document.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        Assert.True(NeonConnectionPolicy.TryCreate(secret, out var settings));
        Assert.Equal("neondb", settings!.Database);
        foreach (var (database, ready) in new[] { ("neondb", false), ("it_asset_management_m1_verify_20261002", true) })
        {
            settings.Database = database;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString }).Build();
            Assert.Equal(ready, await new AssignmentSchemaReadiness(configuration).IsReadyAsync(CancellationToken.None));
        }
    }
}
