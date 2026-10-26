using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalAssignmentCompletionTests
{
    private const string Email = "excel-jwt@fixture.test";
    private const string Password = "Synthetic-test-password-2026!";
    private const string Path = "/api/v1/asset-assignments";
    private sealed class Ready : IAssignmentSchemaReadiness, IMaintenanceSchemaReadiness, ISoftwareReadiness
    { public Task<bool> IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(true); }
    private sealed class FailingAudit(IAuditWriter inner) : IAuditWriter
    {
        private int count;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null)
        {
            inner.Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
            if (action == "assignments.transfer" && ++count == 2) throw new InvalidOperationException("Synthetic transfer audit failure");
        }
    }
    internal static WebApplicationFactory<Program> Factory(bool failingAudit = false, bool frontend = false, int? port = null, bool maintenance = false, bool software = false)
    {
        var factory = new WebApplicationFactory<Program>();
        if (port.HasValue) factory.UseKestrel(port.Value);
        var configured = factory.WithWebHostBuilder(builder => {
            if (port.HasValue) builder.UseContentRoot(System.IO.Path.GetFullPath("src/ItAssetManagement.Api"));
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = "", ["Frontend:Enabled"] = frontend.ToString(), ["Swagger:Enabled"] = "false",
                ["Logging:LogLevel:Default"] = "None", ["Features:AssignmentApi:Enabled"] = "true",
                ["Features:MaintenanceApi:Enabled"] = maintenance.ToString(), ["Features:SoftwareApi:Enabled"] = software.ToString(), ["Features:ReportingApi:Enabled"] = "false", ["Features:ImportExportApi:Enabled"] = "true"
            }));
            builder.ConfigureServices(services => {
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION"),
                    provider => provider.MigrationsHistoryTable("ef_migrations_history", "public")));
                services.RemoveAll<IAssignmentSchemaReadiness>(); services.AddSingleton<IAssignmentSchemaReadiness, Ready>();
                services.RemoveAll<IMaintenanceSchemaReadiness>(); services.AddSingleton<IMaintenanceSchemaReadiness, Ready>();
                services.RemoveAll<ISoftwareReadiness>(); services.AddSingleton<ISoftwareReadiness, Ready>();
                if (failingAudit) { services.RemoveAll<IAuditWriter>(); services.AddScoped<IAuditWriter>(sp => new FailingAudit(new AuditWriter(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IActor>()))); }
            });
        });
        if (port.HasValue) configured.UseKestrel(port.Value);
        return configured;
    }

    internal static async Task SeedAsync(WebApplicationFactory<Program> factory)
    {
        await using var guard = LocalSequenceAuthenticationTests.Database(); await guard.Database.MigrateAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>(); var seed = services.GetRequiredService<DevelopmentSeed>();
        await seed.RunAssignmentPermissionsAsync(); // Historical B remains unchanged.
        var existed = await db.Set<Permission>().AnyAsync(x => x.Code == Permissions.AssignmentTransfer);
        Assert.Equal(existed ? 0 : 3, await seed.RunAssignmentTransferPermissionAsync());
        Assert.Equal(0, await seed.RunAssignmentTransferPermissionAsync());
        await seed.RunAsync(new(Email, Password));
        Assert.Equal(2, await db.Set<RolePermission>().CountAsync(x => x.PermissionId == db.Set<Permission>().Single(p => p.Code == Permissions.AssignmentTransfer).Id));
    }
    internal static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email = Email)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }
    internal static async Task<(long Department, long Type, long Admin)> References(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return ((await db.Set<Department>().FirstAsync(x => x.IsActive)).Id, (await db.Set<AssetType>().FirstAsync(x => x.IsActive)).Id,
            (await db.Set<User>().SingleAsync(x => x.Email == Email)).Id);
    }
    internal static async Task<AssetDto> AssetAsync(HttpClient client, long department, long type)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/assets", new AssetRequest { Name = "Assignment isolated " + Guid.NewGuid(), OwningDepartmentId = department, AssetTypeId = type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }
    internal static async Task<(long Id, string Email)> ActorAsync(WebApplicationFactory<Program> factory, string roleCode, params string[] permissions)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12]; var email = suffix + "@fixture.test";
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>(); var audit = sp.GetRequiredService<IAuditWriter>();
        var id = await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
            await sp.GetRequiredService<IRepository>().LockAsync("users-identity", default);
            var user = new User { Username = suffix, NormalizedUsername = suffix.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(),
                FullName = "Workflow fixture", HasCustomPermissions = permissions.Length > 0,
                EmployeeCode = await sp.GetRequiredService<IEmployeeCodeGenerator>().NextAsync(default) };
            user.PasswordHash = sp.GetRequiredService<IPasswordService>().Hash(user, Password);
            db.Add(user); await db.SaveChangesAsync(); audit.Record("test.fixture", user, null, "SYSTEM");
            var role = await db.Set<Role>().SingleAsync(x => x.Code == roleCode);
            var link = new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow };
            db.Add(link); audit.Record("test.fixture", link, null, "SYSTEM");
            foreach (var code in permissions) {
                var definition = await db.Set<Permission>().SingleAsync(x => x.Code == code);
                var grant = new UserPermission { UserId = user.Id, PermissionId = definition.Id };
                db.Add(grant); audit.Record("test.fixture", grant, null, "SYSTEM");
            }
            await db.SaveChangesAsync(); return user.Id;
        });
        return (id, email);
    }
    internal static async Task<AssignmentDto> AssignAsync(HttpClient client, long asset, long user)
    {
        using var response = await client.PostAsJsonAsync(Path, new AssignAssetRequest { AssetId = asset, UserId = user,
            AssignedAtUtc = DateTime.UtcNow.AddMinutes(-2), ExpectedReturnAtUtc = DateTime.UtcNow.AddDays(7), Note = "Handover" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssignmentDto>())!;
    }

    [LocalPostgresFact]
    public async Task Real_JWT_assign_transfer_history_and_return_preserve_versions_labels_and_cost_masking()
    {
        await using var factory = Factory(); await SeedAsync(factory); using var client = await LoginAsync(factory);
        var refs = await References(factory); var asset = await AssetAsync(client, refs.Department, refs.Type);
        var first = await AssignAsync(client, asset.Id, refs.Admin);
        Assert.Equal(asset.AssetCode, first.AssetCode); Assert.NotNull(first.ExpectedReturnAtUtc);
        using var response = await client.PostAsJsonAsync(Path + $"/{first.Id}/transfer", new TransferAssetRequest {
            DepartmentId = refs.Department, RowVersion = first.RowVersion, Note = "Transfer fixture", ReturnCondition = "Still functional" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transfer = (await response.Content.ReadFromJsonAsync<AssignmentTransferDto>())!;
        Assert.NotEqual(first.RowVersion, transfer.Previous.RowVersion);
        Assert.Equal(transfer.Previous.ReturnedAtUtc, transfer.Current.AssignedAtUtc);
        Assert.Equal("InUse", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
        var history = await client.GetFromJsonAsync<PagedResponse<AssignmentDto>>($"/api/v1/assets/{asset.Id}/assignments?status=Returned&sortBy=id&sortDirection=asc");
        Assert.Equal(first.Id, Assert.Single(history!.Items).Id);
        using var historyResponse = await client.GetAsync(Path + $"?assetId={asset.Id}&status=Active&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        using var json = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("items")[0];
        Assert.False(item.TryGetProperty("purchaseCost", out _)); Assert.False(item.TryGetProperty("email", out _));
        using var stale = await client.PostAsJsonAsync(Path + $"/{first.Id}/return", new ReturnAssetRequest { RowVersion = first.RowVersion, Note = "Stale return" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var returned = await client.PostAsJsonAsync(Path + $"/{transfer.Current.Id}/return", new ReturnAssetRequest {
            RowVersion = transfer.Current.RowVersion, Note = "Returned", Condition = "Still functional" });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Contains("Still functional", (await returned.Content.ReadFromJsonAsync<AssignmentDto>())!.ReturnNote);
        Assert.Equal("InStock", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Path + "?status=Unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Path + "?sortBy=purchaseCost")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/assets/9223372036854775807/assignments")).StatusCode);
    }

    [LocalPostgresFact]
    public async Task Real_JWT_support_and_custom_manager_cannot_inherit_transfer_or_mutate_without_explicit_grants()
    {
        await using var factory = Factory(); await SeedAsync(factory); using var admin = await LoginAsync(factory);
        var refs = await References(factory); var asset = await AssetAsync(admin, refs.Department, refs.Type);
        var assignment = await AssignAsync(admin, asset.Id, refs.Admin);
        foreach (var roleCode in new[] { "TECHNICAL_SUPPORT", "SYSTEM_MANAGER" })
        {
            var suffix = Guid.NewGuid().ToString("N")[..12]; var email = suffix + "@fixture.test";
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>();
                var audit = services.GetRequiredService<IAuditWriter>();
                await services.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                    var user = new User { Username = suffix, NormalizedUsername = suffix.ToUpperInvariant(),
                        Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = "Isolated permission fixture",
                        HasCustomPermissions = roleCode == "SYSTEM_MANAGER", EmployeeCode = await services.GetRequiredService<IEmployeeCodeGenerator>().NextAsync(default) };
                    user.PasswordHash = services.GetRequiredService<IPasswordService>().Hash(user, Password);
                    db.Add(user); await db.SaveChangesAsync();
                    var role = await db.Set<Role>().SingleAsync(x => x.Code == roleCode);
                    var membership = new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow };
                    db.Add(membership); audit.Record("test.fixture", user, null, "SYSTEM"); audit.Record("test.fixture", membership, null, "SYSTEM");
                    if (user.HasCustomPermissions)
                        foreach (var code in new[] { Permissions.AssetRead, Permissions.AssignmentRead })
                        {
                            var permission = await db.Set<Permission>().SingleAsync(x => x.Code == code);
                            var grant = new UserPermission { UserId = user.Id, PermissionId = permission.Id };
                            db.Add(grant); audit.Record("test.fixture", grant, null, "SYSTEM");
                        }
                    await db.SaveChangesAsync(); return true;
                });
            }
            using var restricted = await LoginAsync(factory, email);
            var current = (await restricted.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
            Assert.DoesNotContain(Permissions.AssignmentTransfer, current.Permissions);
            Assert.Equal(roleCode == "SYSTEM_MANAGER" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await restricted.GetAsync(Path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await restricted.PostAsJsonAsync(Path + $"/{assignment.Id}/transfer", new TransferAssetRequest {
                DepartmentId = refs.Department, RowVersion = assignment.RowVersion, Note = "Denied transfer" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await restricted.PostAsJsonAsync(Path + $"/{assignment.Id}/return", new ReturnAssetRequest {
                RowVersion = assignment.RowVersion, Note = "Denied return" })).StatusCode);
        }
        var unchanged = (await admin.GetFromJsonAsync<AssignmentDto>(Path + $"/{assignment.Id}"))!;
        Assert.Null(unchanged.ReturnedAtUtc); Assert.Equal(assignment.RowVersion, unchanged.RowVersion);
    }

    [LocalPostgresFact]
    public async Task Concurrent_transfers_commit_once_and_audit_failure_rolls_back_old_new_rows_and_versions()
    {
        await using var factory = Factory(); await SeedAsync(factory); using var client = await LoginAsync(factory);
        var refs = await References(factory); var asset = await AssetAsync(client, refs.Department, refs.Type);
        var first = await AssignAsync(client, asset.Id, refs.Admin);
        var request = new TransferAssetRequest { DepartmentId = refs.Department, RowVersion = first.RowVersion, Note = "Concurrent transfer" };
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Path + $"/{first.Id}/transfer", request), client.PostAsJsonAsync(Path + $"/{first.Id}/transfer", request));
        try { Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in responses) response.Dispose(); }
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(2, await db.Set<AssetAssignment>().CountAsync(x => x.AssetId == asset.Id));
            Assert.Equal(1, await db.Set<AssetAssignment>().CountAsync(x => x.AssetId == asset.Id && x.ReturnedAtUtc == null));
            Assert.Equal(2, await db.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == asset.Id)); // create + assign, no fake transfer transition.
        }
        var secondAsset = await AssetAsync(client, refs.Department, refs.Type); var initial = await AssignAsync(client, secondAsset.Id, refs.Admin);
        await using var failing = Factory(true); using var failingClient = await LoginAsync(failing);
        long auditBefore;
        await using (var scope = factory.Services.CreateAsyncScope()) auditBefore = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AuditLog>().LongCountAsync();
        using var error = await failingClient.PostAsJsonAsync(Path + $"/{initial.Id}/transfer", new TransferAssetRequest { DepartmentId = refs.Department, RowVersion = initial.RowVersion, Note = "Should rollback" });
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unchanged = await db.Set<AssetAssignment>().AsNoTracking().SingleAsync(x => x.Id == initial.Id);
            Assert.Null(unchanged.ReturnedAtUtc); Assert.Equal(initial.RowVersion, Convert.ToBase64String(unchanged.RowVersion));
            Assert.Equal(1, await db.Set<AssetAssignment>().CountAsync(x => x.AssetId == secondAsset.Id));
            Assert.Equal(auditBefore, await db.Set<AuditLog>().LongCountAsync());
        }
    }
}
