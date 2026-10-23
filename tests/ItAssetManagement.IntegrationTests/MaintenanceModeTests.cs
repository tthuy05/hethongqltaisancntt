using System.Net;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Contracts.Database;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ItAssetManagement.IntegrationTests;

// Always offline: Development has no provider; Production uses synthetic settings.
// Any DbContext/probe/Assignment readiness access fails and is counted explicitly.
public sealed class MaintenanceModeTests
{
    private sealed class AccessGuard : IReadOnlyDatabaseProbe, IAssignmentSchemaReadiness
    {
        public int DatabaseCalls;
        public int ProbeCalls;
        public int ReadinessCalls;
        public int AuthenticationCalls;
        public Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref ProbeCalls); throw new InvalidOperationException("Maintenance must not probe a database."); }
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        { Interlocked.Increment(ref ReadinessCalls); throw new InvalidOperationException("Maintenance must not check workflow readiness."); }
    }

    private static IConfiguration Configuration(string? enabled) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { [MaintenanceMode.ConfigurationKey] = enabled }).Build();

    private static WebApplicationFactory<Program> Factory(string environment, string? enabled, AccessGuard guard,
        bool assignmentEnabled = false) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = environment == "Production"
                ? "Host=offline-fixture.neon.tech;Database=fixture_db;Username=fixture;Password=synthetic-not-used" : "",
            ["Jwt:SigningKey"] = "synthetic-maintenance-test-key-never-for-live-tokens-2026",
            ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false",
            ["Database:EnableReadinessProbe"] = "true",
            ["RENDER"] = "false",
            [AssignmentRollout.ConfigurationKey] = assignmentEnabled.ToString(),
            [MaintenanceMode.ConfigurationKey] = enabled,
            ["Logging:LogLevel:Default"] = "None"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.AddScoped<AppDbContext>(_ =>
            {
                Interlocked.Increment(ref guard.DatabaseCalls);
                throw new InvalidOperationException("Maintenance must not resolve a business database.");
            });
            services.RemoveAll<IReadOnlyDatabaseProbe>(); services.AddSingleton<IReadOnlyDatabaseProbe>(guard);
            services.RemoveAll<IAssignmentSchemaReadiness>(); services.AddSingleton<IAssignmentSchemaReadiness>(guard);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.Events.OnMessageReceived = _ =>
                { Interlocked.Increment(ref guard.AuthenticationCalls); return Task.CompletedTask; });
        });
    });

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(" false ", false)]
    [InlineData(" true ", true)]
    [InlineData(" ", true)]
    [InlineData("1", true)]
    [InlineData("must-not-echo-invalid-setting", true)]
    public void Configuration_is_off_by_default_and_fails_closed_on_nonempty_typos(string? configured, bool expected)
    {
        var configuration = Configuration(configured);
        Assert.Equal(expected, MaintenanceMode.IsEnabled(configuration));
        var version = DeploymentRevision.Create(configuration);
        Assert.Equal(expected, version.MaintenanceModeEnabled);
        Assert.DoesNotContain("must-not-echo", JsonSerializer.Serialize(version));
    }

    [Theory]
    [InlineData("GET", "/health/live", true)]
    [InlineData("HEAD", "/health/live", true)]
    [InlineData("GET", "/health/version", true)]
    [InlineData("HEAD", "/HEALTH/VERSION", true)]
    [InlineData("GET", "/health/live/", false)]
    [InlineData("GET", "/health/live/anything", false)]
    [InlineData("GET", "/health/live-extra", false)]
    [InlineData("GET", "/health/ready", false)]
    [InlineData("GET", "/health/version.json", false)]
    [InlineData("POST", "/health/live", false)]
    [InlineData("OPTIONS", "/health/version", false)]
    [InlineData("DELETE", "/health/version", false)]
    [InlineData("GET", "/api/v1/assets", false)]
    public void Allowlist_is_exact_and_read_only(string method, string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method; context.Request.Path = path;
        Assert.Equal(expected, MaintenanceMode.IsAllowed(context.Request));
    }

    [Theory]
    [InlineData("Development", "true", false)]
    [InlineData("Production", "true", false)]
    [InlineData("Development", "true", true)]
    [InlineData("Production", "must-not-echo-invalid-setting", true)]
    public async Task Enabled_gate_precedes_auth_binding_business_and_workflow_readiness(string environment, string enabled,
        bool assignmentEnabled)
    {
        var guard = new AccessGuard();
        await using var factory = Factory(environment, enabled, guard, assignmentEnabled);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-maintenance-test-token");
        foreach (var (method, target) in new[]
        {
            ("GET", "/api/v1/assets"), ("POST", "/api/v1/assets"), ("PUT", "/api/v1/assets/1"),
            ("PATCH", "/api/v1/assets/1/status"), ("DELETE", "/api/v1/assets/1"),
            ("POST", "/api/v1/auth/login"), ("POST", "/api/v1/auth/activate"), ("GET", "/api/v1/auth/me"),
            ("POST", "/api/v1/users"), ("PUT", "/api/v1/roles/1/permissions"),
            ("GET", "/api/v1/departments"), ("GET", "/api/v1/asset-types"), ("GET", "/api/v1/audit-logs"),
            ("GET", "/api/v1/asset-assignments"), ("POST", "/api/v1/asset-assignments/1/return"),
            ("GET", "/health/ready"), ("GET", "/swagger/"), ("GET", "/openapi/v1.json"),
            ("GET", "/"), ("GET", "/index.html"), ("GET", "/js/app.js"),
            ("GET", "/not-a-route?maintenance=false&bypass=true"),
            ("POST", "/health/live"), ("GET", "/health/live/"), ("GET", "/health/version-extra")
        })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), target);
            request.Headers.Add("X-Maintenance-Bypass", "true");
            request.Content = new StringContent("not-json", System.Text.Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("60", response.Headers.RetryAfter?.ToString());
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            var text = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(text);
            Assert.Equal("MAINTENANCE_MODE", document.RootElement.GetProperty("code").GetString());
            Assert.Equal(503, document.RootElement.GetProperty("status").GetInt32());
            Assert.True(document.RootElement.TryGetProperty("traceId", out _));
            Assert.DoesNotContain("synthetic-not-used", text);
            Assert.DoesNotContain("invalid-maintenance-test-token", text);
            Assert.DoesNotContain("must-not-echo-invalid-setting", text);
        }
        Assert.Equal(0, guard.AuthenticationCalls);
        AssertNoDatabaseAccess(guard);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Enabled_gate_keeps_only_liveness_and_version_available_without_database(string environment)
    {
        var guard = new AccessGuard();
        await using var factory = Factory(environment, "true", guard);
        using var client = factory.CreateClient();
        // Cryptographically valid bearer tokens must not turn health into a DB
        // account check while the database is deliberately unavailable/old-schema.
        var token = new JwtSecurityToken("it-asset-management", "it-asset-management-ui",
            [new Claim("sub", "1"), new Claim("token_version", "0")], DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5), new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("synthetic-maintenance-test-key-never-for-live-tokens-2026")), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        foreach (var target in new[] { "/health/live", "/health/version", "/HEALTH/VERSION?maintenance=false" })
        {
            using var response = await client.GetAsync(target);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            if (target.StartsWith("/health/version", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.True(document.RootElement.GetProperty("maintenanceModeEnabled").GetBoolean());
                Assert.False(document.RootElement.GetProperty("assignmentApiEnabled").GetBoolean());
            }
            using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, target));
            Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        }
        using var blockedHead = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blockedHead.StatusCode);
        Assert.Equal("", await blockedHead.Content.ReadAsStringAsync());
        Assert.Equal("60", blockedHead.Headers.RetryAfter?.ToString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/assets")).StatusCode);
        Assert.Equal(0, guard.AuthenticationCalls);
        AssertNoDatabaseAccess(guard);
    }

    [Theory]
    [InlineData("Development", null)]
    [InlineData("Development", "false")]
    [InlineData("Production", null)]
    [InlineData("Production", "false")]
    public async Task Off_mode_preserves_existing_health_auth_and_assignment_responses(string environment, string? enabled)
    {
        var guard = new AccessGuard();
        await using var factory = Factory(environment, enabled, guard);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        using var versionResponse = await client.GetAsync("/health/version");
        Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
        using var version = JsonDocument.Parse(await versionResponse.Content.ReadAsStringAsync());
        Assert.False(version.RootElement.GetProperty("maintenanceModeEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/asset-assignments")).StatusCode);
        Assert.True(guard.AuthenticationCalls > 0); // Auth is not disabled by an OFF gate.
        AssertNoDatabaseAccess(guard);
    }

    [Fact]
    public async Task Middleware_reads_current_configuration_without_claiming_to_drain_in_flight_requests()
    {
        var configuration = Configuration(null);
        var calls = 0;
        var middleware = new MaintenanceModeMiddleware(_ => { calls++; return Task.CompletedTask; }, configuration);
        var first = new DefaultHttpContext(); first.Request.Path = "/api/v1/assets";
        await middleware.InvokeAsync(first);
        configuration[MaintenanceMode.ConfigurationKey] = "true";
        var blocked = new DefaultHttpContext(); blocked.Request.Path = "/api/v1/assets";
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        blocked.RequestServices = services;
        blocked.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(blocked);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, blocked.Response.StatusCode);
        Assert.Equal(1, calls);
        configuration[MaintenanceMode.ConfigurationKey] = "false";
        await middleware.InvokeAsync(new DefaultHttpContext());
        Assert.Equal(2, calls);
    }

    private static void AssertNoDatabaseAccess(AccessGuard guard)
    {
        Assert.Equal(0, guard.DatabaseCalls);
        Assert.Equal(0, guard.ProbeCalls);
        Assert.Equal(0, guard.ReadinessCalls);
    }
}
