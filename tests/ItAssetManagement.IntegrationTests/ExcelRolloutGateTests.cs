using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class ExcelRolloutGateTests
{
    [LocalPostgresFact]
    public async Task Real_JWT_does_not_activate_prepared_excel_permission_even_with_isolated_database_grant()
    {
        // This is evidence of the CLOSED rollout gate, not successful Excel JWT acceptance.
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
            var provider = scope.ServiceProvider; var db = provider.GetRequiredService<AppDbContext>();
            await provider.GetRequiredService<DevelopmentSeed>().RunAsync(new(email, password));
            await provider.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                var permission = await db.Set<Permission>().SingleOrDefaultAsync(x => x.Code == Permissions.ImportAssets);
                if (permission == null) {
                    permission = new Permission { Code = Permissions.ImportAssets, Name = "Prepared Excel import", Module = "import" };
                    db.Add(permission); await db.SaveChangesAsync();
                    provider.GetRequiredService<IAuditWriter>().Record("test.fixture", permission, null, "SYSTEM");
                }
                var role = await db.Set<Role>().SingleAsync(x => x.Code == "ADMIN_IT");
                if (!await db.Set<RolePermission>().AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id)) {
                    var grant = new RolePermission { RoleId = role.Id, PermissionId = permission.Id, GrantedAtUtc = DateTime.UtcNow };
                    db.Add(grant); await db.SaveChangesAsync(); provider.GetRequiredService<IAuditWriter>().Record("test.fixture", grant, null, "SYSTEM");
                }
                await db.SaveChangesAsync(); return true;
            });
        }
        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Contains(Permissions.AssetRead, session.User.Permissions);
        Assert.DoesNotContain(Permissions.ImportAssets, session.User.Permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/assets")).StatusCode);
        await using var verify = factory.Services.CreateAsyncScope(); var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await verifyDb.Set<Asset>().CountAsync(); var audits = await verifyDb.Set<AuditLog>().CountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/import/assets/template")).StatusCode);
        foreach (var path in new[] { "/api/v1/assignments", "/api/v1/asset-maintenance", "/api/v1/reports/asset-inventory" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(count, await verifyDb.Set<Asset>().CountAsync()); Assert.Equal(audits, await verifyDb.Set<AuditLog>().CountAsync());
    }
}
