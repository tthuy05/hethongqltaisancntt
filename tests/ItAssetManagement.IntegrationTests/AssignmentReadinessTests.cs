using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace ItAssetManagement.IntegrationTests;

// Explicit opt-in READ ONLY checks, not MvpFixture: no user/asset fixture seed or DML.
public sealed class AssignmentReadinessTests
{
    [NeonFact]
    public async Task Existing_schema12_shared_and_seeded_isolated_are_ready_without_enabling_API_or_writes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configPath = Environment.GetEnvironmentVariable("ITAM_TEST_CONFIG_PATH") ?? Path.Combine(root,
            "src/ItAssetManagement.Api/appsettings.Development.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var secret = document.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        Assert.True(NeonConnectionPolicy.TryCreate(secret, out var settings));
        Assert.Equal("neondb", settings!.Database);
        var testDatabase = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE") ?? "it_asset_management_m1_verify_20261002";
        Assert.Matches("\\Ait_asset_management_(m1|full_schema)_verify_[a-z0-9_]{1,24}\\z",testDatabase);
        foreach (var (database, ready) in new[] { ("neondb", true), (testDatabase, true) })
        {
            settings.Database = database;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString }).Build();
            Assert.Equal(ready, await new AssignmentSchemaReadiness(configuration).IsReadyAsync(CancellationToken.None));
        }
    }
}
