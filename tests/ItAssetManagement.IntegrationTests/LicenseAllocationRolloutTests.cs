using System.Net;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Contracts.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

public sealed class LicenseAllocationRolloutTests
{
    private sealed class Ready : ILicenseReadiness, ILicenseAllocationReadiness {
        public int Calls;
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(true); }
    }
    private static WebApplicationFactory<Program> Factory(bool license, string? allocation, Ready? probe = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
        builder.UseEnvironment("Development"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"] = "", [LicenseRollout.Key] = license.ToString(), [LicenseRollout.AllocationKey] = allocation, ["LicenseKeyEncryption:ActiveVersion"] = "", ["Frontend:Enabled"] = "false", ["Logging:LogLevel:Default"] = "None"
        }));
        if (probe != null) builder.ConfigureServices(services => {
            services.RemoveAll<ILicenseReadiness>(); services.AddSingleton<ILicenseReadiness>(probe);
            services.RemoveAll<ILicenseAllocationReadiness>(); services.AddSingleton<ILicenseAllocationReadiness>(probe);
        });
    });
    [Theory]
    [InlineData(false, "true")] [InlineData(true, null)] [InlineData(true, "false")] [InlineData(true, "invalid")]
    public async Task Closed_allocation_is_404_without_probing_even_when_CRUD_is_enabled(bool license, string? allocation)
    {
        var probe = new Ready(); await using var app = Factory(license, allocation, probe); using var client = app.CreateClient();
        foreach (var path in new[] { "/api/v1/license-assignments", "/api/v1/license-assignments/1/revoke", "/api/v1/software-licenses/1/assignments" }) Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, probe.Calls); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
    [Fact]
    public async Task Closed_allocation_does_not_consume_CRUD_ingress_budget()
    {
        var probe = new Ready(); await using var app = Factory(true, "false", probe); using var client = app.CreateClient();
        for (var i = 0; i <= WorkflowIngress.RequestsPerMinute; i++) Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/license-assignments")).StatusCode);
        Assert.Equal(0, probe.Calls);
    }
    [Fact]
    public async Task Enabled_without_configuration_fails_closed_and_never_uses_a_fixed_AES_key()
    {
        await using var app = Factory(true, "true"); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/license-assignments")).StatusCode);
        Assert.Throws<InvalidOperationException>(() => app.Services.GetRequiredService<ILicenseKeyProtector>());
    }
    [Fact]
    public async Task Ready_allocation_still_requires_authentication()
    {
        await using var app = Factory(true, "true", new Ready()); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/license-assignments")).StatusCode);
    }
}
