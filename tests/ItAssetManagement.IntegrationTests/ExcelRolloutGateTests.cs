using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class ExcelRolloutGateTests
{
    private sealed class SyntheticAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.NoResult());

            var token = header["Bearer ".Length..].Trim();
            if (token == "invalid")
                return Task.FromResult(AuthenticateResult.Fail("Invalid token"));

            var claims = new List<Claim> { new("sub", "1") };
            if (token == "admin")
                claims.AddRange(Permissions.All.Select(p => new Claim("permission", p)));
            else if (token == "support")
                claims.AddRange(Permissions.Read.Select(p => new Claim("permission", p)));
            else if (token == "importer")
                claims.AddRange([new Claim("permission", Permissions.AssetRead), new Claim("permission", Permissions.ImportAssets)]);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private sealed class SyntheticActor(IHttpContextAccessor accessor) : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => accessor.HttpContext?.Request.Method;
        public string? Path => accessor.HttpContext?.Request.Path.Value;
        public bool Has(string permission) => accessor.HttpContext?.User.HasClaim("permission", permission) == true;
    }

    private sealed class SyntheticRepository : IRepository
    {
        public IQueryable<T> Query<T>(bool tracking = false) where T : class => Array.Empty<T>().AsQueryable();
        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => q;
        public void Add<T>(T entity) where T : class { }
        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task LockAssetAsync(long id, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => Task.FromResult(false);
    }

    private static WebApplicationFactory<Program> OfflineFactory(bool enabled) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "",
                ["Frontend:Enabled"] = "false",
                ["Swagger:Enabled"] = "false",
                ["Logging:LogLevel:Default"] = "None",
                ["Features:ImportExportApi:Enabled"] = enabled.ToString()
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRepository>();
                services.AddSingleton<IRepository, SyntheticRepository>();
                services.RemoveAll<IActor>();
                services.AddScoped<IActor, SyntheticActor>();
                services.AddAuthentication("synthetic").AddScheme<AuthenticationSchemeOptions, SyntheticAuthHandler>("synthetic", _ => { });
            });
        });

    [Fact]
    public async Task Gate_disabled_returns_404_for_all_import_and_export_routes()
    {
        await using var factory = OfflineFactory(false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");

        foreach (var path in new[] { "/api/v1/import/assets/template", "/api/v1/export/assets", "/api/v1/export/maintenance", "/api/v1/export/assignment-history", "/api/v1/export/warranty-expiration", "/api/v1/export/replacement-report" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Gate_enabled_enforces_401_for_unauthenticated_and_403_for_unauthorized()
    {
        await using var factory = OfflineFactory(true);
        using var client = factory.CreateClient();

        // 1. Unauthenticated -> 401
        var unauth = await client.GetAsync("/api/v1/import/assets/template");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // 2. Technical Support (lacks import and export grants) -> 403 Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "support");
        var supportImport = await client.GetAsync("/api/v1/import/assets/template");
        Assert.Equal(HttpStatusCode.Forbidden, supportImport.StatusCode);

        var supportExport = await client.GetAsync("/api/v1/export/assets");
        Assert.Equal(HttpStatusCode.Forbidden, supportExport.StatusCode);

        // 3. Importer (has import.assets.execute) -> 200 OK for import template, 403 for export
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "importer");
        var importerTemplate = await client.GetAsync("/api/v1/import/assets/template");
        Assert.Equal(HttpStatusCode.OK, importerTemplate.StatusCode);

        var importerExport = await client.GetAsync("/api/v1/export/assets");
        Assert.Equal(HttpStatusCode.Forbidden, importerExport.StatusCode);

        // 4. Admin (has all permissions) -> 200 OK
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        var adminTemplate = await client.GetAsync("/api/v1/import/assets/template");
        Assert.Equal(HttpStatusCode.OK, adminTemplate.StatusCode);

        var adminExport = await client.GetAsync("/api/v1/export/assets");
        Assert.Equal(HttpStatusCode.OK, adminExport.StatusCode);
    }

    [LocalPostgresFact]
    public async Task Real_JWT_activates_excel_permission_with_isolated_database_grant()
    {
        await using (var guarded = LocalSequenceAuthenticationTests.Database()) await guarded.Database.MigrateAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = "", ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false",
                ["Logging:LogLevel:Default"] = "None", ["Features:ImportExportApi:Enabled"] = "true",
                ["Features:AssignmentApi:Enabled"] = "false", ["Features:MaintenanceApi:Enabled"] = "false",
                ["Features:ReportingApi:Enabled"] = "false"
            }));
            builder.ConfigureServices(services => services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
                Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION"), p => p.MigrationsHistoryTable("ef_migrations_history", "public"))));
        });
        const string email = "excel-jwt@fixture.test";
        const string password = "Synthetic-test-password-2026!";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider;
            await provider.GetRequiredService<DevelopmentSeed>().RunAsync(new(email, password));
        }
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Contains(Permissions.AssetRead, session.User.Permissions);
        Assert.Contains(Permissions.ImportAssets, session.User.Permissions);
        Assert.Contains(Permissions.ExportAssets, session.User.Permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/import/assets/template")).StatusCode);
    }
}

