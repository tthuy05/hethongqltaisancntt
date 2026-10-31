using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Contracts.Security;
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
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalReportingRolloutTests : IAsyncLifetime
{
    private const string AdminEmail="reports-admin@fixture.test", ManagerEmail="reports-manager@fixture.test", SupportEmail="reports-support@fixture.test";
    private const string Password="Synthetic-test-password-2026!";
    private string? previous;
    private WebApplicationFactory<Program>? app;
    private long managerId, managerRoleId, departmentId;
    private short year;
    private static string ApiContentRoot()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(!File.Exists(Path.Combine(root.FullName,"ItAssetManagement.slnx")))root=root.Parent??throw new InvalidOperationException("Repository root missing.");
        return Path.Combine(root.FullName,"src","ItAssetManagement.Api");
    }
    private sealed class FixtureActor : IActor {
        public long? UserId=>null; public bool Has(string code)=>false;public Guid CorrelationId{get;}=Guid.NewGuid();public string? Method=>null;public string? Path=>null;
    }
    private sealed class FailingPermissionAudit(IAuditWriter inner) : IAuditWriter {
        public void Record(string action,object? entity,long? actorId,string actorType,string outcome="SUCCESS",object? before=null,object? after=null,string? failureCode=null) {
            inner.Record(action,entity,actorId,actorType,outcome,before,after,failureCode);
            if(entity is UserPermission && action=="users.permissions.replace")throw new InvalidOperationException("Synthetic permission audit failure");
        }
    }
    private sealed class LocalReadiness : IReportingReadiness {
        public async Task<bool> IsReadyAsync(CancellationToken ct) {
            await using var db=LocalSequenceAuthenticationTests.Database();
            await db.Database.OpenConnectionAsync(ct);
            return await ReportingReadiness.ProbeAsync((NpgsqlConnection)db.Database.GetDbConnection(),ct);
        }
    }
    public async Task InitializeAsync()
    {
        previous=Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION");if(string.IsNullOrEmpty(previous))return;
        await using var guard=LocalSequenceAuthenticationTests.Database();
        var settings=new NpgsqlConnectionStringBuilder(previous);settings.Database="postgres";
        var database="itam_local_verify_reports_"+Guid.NewGuid().ToString("N");
        await using(var c=new NpgsqlConnection(settings.ConnectionString)) {
            await c.OpenAsync();await using var create=new NpgsqlCommand($"CREATE DATABASE \"{database}\"",c);await create.ExecuteNonQueryAsync();
        }
        settings.Database=database;Environment.SetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION",settings.ConnectionString);
        BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        await using(var db=LocalSequenceAuthenticationTests.Database())await db.Database.MigrateAsync();
        app=LocalAssignmentCompletionTests.Factory().WithWebHostBuilder(builder=> {
            builder.UseContentRoot(ApiContentRoot());
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{[ReportingRollout.Key]="true"}));
            builder.ConfigureServices(services=>{services.RemoveAll<IReportingReadiness>();services.AddScoped<IReportingReadiness,LocalReadiness>();});
        });
        await SeedBaselineAsync();
    }
    public async Task DisposeAsync() {
        if(app!=null)await app.DisposeAsync();
        Environment.SetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION",previous);BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        // Retain each unique fixture database; never overwrite/drop another target.
    }
    private async Task SeedBaselineAsync()
    {
        await using var scope=app!.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit=new AuditWriter(db,new FixtureActor());var now=DateTime.UtcNow;year=checked((short)(now.Year+1));
        await new AuditedUnitOfWork(db).RunAsync(async()=> {
            async Task Add(params object[] rows) {db.AddRange(rows);await db.SaveChangesAsync();foreach(var row in rows)audit.Record("test.fixture",row,null,"SYSTEM");await db.SaveChangesAsync();}
            var roles=Permissions.Roles.Select(code=>new Role{Code=code,Name=code,IsSystem=true}).ToArray();
            var definitions=Permissions.All.Except(Permissions.Reports).Select(code=>new Permission{Code=code,Name=code,Module=code.Split('.')[0]}).ToArray();
            var department=new Department{Code="RPT",Name="Report fixture IT"};var type=new AssetType{Code="RPT",Name="Report fixture laptop"};
            await Add([..roles,..definitions,department,type]);departmentId=department.Id;managerRoleId=roles.Single(x=>x.Code=="SYSTEM_MANAGER").Id;
            var passwords=scope.ServiceProvider.GetRequiredService<IPasswordService>();
            var users=new[]{AdminEmail,ManagerEmail,SupportEmail}.Select((email,index)=>{
                var user=new User{Email=email,NormalizedEmail=email.ToUpperInvariant(),Username="report-user-"+index,NormalizedUsername="REPORT-USER-"+index,FullName="Report fixture "+index,DepartmentId=department.Id};
                user.PasswordHash=passwords.Hash(user,Password);return user;
            }).ToArray();await Add(users.Cast<object>().ToArray());managerId=users[1].Id;
            var links=new List<object>();
            for(var i=0;i<roles.Length;i++) {
                links.Add(new UserRole{UserId=users[i].Id,RoleId=roles[i].Id,AssignedAtUtc=now});
                var codes=i==0?Permissions.All.Except(Permissions.Reports):i==1?Permissions.Read.Concat(Permissions.Operate).Except(Permissions.Reports):Permissions.Read.Concat(Permissions.MaintenanceSupport);
                links.AddRange(definitions.Where(p=>codes.Contains(p.Code)).Select(p=>new RolePermission{RoleId=roles[i].Id,PermissionId=p.Id,GrantedAtUtc=now}));
            }
            await Add(links.ToArray());
            var assets=new[]{new Asset{AssetCode="RPT-001",Name="Known-cost report asset",AssetTypeId=type.Id,OwningDepartmentId=department.Id,PurchaseCost=123.45m,PurchaseDate=DateOnly.FromDateTime(now).AddYears(-2),WarrantyEndDate=DateOnly.FromDateTime(now).AddDays(10)},
                new Asset{AssetCode="RPT-002",Name="Unknown-cost report asset",AssetTypeId=type.Id,OwningDepartmentId=department.Id,PurchaseCost=null,PurchaseDate=null,WarrantyEndDate=DateOnly.FromDateTime(now).AddDays(20)}};
            await Add(assets.Cast<object>().ToArray());
            var software=new Software{Code="PM-RPT",Name="Report software",Publisher="Fixture"};
            var rule=new ReplacementRule{Code="RR-RPT",Name="Report rule",Severity="HIGH",MinimumAgeMonths=12,EffectiveFromUtc=now.AddDays(-10)};
            await Add(software,rule);
            var license=new SoftwareLicense{SoftwareId=software.Id,LicenseCode="BQ-RPT",LicenseType="PER_USER",TotalQuantity=2,StartsAtUtc=now.AddDays(-30),ExpiresAtUtc=now.AddDays(10),PurchaseCost=50};
            await Add(license);
            var envelope=scope.ServiceProvider.GetRequiredService<ILicenseKeyProtector>().Protect(license.Id,"QA-LICENSE-SECRET-9988");
            license.LicenseKeyCiphertext=envelope.Ciphertext;license.LicenseKeyLast4=envelope.Last4;license.KeyVersion=envelope.KeyVersion;
            audit.Record("test.fixture",license,null,"SYSTEM");await db.SaveChangesAsync();
            await Add(new LicenseAssignment{SoftwareLicenseId=license.Id,AssignedUserId=users[1].Id,AssignedByUserId=users[0].Id,AssignedAtUtc=now.AddDays(-20)},
                new AssetAssignment{AssetId=assets[0].Id,AssignedUserId=users[1].Id,AssignedByUserId=users[0].Id,AssignedAtUtc=now.AddDays(-20),ReturnedAtUtc=now.AddDays(-10),ReturnedByUserId=users[0].Id},
                new MaintenanceTicket{TicketCode="BT-RPT-1",AssetId=assets[0].Id,Title="Known cost",Description="Fixture",Status="RESOLVED",RequestedByUserId=users[0].Id,AssignedToUserId=users[2].Id,OpenedAtUtc=now.AddDays(-3),StartedAtUtc=now.AddDays(-2),ResolvedAtUtc=now.AddDays(-1),Resolution="Done",ActualCost=10.25m},
                new MaintenanceTicket{TicketCode="BT-RPT-2",AssetId=assets[1].Id,Title="Unknown cost",Description="Fixture",Status="FAILED",RequestedByUserId=users[0].Id,AssignedToUserId=users[2].Id,OpenedAtUtc=now.AddDays(-3),StartedAtUtc=now.AddDays(-2),ResolvedAtUtc=now.AddDays(-1),Resolution="Failed",ActualCost=null},
                new ReplacementRecommendation{EvaluationSnapshotJson="{}",AssetId=assets[0].Id,ReplacementRuleId=rule.Id,Priority="HIGH",Reason="Current estimate",EstimatedReplacementCost=500,PlannedReplacementYear=year,RecommendedAtUtc=now.AddDays(-3),Disposition="PLANNED",DispositionAtUtc=now.AddDays(-2),DispositionByUserId=users[0].Id},
                new ReplacementRecommendation{EvaluationSnapshotJson="{}",AssetId=assets[1].Id,ReplacementRuleId=rule.Id,Priority="MEDIUM",Reason="Unknown estimate",EstimatedReplacementCost=null,PlannedReplacementYear=year,RecommendedAtUtc=now.AddDays(-3)},
                new ReplacementRecommendation{EvaluationSnapshotJson="{}",AssetId=assets[0].Id,ReplacementRuleId=rule.Id,Priority="LOW",Reason="Historical estimate",EstimatedReplacementCost=999,PlannedReplacementYear=year,RecommendedAtUtc=now.AddDays(-6),IsCurrent=false,Disposition="SUPERSEDED",DispositionAtUtc=now.AddDays(-4)});
            return true;
        });
    }
    private async Task<int> SeedReportsAsync() {await using var scope=app!.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunReportingPermissionsAsync();}
    [LocalPostgresFact]
    public async Task Narrow_seed_and_real_read_only_readiness_are_idempotent_with_no_support_grants()
    {
        var probe=new LocalReadiness();Assert.False(await probe.IsReadyAsync(default));
        Assert.Equal(21,await SeedReportsAsync());Assert.Equal(0,await SeedReportsAsync());Assert.True(await probe.IsReadyAsync(default));
        await using var db=LocalSequenceAuthenticationTests.Database();Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(7,await db.Set<Permission>().CountAsync(x=>Permissions.Reports.Contains(x.Code)));
        var grants=from link in db.Set<RolePermission>() join role in db.Set<Role>() on link.RoleId equals role.Id join definition in db.Set<Permission>() on link.PermissionId equals definition.Id where Permissions.Reports.Contains(definition.Code) select role.Code;
        Assert.Equal(7,await grants.CountAsync(x=>x=="ADMIN_IT"));Assert.Equal(7,await grants.CountAsync(x=>x=="SYSTEM_MANAGER"));Assert.Equal(0,await grants.CountAsync(x=>x=="TECHNICAL_SUPPORT"));
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app!,AdminEmail);
        var catalog=await admin.GetFromJsonAsync<JsonElement>("/api/v1/roles/matrix");
        var cells=catalog.EnumerateArray().Where(x=>Permissions.Reports.Contains(x.GetProperty("code").GetString())).ToArray();
        Assert.Equal(7,cells.Length);Assert.All(cells,cell=>Assert.True(cell.GetProperty("available").GetBoolean()));
        Assert.Equal(7,cells.Select(cell=>cell.GetProperty("module").GetString()+"|"+cell.GetProperty("action").GetString()).Distinct().Count());
        var support=await db.Set<Role>().SingleAsync(x=>x.Code=="TECHNICAL_SUPPORT");
        var supportIds=await db.Set<RolePermission>().Where(x=>x.RoleId==support.Id).Select(x=>x.PermissionId).ToListAsync();
        supportIds.Add((await db.Set<Permission>().SingleAsync(x=>x.Code==Permissions.ReportAssets)).Id);
        using var forbidden=await admin.PutAsJsonAsync($"/api/v1/roles/{support.Id}/permissions",new {permissionIds=supportIds,rowVersion=Convert.ToBase64String(support.RowVersion)});
        Assert.Equal(HttpStatusCode.BadRequest,forbidden.StatusCode);Assert.Equal(0,await grants.CountAsync(x=>x=="TECHNICAL_SUPPORT"));
    }
    [LocalPostgresFact]
    public async Task Real_JWT_reads_all_reports_and_dashboard_with_nonempty_history_and_masks_key_and_restricted_costs()
    {
        Assert.Equal(21,await SeedReportsAsync());
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app!,AdminEmail);
        var paths=new[]{"asset-inventory","assets-by-department","assets-by-type","asset-status","assignment-history","maintenance-history","maintenance-cost","warranty-expiration","license-expiration","replacement-recommendations","replacement-budget?year="+year};
        foreach(var path in paths){using var response=await admin.GetAsync("/api/v1/reports/"+path);Assert.True(response.StatusCode==HttpStatusCode.OK,$"Report {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");}
        foreach(var path in new[]{"summary","assets","alerts","costs"}){using var response=await admin.GetAsync("/api/v1/dashboard/"+path);Assert.True(response.StatusCode==HttpStatusCode.OK,$"Dashboard {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");}
        var history=(await admin.GetFromJsonAsync<JsonElement>("/api/v1/reports/assignment-history")).GetProperty("items");Assert.Equal(1,history.GetArrayLength());
        var maintenance=(await admin.GetFromJsonAsync<JsonElement>("/api/v1/reports/maintenance-history")).GetProperty("items");Assert.Equal(2,maintenance.GetArrayLength());
        var costs=await admin.GetFromJsonAsync<JsonElement>("/api/v1/reports/maintenance-cost");Assert.Equal(10.25m,costs.GetProperty("totalCost").GetDecimal());Assert.Equal(1,costs.GetProperty("missingCostCount").GetInt32());
        var budget=await admin.GetFromJsonAsync<JsonElement>("/api/v1/reports/replacement-budget?year="+year);Assert.Equal(500m,budget.GetProperty("totalBudget").GetDecimal());Assert.Equal(1,budget.GetProperty("missingEstimateCount").GetInt32());
        var license=await admin.GetStringAsync("/api/v1/reports/license-expiration");Assert.Contains("9988",license);Assert.DoesNotContain("QA-LICENSE-SECRET",license);Assert.DoesNotContain("ciphertext",license,StringComparison.OrdinalIgnoreCase);
        using var support=await LocalAssignmentCompletionTests.LoginAsync(app!,SupportEmail);
        Assert.Equal(HttpStatusCode.Forbidden,(await support.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await support.GetAsync("/api/v1/dashboard/costs")).StatusCode);
        using var manager=await LocalAssignmentCompletionTests.LoginAsync(app!,ManagerEmail);
        var account=await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{managerId}/account");
        var allowed=Permissions.Read.Concat(Permissions.Reports.Except(new[]{Permissions.ReportCost,Permissions.DashboardCost})).Append(Permissions.AssetCost).Distinct().ToArray();
        using var changed=await admin.PutAsJsonAsync($"/api/v1/users/{managerId}/roles",new {roleIds=new[]{managerRoleId},permissionCodes=allowed,rowVersion=account.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await manager.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
        using var restricted=await LocalAssignmentCompletionTests.LoginAsync(app!,ManagerEmail);
        var inventory=await restricted.GetFromJsonAsync<JsonElement>("/api/v1/reports/asset-inventory");Assert.All(inventory.GetProperty("items").EnumerateArray(),row=>Assert.Equal(JsonValueKind.Null,row.GetProperty("purchaseCost").ValueKind));
        Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/v1/reports/asset-inventory?sortBy=purchaseCost")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/v1/reports/maintenance-cost")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/v1/reports/replacement-budget?year="+year)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/v1/dashboard/costs")).StatusCode);
        var resetAccount=await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{managerId}/account");
        using var reset=await admin.PutAsJsonAsync($"/api/v1/users/{managerId}/roles",new{roleIds=new[]{managerRoleId},resetToRoleDefaults=true,rowVersion=resetAccount.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await restricted.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
        using var restored=await LocalAssignmentCompletionTests.LoginAsync(app!,ManagerEmail);
        Assert.Equal(HttpStatusCode.OK,(await restored.GetAsync("/api/v1/reports/maintenance-cost")).StatusCode);
        var againAccount=await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{managerId}/account");
        using var again=await admin.PutAsJsonAsync($"/api/v1/users/{managerId}/roles",new{roleIds=new[]{managerRoleId},permissionCodes=allowed,rowVersion=againAccount.GetProperty("rowVersion").GetString()});
        Assert.Equal(HttpStatusCode.OK,again.StatusCode);
        var changedAgain=(await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user");
        long supportRoleId;await using(var lookup=LocalSequenceAuthenticationTests.Database())supportRoleId=(await lookup.Set<Role>().SingleAsync(x=>x.Code=="TECHNICAL_SUPPORT")).Id;
        using var switched=await admin.PutAsJsonAsync($"/api/v1/users/{managerId}/roles",new{roleIds=new[]{supportRoleId},rowVersion=changedAgain.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,switched.StatusCode);
        using var reassigned=await LocalAssignmentCompletionTests.LoginAsync(app!,ManagerEmail);
        Assert.Equal(HttpStatusCode.Forbidden,(await reassigned.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
        await using var db=LocalSequenceAuthenticationTests.Database();Assert.Equal(3,await db.Set<ReplacementRecommendation>().CountAsync());Assert.Equal(2,await db.Set<MaintenanceTicket>().CountAsync());
        Assert.Equal(0,await db.Set<UserPermission>().CountAsync(x=>x.UserId==managerId));
        var linkAudit=await db.Set<AuditLog>().Where(x=>x.EntityType==nameof(UserPermission)).ToListAsync();
        Assert.Equal(allowed.Length*4,linkAudit.Count);Assert.All(linkAudit,entry=>Assert.NotEqual("0",entry.EntityId));
        Assert.Equal(1,await db.Set<LicenseAssignment>().CountAsync()); // Role changes do not silently revoke an allocation.
    }
    [LocalPostgresFact]
    public async Task Permission_audit_failure_rolls_back_saved_links_custom_flag_and_token_version()
    {
        Assert.Equal(21,await SeedReportsAsync());
        using var manager=await LocalAssignmentCompletionTests.LoginAsync(app!,ManagerEmail);
        await using var failing=app!.WithWebHostBuilder(builder=>builder.ConfigureServices(services=> {
            services.RemoveAll<IAuditWriter>();services.AddScoped<IAuditWriter>(sp=>new FailingPermissionAudit(new AuditWriter(sp.GetRequiredService<AppDbContext>(),sp.GetRequiredService<IActor>())));
        }));
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(failing,AdminEmail);
        var account=await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{managerId}/account");
        await using var db=LocalSequenceAuthenticationTests.Database();var original=await db.Set<User>().AsNoTracking().SingleAsync(x=>x.Id==managerId);
        using var response=await admin.PutAsJsonAsync($"/api/v1/users/{managerId}/roles",new {roleIds=new[]{managerRoleId},permissionCodes=new[]{Permissions.AssetRead,Permissions.ReportAssets},rowVersion=account.GetProperty("rowVersion").GetString()});
        Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
        var actual=await db.Set<User>().AsNoTracking().SingleAsync(x=>x.Id==managerId);
        Assert.False(actual.HasCustomPermissions);Assert.Equal(original.TokenVersion,actual.TokenVersion);
        Assert.Equal(0,await db.Set<UserPermission>().CountAsync(x=>x.UserId==managerId));
        Assert.Equal(0,await db.Set<AuditLog>().CountAsync(x=>x.Action=="users.permissions.replace"));
        Assert.Equal(HttpStatusCode.OK,(await manager.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
    }
    [LocalPostgresFact]
    public async Task Closed_reporting_does_not_force_unseeded_grants_into_existing_Admin_matrix()
    {
        await using var closed=app!.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{[ReportingRollout.Key]="false"})));
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(closed,AdminEmail);
        await using var db=LocalSequenceAuthenticationTests.Database();var role=await db.Set<Role>().SingleAsync(x=>x.Code=="ADMIN_IT");
        var ids=await db.Set<RolePermission>().Where(x=>x.RoleId==role.Id).Select(x=>x.PermissionId).ToArrayAsync();
        Assert.Equal(62,ids.Length);Assert.Equal(0,await db.Set<Permission>().CountAsync(x=>Permissions.Reports.Contains(x.Code)));
        using var response=await admin.PutAsJsonAsync($"/api/v1/roles/{role.Id}/permissions",new{permissionIds=ids,rowVersion=Convert.ToBase64String(role.RowVersion)});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
    }
}
