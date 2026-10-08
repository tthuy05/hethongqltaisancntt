using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

// Safe with either the Stage A checkout (no Assignment Controller) or the full WIP.
// Every connection string is synthetic/empty; no cloud or business writes here.
public sealed class AssignmentRolloutTests
{
    private sealed class Readiness(bool ready, Action? beforeResult = null) : IAssignmentSchemaReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken) { Calls++; beforeResult?.Invoke(); return Task.FromResult(ready); }
    }
    private static WebApplicationFactory<Program> Factory(string environment, string? enabled, Readiness readiness) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = environment == "Production"
                    ? "Host=ep-fixture.neon.tech;Database=fixture_db;Username=fixture;Password=synthetic-not-used" : "",
                ["Jwt:SigningKey"] = "synthetic-rollout-tests-signing-key-not-for-deployment-2026",
                ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false",
                [AssignmentRollout.ConfigurationKey] = enabled,
                ["Logging:LogLevel:Default"] = "None"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAssignmentSchemaReadiness>();
                services.AddSingleton<IAssignmentSchemaReadiness>(readiness);
            });
        });

    [Theory]
    [InlineData("Development", null)]
    [InlineData("Development", "false")]
    [InlineData("Production", null)]
    [InlineData("Production", "false")]
    [InlineData("Production", "invalid")]
    public async Task Disabled_routes_are_404_before_auth_model_binding_or_database(string environment, string? enabled)
    {
        var readiness = new Readiness(false);
        await using var factory = Factory(environment, enabled, readiness);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-invalid-token");
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/api/v1/asset-assignments"), (HttpMethod.Get, "/api/v1/asset-assignments/1"),
            (HttpMethod.Post, "/api/v1/asset-assignments"), (HttpMethod.Post, "/api/v1/asset-assignments/1/return")
        })
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent("not-json") };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        Assert.Equal(0, readiness.Calls);
        var descriptions = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items;
        Assert.DoesNotContain(descriptions.SelectMany(group => group.Items), description =>
            description.RelativePath?.StartsWith("api/v1/asset-assignments", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Enabled_but_unready_is_503_before_Controller_execution_instead_of_500(string environment)
    {
        var readiness = new Readiness(false);
        await using var factory = Factory(environment, "true", readiness);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-invalid-token");
        foreach (var path in new[] { "/api/v1/asset-assignments", "/api/v1/asset-assignments/1/return" })
        {
            using var response = await client.PostAsync(path, new StringContent("not-json"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("30", response.Headers.RetryAfter?.ToString());
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("ASSIGNMENT_NOT_READY", error.GetProperty("code").GetString());
            Assert.True(error.TryGetProperty("traceId", out _));
            var body = error.ToString();
            Assert.DoesNotContain("ep-fixture", body); Assert.DoesNotContain("synthetic-not-used", body);
        }
        Assert.Equal(2, readiness.Calls);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public async Task Rollout_does_not_change_legacy_health_or_auth_policies(string enabled)
    {
        var readiness = new Readiness(false);
        await using var factory = Factory("Development", enabled, readiness);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/version")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/asset-assignments-extra")).StatusCode); // Only exact path segments are gated.
        Assert.Equal(0, readiness.Calls);
    }

    [Fact]
    public async Task Runtime_disable_closes_existing_middleware_without_contacting_readiness()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { [AssignmentRollout.ConfigurationKey] = "true" }).Build();
        var readiness = new Readiness(true);
        using var services = new ServiceCollection().AddSingleton<IAssignmentSchemaReadiness>(readiness).BuildServiceProvider();
        var called = false;
        var middleware = new AssignmentRolloutMiddleware(_ => { called = true; return Task.CompletedTask; }, configuration);
        configuration[AssignmentRollout.ConfigurationKey] = "false";
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/v1/asset-assignments";
        await middleware.InvokeAsync(context);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(0, readiness.Calls); Assert.False(called);
        Assert.False(DeploymentRevision.Create(configuration).AssignmentApiEnabled);
    }

    [Fact]
    public async Task Disable_during_readiness_is_rechecked_before_business_execution()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { [AssignmentRollout.ConfigurationKey] = "true" }).Build();
        var readiness = new Readiness(true, () => configuration[AssignmentRollout.ConfigurationKey] = "false");
        using var services = new ServiceCollection().AddSingleton<IAssignmentSchemaReadiness>(readiness).BuildServiceProvider();
        var called = false;
        var middleware = new AssignmentRolloutMiddleware(_ => { called = true; return Task.CompletedTask; }, configuration);
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/v1/asset-assignments/1/return";
        await middleware.InvokeAsync(context);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(1, readiness.Calls); Assert.False(called);
    }

    [Fact]
    public async Task Missing_secret_refuses_readiness_without_a_connection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "" }).Build();
        Assert.False(await new AssignmentSchemaReadiness(configuration).IsReadyAsync(CancellationToken.None));
    }
}
