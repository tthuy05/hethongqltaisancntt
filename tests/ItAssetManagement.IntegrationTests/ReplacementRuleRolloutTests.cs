using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

public sealed class ReplacementRuleRolloutTests
{
    private sealed class Ready(bool value) : IReplacementRuleReadiness
    {
        public int Calls;
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(value); }
    }
    private static WebApplicationFactory<Program> Factory(string? flag, Ready ready) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "", [ReplacementRuleRollout.Key] = flag, ["Frontend:Enabled"] = "false", ["Logging:LogLevel:Default"] = "None"
        }));
        builder.ConfigureServices(services => { services.RemoveAll<IReplacementRuleReadiness>(); services.AddSingleton<IReplacementRuleReadiness>(ready); });
    });
    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("invalid")]
    public async Task Disabled_rules_is_absent_from_discovery_and_never_probes_database(string? flag)
    {
        var ready = new Ready(false); await using var app = Factory(flag, ready); using var client = app.CreateClient();
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete }) {
            using var request = new HttpRequestMessage(method, "/api/v1/replacement-rules/1") { Content = new StringContent("invalid-json") };
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
        }
        Assert.Equal(0, ready.Calls);
        Assert.DoesNotContain(app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items.SelectMany(x => x.Items), x => x.RelativePath?.StartsWith("api/v1/software") == true);
    }
    [Fact]
    public async Task Missing_schema_or_grants_returns_503_before_auth_or_controller()
    {
        var ready = new Ready(false); await using var app = Factory("true", ready); using var client = app.CreateClient();
        using var response = await client.PostAsync("/api/v1/replacement-rules", new StringContent("invalid-json"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Equal("30", response.Headers.RetryAfter?.ToString());
        Assert.Equal("REPLACEMENT_RULE_NOT_READY", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()); Assert.Equal(1, ready.Calls);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
    [Fact]
    public async Task Ready_module_still_requires_authentication()
    {
        await using var app = Factory("true", new Ready(true)); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/replacement-rules")).StatusCode);
    }
}
