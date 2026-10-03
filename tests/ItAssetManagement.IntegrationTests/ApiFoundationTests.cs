using System.Net;
using ItAssetManagement.Application.Contracts.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

public sealed class ApiFoundationTests
{
    private sealed class FixtureProbe(DatabaseProbeStatus status) : IReadOnlyDatabaseProbe
    {
        public int Calls { get; private set; }
        public Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new DatabaseProbeResult(status, "fixture_db", "fixture_version"));
        }
    }

    private static WebApplicationFactory<Program> Factory(string environment, FixtureProbe probe, bool enabled = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "",
                ["Jwt:SigningKey"] = "synthetic-test-key-not-used-outside-tests-2026",
                ["Frontend:Enabled"] = "false",
                ["Swagger:Enabled"] = "false",
                ["Database:EnableReadinessProbe"] = enabled.ToString()
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReadOnlyDatabaseProbe>();
                services.AddSingleton<IReadOnlyDatabaseProbe>(probe);
            });
        });

    [Fact]
    public async Task Liveness_is_200_without_contacting_a_database()
    {
        var probe = new FixtureProbe(DatabaseProbeStatus.NotConfigured);
        await using var factory = Factory("Development", probe);
        var response = await factory.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Alive", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Readiness_is_disabled_by_default_and_does_not_call_the_probe()
    {
        var probe = new FixtureProbe(DatabaseProbeStatus.Reachable);
        await using var factory = Factory("Development", probe);
        var response = await factory.CreateClient().GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("ProbeDisabled", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, probe.Calls);
    }

    [Theory]
    [InlineData(DatabaseProbeStatus.Reachable, HttpStatusCode.OK)]
    [InlineData(DatabaseProbeStatus.NotConfigured, HttpStatusCode.ServiceUnavailable)]
    [InlineData(DatabaseProbeStatus.InvalidConfiguration, HttpStatusCode.ServiceUnavailable)]
    [InlineData(DatabaseProbeStatus.ConnectionFailed, HttpStatusCode.ServiceUnavailable)]
    [InlineData(DatabaseProbeStatus.TlsNotVerified, HttpStatusCode.ServiceUnavailable)]
    public async Task Enabled_readiness_returns_only_a_safe_status(DatabaseProbeStatus status, HttpStatusCode expected)
    {
        var probe = new FixtureProbe(status);
        await using var factory = Factory("Development", probe, enabled: true);
        var response = await factory.CreateClient().GetAsync("/health/ready");
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("fixture_db", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task Development_exposes_openapi_and_protects_business_endpoints()
    {
        var probe = new FixtureProbe(DatabaseProbeStatus.NotConfigured);
        await using var factory = Factory("Development", probe);
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/health/live", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
        foreach (var path in new[] { "/index.html", "/js/mock/seed.js" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Production_does_not_expose_diagnostics_or_mock_assets()
    {
        var probe = new FixtureProbe(DatabaseProbeStatus.Reachable);
        await using var factory = Factory("Production", probe, enabled: true);
        var client = factory.CreateClient();
        foreach (var path in new[] { "/health/ready", "/openapi/v1.json", "/index.html", "/js/mock/seed.js", "/swagger/", "/swagger/init.js", "/swagger/swagger-ui-bundle.js" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Openapi_declares_bearer_only_for_protected_operations_and_swagger_can_be_disabled()
    {
        var probe = new FixtureProbe(DatabaseProbeStatus.NotConfigured);
        await using var factory = Factory("Development", probe);
        var client = factory.CreateClient();
        using var document = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.Equal(0, root.GetProperty("paths").GetProperty("/api/v1/auth/login").GetProperty("post").GetProperty("security").GetArrayLength());
        Assert.Equal(0, root.GetProperty("paths").GetProperty("/health/live").GetProperty("get").GetProperty("security").GetArrayLength());
        Assert.True(root.GetProperty("paths").GetProperty("/api/v1/auth/me").GetProperty("get").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/")).StatusCode);
        Assert.Equal(0, probe.Calls);
    }
}
