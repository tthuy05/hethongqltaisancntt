using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItAssetManagement.IntegrationTests;

// Local HTTP wiring/policy tests with no database connection or real JWT grants.
public sealed class MaintenanceRolloutTests
{
    public sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var grants = Request.Headers.ContainsKey("X-Fixture-Read") ? new[] { Permissions.MaintenanceRead } : [];
            Context.Items[typeof(CurrentUser)] = new CurrentUser(1, "Fixture", "fixture@test.invalid", null, ["TECHNICAL_SUPPORT"], grants);
            var claims = new[] { new Claim("sub", "1") }.Concat(grants.Select(p => new Claim("permission", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private sealed class ReadOnlyRepository : IRepository
    {
        public IQueryable<T> Query<T>(bool tracking = false) where T : class => Array.Empty<T>().AsQueryable();
        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public void Add<T>(T entity) where T : class => throw new InvalidOperationException("Read-only fixture");
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new InvalidOperationException("Read-only fixture");
        public Task SaveAsync(CancellationToken ct) => throw new InvalidOperationException("Read-only fixture");
        public Task LockAsync(string resource, CancellationToken ct) => throw new InvalidOperationException("Read-only fixture");
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new InvalidOperationException("Read-only fixture");
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new InvalidOperationException("Read-only fixture");
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => q;
    }

    private static WebApplicationFactory<Program> Factory(string? enabled, bool fixtureAuth = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MaintenanceRollout.ConfigurationKey] = enabled, ["ConnectionStrings:DefaultConnection"] = "",
                ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false", ["Logging:LogLevel:Default"] = "None"
            }));
            if (fixtureAuth) builder.ConfigureServices(s =>
            {
                s.RemoveAll<IRepository>(); s.AddSingleton<IRepository, ReadOnlyRepository>();
                s.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, Handler>("fixture", _ => { });
            });
        });

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("invalid")]
    public async Task Disabled_routes_and_openapi_are_absent_without_touching_database(string? enabled)
    {
        await using var factory = Factory(enabled); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-fixture");
        foreach (var (method, path) in new[] { (HttpMethod.Get, "/api/v1/asset-maintenance"),
            (HttpMethod.Get, "/api/v1/asset-maintenance/1"), (HttpMethod.Post, "/api/v1/asset-maintenance"),
            (HttpMethod.Post, "/api/v1/asset-maintenance/1/complete") })
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent("not-json") };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        var descriptions = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items;
        Assert.DoesNotContain(descriptions.SelectMany(g => g.Items), d => d.RelativePath?.StartsWith("api/v1/asset-maintenance") == true);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
    }

    [Fact]
    public async Task Prepared_policies_exist_but_do_not_enter_active_grant_catalog()
    {
        await using var factory = Factory(null);
        var provider = factory.Services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();
        foreach (var permission in new[] { Permissions.MaintenanceRead, Permissions.MaintenanceCreate, Permissions.MaintenanceResolve })
        {
            Assert.NotNull(await provider.GetPolicyAsync(permission));
            Assert.DoesNotContain(permission, Permissions.All);
        }
        Assert.Equal(Permissions.MaintenanceResolve, Permissions.MaintenanceComplete);
    }

    [Fact]
    public async Task Synthetic_opt_in_uses_real_service_DI_and_returns_403_not_missing_policy_500()
    {
        await using var factory = Factory("true", true); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/asset-maintenance")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Fixture-Read", "1");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/asset-maintenance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsync("/api/v1/asset-maintenance/1/complete", new StringContent("not-json"))).StatusCode);
    }
}
