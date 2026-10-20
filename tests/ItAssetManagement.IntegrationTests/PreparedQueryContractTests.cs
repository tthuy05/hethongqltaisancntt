using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using ItAssetManagement.Application.Import;
using ItAssetManagement.Application.Export;

namespace ItAssetManagement.IntegrationTests;

// Synthetic HTTP model-binding/query contract, not live JWT/grant rollout proof.
public sealed class PreparedQueryContractTests
{
    public sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1"), new Claim("permission", Permissions.DashboardRead)], Scheme.Name)), Scheme.Name)));
    }
    private sealed class Actor : IActor
    {
        public long? UserId => 1; public Guid CorrelationId => Guid.NewGuid(); public string? Method => "GET"; public string? Path => "/synthetic";
        public bool Has(string permission) => true;
    }
    private sealed class Repository : IRepository
    {
        public IQueryable<T> Query<T>(bool tracking = false) where T : class => Array.Empty<T>().AsQueryable();
        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => q;
        public void Add<T>(T entity) where T : class => throw new InvalidOperationException("Read-only HTTP fixture");
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new InvalidOperationException("Read-only HTTP fixture");
        public Task LockAsync(string resource, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new NotSupportedException();
    }
    private static WebApplicationFactory<Program> Factory(bool enabled = true) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseEnvironment("Development");
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"]="", ["Frontend:Enabled"]="false", ["Swagger:Enabled"]="false",
            ["Features:ReportingApi:Enabled"]=enabled.ToString(), ["Features:ImportExportApi:Enabled"]=enabled.ToString()
        }));
        b.ConfigureServices(s => {
            s.RemoveAll<IRepository>(); s.AddSingleton<IRepository,Repository>(); s.RemoveAll<IActor>(); s.AddSingleton<IActor,Actor>();
            s.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions,Handler>("fixture", _ => {});
        });
    });
    [Theory]
    [InlineData("/api/v1/dashboard/summary")]
    [InlineData("/api/v1/reports/asset-inventory")]
    [InlineData("/api/v1/export/assets")]
    public async Task Prepared_endpoints_are_absent_until_explicit_rollout(string path)
    {
        await using var factory = Factory(false); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Import_post_is_absent_before_rollout_even_for_authenticated_actor()
    {
        await using var factory = Factory(false); using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1]), "file", "fixture.xlsx");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/import/assets", form)).StatusCode);
    }
    [Theory]
    [InlineData("dashboard/costs", "assetCount", "missingPurchaseCostCount", "maintenanceTicketCountYtd", "missingMaintenanceCostCountYtd")]
    [InlineData("reports/maintenance-cost", "ticketCount", "missingCostCount", "ticketCount", "missingCostCount")]
    public async Task Financial_completeness_fields_are_serialized_on_empty_dataset(string path, string first, string second, string third, string fourth)
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/" + path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var field in new[] { first, second, third, fourth }) Assert.Equal(0, json.RootElement.GetProperty(field).GetInt32());
    }
    [Fact]
    public async Task Multipart_preview_accepts_dry_run_query_and_has_no_write_side_effect()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var bytes = SafeExcelWorkbookWriter.CreateWorkbook(["Name", "AssetTypeId", "DepartmentId"], new object?[][] { ["Synthetic", 1, 1] });
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes); file.Headers.ContentType = new(ImportSecurityLimits.AllowedMimeType);
        form.Add(file, "file", "fixture.xlsx");
        using var response = await client.PostAsync("/api/v1/import/assets?dryRun=true", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AssetImportResult>())!;
        Assert.True(result.DryRun); Assert.False(result.Committed); Assert.NotEmpty(result.Errors);
    }
    [Theory]
    [InlineData("/dashboard/summary?departmentId=1")]
    [InlineData("/dashboard/assets?groupBy=type&departmentId=1")]
    [InlineData("/dashboard/alerts?daysAhead=30&departmentId=1")]
    [InlineData("/dashboard/costs?fromUtc=2026-01-01T00:00:00Z&departmentId=1")]
    [InlineData("/reports/asset-inventory?departmentId=1&assetTypeId=1&purchasedFrom=2026-01-01&includeArchived=false")]
    [InlineData("/reports/assets-by-department?assetTypeId=1")]
    [InlineData("/reports/assets-by-type?departmentId=1")]
    [InlineData("/reports/asset-status?departmentId=1")]
    [InlineData("/reports/assignment-history?assetId=1&userId=1&fromUtc=2026-01-01T00:00:00Z")]
    [InlineData("/reports/maintenance-history?technicianId=1&includeArchived=false")]
    [InlineData("/reports/maintenance-cost?groupBy=department&departmentId=1")]
    [InlineData("/reports/warranty-expiration?fromDate=2026-01-01&assetTypeId=1")]
    [InlineData("/reports/license-expiration?softwareId=1&fromUtc=2026-01-01T00:00:00Z")]
    [InlineData("/reports/replacement-recommendations?plannedYear=2027&generatedFromUtc=2026-01-01T00:00:00Z")]
    [InlineData("/reports/replacement-budget?year=2027&departmentId=1")]
    [InlineData("/export/assets?departmentId=1&assetTypeId=1")]
    [InlineData("/export/maintenance?assetId=1&technicianId=1")]
    [InlineData("/export/assignment-history?userId=1&departmentId=1")]
    [InlineData("/export/warranty-expiration?departmentId=1")]
    [InlineData("/export/replacement-report?year=2027&departmentId=1")]
    public async Task Supported_query_fields_reach_prepared_endpoint(string path)
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1" + path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Theory]
    [InlineData("/reports/asset-inventory?departmentId=1&departmentId=2")]
    [InlineData("/dashboard/assets?groupBy=type&unexpected=1")]
    [InlineData("/reports/asset-inventory?page=2147483647&pageSize=100")]
    [InlineData("/reports/asset-inventory?sortBy=unknown")]
    [InlineData("/reports/maintenance-cost?groupBy=")]
    [InlineData("/reports/maintenance-cost?fromUtc=2026-01-01")]
    public async Task Duplicate_unknown_or_invalid_query_is_400(string path)
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1" + path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_assets_accepts_dryRun_query_and_rejects_unknown_query()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        using var unknownForm = new MultipartFormDataContent();
        unknownForm.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "test.xlsx");
        using var unknown = await client.PostAsync("/api/v1/import/assets?unexpected=1", unknownForm);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var unknownProblem = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(unknown.Content);
        Assert.Equal("VALIDATION_ERROR", unknownProblem?.Title);

        using var dryRunForm = new MultipartFormDataContent();
        dryRunForm.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "test.xlsx");
        using var dryRun = await client.PostAsync("/api/v1/import/assets?dryRun=true", dryRunForm);
        Assert.Equal(HttpStatusCode.BadRequest, dryRun.StatusCode);
        var dryRunProblem = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(dryRun.Content);
        Assert.Equal("EMPTY_FILE", dryRunProblem?.Title);
    }
}
