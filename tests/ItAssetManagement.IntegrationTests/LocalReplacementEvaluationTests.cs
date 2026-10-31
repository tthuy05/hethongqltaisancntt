using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalReplacementEvaluationTests : IAsyncLifetime
{
    private string? previous;
    private static string ScriptPath()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(!File.Exists(System.IO.Path.Combine(root.FullName,"ItAssetManagement.slnx"))) root=root.Parent??throw new InvalidOperationException("Repository root missing");
        return System.IO.Path.Combine(root.FullName,"scripts/deploy-replacement-severity.sql");
    }
    private sealed class Ready : IReplacementRuleReadiness { public Task<bool> IsReadyAsync(CancellationToken ct)=>Task.FromResult(true); }
    public async Task InitializeAsync()
    {
        previous=Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION");
        if(string.IsNullOrEmpty(previous))return;
        await using var guard=LocalSequenceAuthenticationTests.Database(); // Reject shared/pooled/credentialless targets.
        var settings=new NpgsqlConnectionStringBuilder(previous);
        var database="itam_local_verify_replacement_"+Guid.NewGuid().ToString("N");
        settings.Database="postgres";
        await using(var connection=new NpgsqlConnection(settings.ConnectionString)) {
            await connection.OpenAsync();
            await using var command=new NpgsqlCommand("CREATE DATABASE \""+database+"\"",connection);
            await command.ExecuteNonQueryAsync();
        }
        settings.Database=database;Environment.SetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION",settings.ConnectionString);
        ItAssetManagement.Infrastructure.Mvp.BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        await using var db=LocalSequenceAuthenticationTests.Database();
        await db.GetService<IMigrator>().MigrateAsync("20261024043835_AddUserPermissions");
        // Upgrade rehearsal retains a legacy nullable-severity rule without guessing/backfill.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO replacement_rules(code,name,minimum_age_months,is_active,row_version) VALUES ('LEGACY','Preserve legacy',60,false,decode(repeat('01',16),'hex'))");
        var script=await File.ReadAllTextAsync(ScriptPath());
        await db.Database.OpenConnectionAsync();
        try {
            await db.Database.ExecuteSqlRawAsync("SET itam.replacement_rollout_approved='20261030024812'");
            await db.Database.ExecuteSqlRawAsync(script);
        } finally {await db.Database.CloseConnectionAsync();}
        var legacy=await db.Set<ReplacementRule>().SingleAsync(x=>x.Code=="LEGACY");
        Assert.Null(legacy.Severity);Assert.Equal("Preserve legacy",legacy.Name);Assert.Equal(60,legacy.MinimumAgeMonths);
        Assert.Equal(6,(await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='replacement_rules' AND column_name='severity' AND udt_name='varchar' AND character_maximum_length=20 AND is_nullable='YES') AND (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='c')=86 AND (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f')=44 AS \"Value\"").SingleAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
    public Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION",previous);
        ItAssetManagement.Infrastructure.Mvp.BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        return Task.CompletedTask; // Isolated databases retained for evidence; never overwrite/drop an old target.
    }
    [LocalPostgresFact]
    public async Task Guarded_script_refuses_missing_approval_and_reapplication_without_changing_data()
    {
        var script=await File.ReadAllTextAsync(ScriptPath());
        await using(var db=LocalSequenceAuthenticationTests.Database()) {
            var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync(script));
            Assert.Equal("P0001",error.SqlState);Assert.Equal("REPLACEMENT_ROLLOUT_NOT_APPROVED",error.MessageText);
        } // Dispose failed transaction/connection before issuing any further SQL.
        await using(var db=LocalSequenceAuthenticationTests.Database()) {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET itam.replacement_rollout_approved='20261030024812'");
            var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync(script));
            Assert.Equal("P0001",error.SqlState);Assert.Equal("REPLACEMENT_SCHEMA_BASELINE_CHANGED",error.MessageText);
        }
        await using var verify=LocalSequenceAuthenticationTests.Database();
        Assert.Equal(6,(await verify.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal("Preserve legacy",(await verify.Set<ReplacementRule>().SingleAsync(x=>x.Code=="LEGACY")).Name);
        await using(var invalid=LocalSequenceAuthenticationTests.Database()) {
            var error=await Assert.ThrowsAsync<PostgresException>(()=>invalid.Database.ExecuteSqlRawAsync("UPDATE replacement_rules SET severity='INVALID' WHERE code='LEGACY'"));
            Assert.Equal("23514",error.SqlState);
        }
        Assert.Null((await verify.Set<ReplacementRule>().AsNoTracking().SingleAsync(x=>x.Code=="LEGACY")).Severity);
    }
    private static WebApplicationFactory<Program> Factory()=>LocalAssignmentCompletionTests.Factory().WithWebHostBuilder(builder=> {
        builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?> {[ReplacementRuleRollout.Key]="true"}));
        builder.ConfigureServices(services=> {services.RemoveAll<IReplacementRuleReadiness>();services.AddSingleton<IReplacementRuleReadiness,Ready>();});
    });
    private static async Task<ReplacementRuleDto> RuleAsync(HttpClient client,long? type=null)
    {
        using var response=await client.PostAsJsonAsync("/api/v1/replacement-rules",new {name="Age test",minimumAgeMonths=1,severity="HIGH",assetTypeId=type,estimatedUnitCost=4500m});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);return (await response.Content.ReadFromJsonAsync<ReplacementRuleDto>())!;
    }
    private static async Task<long> AssetAsync(WebApplicationFactory<Program> app,HttpClient client)
    {
        var refs=await LocalAssignmentCompletionTests.References(app);
        using var response=await client.PostAsJsonAsync("/api/v1/assets",new AssetRequest {Name="Replacement asset",AssetTypeId=refs.Type,
            OwningDepartmentId=refs.Department,PurchaseDate=DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-5)),PurchasePrice=1000m});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);return (await response.Content.ReadFromJsonAsync<AssetDto>())!.Id;
    }
    [LocalPostgresFact]
    public async Task Evaluation_plan_reevaluation_and_redaction_preserve_evidence_and_never_retire_asset()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app);
        var rule=await RuleAsync(admin);var asset=await AssetAsync(app,admin);
        const string path="/api/v1/replacement-recommendations";
        using var firstResponse=await admin.PostAsJsonAsync(path+"/evaluate",new {assetIds=new[]{asset}});
        Assert.Equal(HttpStatusCode.OK,firstResponse.StatusCode);
        var first=(await firstResponse.Content.ReadFromJsonAsync<ReplacementEvaluationSummary>())!;
        Assert.Equal(1,first.Created);var id=first.Results[0].RecommendationId!.Value;
        var detail=(await admin.GetFromJsonAsync<ReplacementRecommendationDto>(path+"/"+id))!;
        Assert.Equal("HIGH",detail.Priority);Assert.Equal(4500m,detail.EstimatedReplacementCost);
        Assert.Contains("RESOLVED_PLUS_FAILED_INCLUDING_ARCHIVED",detail.EvaluationSnapshotJson);
        using var plan=await admin.PatchAsJsonAsync(path+"/"+id,new {disposition="PLANNED",plannedReplacementYear=2027,note="Keep planning note",rowVersion=detail.RowVersion});
        Assert.Equal(HttpStatusCode.OK,plan.StatusCode);
        var planned=(await plan.Content.ReadFromJsonAsync<ReplacementRecommendationDto>())!;
        using var rescheduled=await admin.PatchAsJsonAsync(path+"/"+id,new {disposition="PLANNED",plannedReplacementYear=2028,note="Keep planning note",rowVersion=planned.RowVersion});
        Assert.Equal(HttpStatusCode.OK,rescheduled.StatusCode);
        var writes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>admin.PostAsJsonAsync(path+"/evaluate",new {assetIds=new[]{asset}})));
        try {Assert.All(writes,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));}finally {foreach(var response in writes)response.Dispose();}
        var old=(await admin.GetFromJsonAsync<ReplacementRecommendationDto>(path+"/"+id))!;
        Assert.Equal("SUPERSEDED",old.Disposition);Assert.False(old.IsCurrent);Assert.Equal("Keep planning note",old.DispositionNote);
        Assert.Equal(detail.EvaluationSnapshotJson,old.EvaluationSnapshotJson);
        Assert.Equal((short)2028,old.PlannedReplacementYear);
        var references=await LocalAssignmentCompletionTests.References(app);
        using var filtered=await admin.GetAsync(path+"?departmentId="+references.Department+"&sortBy=priority&sortDirection=desc&isCurrent=true");
        Assert.Equal(HttpStatusCode.OK,filtered.StatusCode);
        Assert.Single((await filtered.Content.ReadFromJsonAsync<PagedResponse<ReplacementRecommendationDto>>())!.Items);
        var reader=await LocalAssignmentCompletionTests.ActorAsync(app,"SYSTEM_MANAGER",Permissions.ReplacementRead,Permissions.AssetRead);
        using var limited=await LocalAssignmentCompletionTests.LoginAsync(app,reader.Email);
        var redacted=await limited.GetStringAsync(path+"/"+id);
        Assert.DoesNotContain("evaluationSnapshotJson",redacted);Assert.DoesNotContain("estimatedReplacementCost",redacted);Assert.DoesNotContain("4500",redacted);
        Assert.Equal(HttpStatusCode.Forbidden,(await limited.PostAsJsonAsync(path+"/evaluate",new {assetIds=new[]{asset}})).StatusCode);
        await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3,await db.Set<ReplacementRecommendation>().CountAsync(x=>x.AssetId==asset));
        Assert.Equal(1,await db.Set<ReplacementRecommendation>().CountAsync(x=>x.AssetId==asset&&x.IsCurrent));
        Assert.Equal("IN_STOCK",(await db.Set<Asset>().SingleAsync(x=>x.Id==asset)).CurrentStatus);
        var storedRule=await admin.GetFromJsonAsync<ReplacementRuleDto>("/api/v1/replacement-rules/"+rule.Id);
        using var stopped=await admin.PatchAsJsonAsync("/api/v1/replacement-rules/"+rule.Id+"/status",new {isActive=false,rowVersion=storedRule!.RowVersion});
        Assert.Equal(HttpStatusCode.OK,stopped.StatusCode);
        using var noMatch=await admin.PostAsJsonAsync(path+"/evaluate",new {assetIds=new[]{asset}});
        Assert.Equal(0,(await noMatch.Content.ReadFromJsonAsync<ReplacementEvaluationSummary>())!.Created);
        db.ChangeTracker.Clear();Assert.False(await db.Set<ReplacementRecommendation>().AnyAsync(x=>x.AssetId==asset&&x.IsCurrent));
    }
    private sealed class FailEvaluation(IAuditWriter inner) : IAuditWriter
    {
        public void Record(string action,object? entity,long? actorId,string actorType,string outcome="SUCCESS",object? before=null,object? after=null,string? failureCode=null)
        { inner.Record(action,entity,actorId,actorType,outcome,before,after,failureCode);if(action=="replacement.evaluate")throw new InvalidOperationException("Synthetic evaluation audit failure"); }
    }
    [LocalPostgresFact]
    public async Task Closed_replacement_does_not_force_unseeded_grants_into_protected_admin_matrix()
    {
        await using var baseline=Factory();
        await using var app=baseline.WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?> {[ReplacementRuleRollout.Key]="false"})));
        await LocalAssignmentCompletionTests.SeedAsync(app);
        long roleId;long[] ids;string version;
        await using(var scope=app.Services.CreateAsyncScope()) {
            var sp=scope.ServiceProvider;var db=sp.GetRequiredService<AppDbContext>();
            roleId=(await db.Set<Role>().SingleAsync(x=>x.Code=="ADMIN_IT")).Id;
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async()=> {
                var removed=await (from link in db.Set<RolePermission>() join permission in db.Set<Permission>() on link.PermissionId equals permission.Id
                    where link.RoleId==roleId&&ReplacementPermissions.All.Contains(permission.Code) select link).ToListAsync();
                foreach(var link in removed) {sp.GetRequiredService<IRoleGrantPersistence>().RemoveGrant(link);sp.GetRequiredService<IAuditWriter>().Record("test.fixture",link,null,"SYSTEM");}
                await db.SaveChangesAsync();return true;
            });
            ids=await db.Set<RolePermission>().Where(x=>x.RoleId==roleId).Select(x=>x.PermissionId).ToArrayAsync();
            version=Convert.ToBase64String((await db.Set<Role>().SingleAsync(x=>x.Id==roleId)).RowVersion);
        }
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app);
        using var response=await admin.PutAsJsonAsync("/api/v1/roles/"+roleId+"/permissions",new {permissionIds=ids,rowVersion=version});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
    }
    [LocalPostgresFact]
    public async Task Narrow_permission_seed_is_additive_idempotent_and_excludes_support()
    {
        await using var app=Factory();
        await using var scope=app.Services.CreateAsyncScope();var sp=scope.ServiceProvider;var db=sp.GetRequiredService<AppDbContext>();
        await sp.GetRequiredService<IUnitOfWork>().RunAsync(async()=> {
            foreach(var code in Permissions.Roles) {var role=new Role {Code=code,Name=code,IsSystem=true};db.Add(role);sp.GetRequiredService<IAuditWriter>().Record("test.fixture",role,null,"SYSTEM");}
            await db.SaveChangesAsync();return true;
        });
        var seed=sp.GetRequiredService<ItAssetManagement.Infrastructure.Mvp.DevelopmentSeed>();
        Assert.Equal(16,await seed.RunReplacementPermissionsAsync());Assert.Equal(0,await seed.RunReplacementPermissionsAsync());
        Assert.Equal(6,await db.Set<Permission>().CountAsync(x=>ReplacementPermissions.All.Contains(x.Code)));
        Assert.Equal(10,await db.Set<RolePermission>().CountAsync());
        var support=(await db.Set<Role>().SingleAsync(x=>x.Code=="TECHNICAL_SUPPORT")).Id;
        Assert.False(await db.Set<RolePermission>().AnyAsync(x=>x.RoleId==support));
        Assert.False(await db.Set<Asset>().AnyAsync());Assert.False(await db.Set<ReplacementRecommendation>().AnyAsync());
    }
    [LocalPostgresFact]
    public async Task Audit_failure_and_incomplete_rule_cannot_destroy_current_recommendation()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app);
        await RuleAsync(admin);var asset=await AssetAsync(app,admin);
        const string path="/api/v1/replacement-recommendations/evaluate";
        using var initial=await admin.PostAsJsonAsync(path,new {assetIds=new[]{asset}});
        Assert.Equal(HttpStatusCode.OK,initial.StatusCode);
        var id=(await initial.Content.ReadFromJsonAsync<ReplacementEvaluationSummary>())!.Results[0].RecommendationId!.Value;
        await using var failing=app.WithWebHostBuilder(builder=>builder.ConfigureServices(services=> {
            services.RemoveAll<IAuditWriter>();services.AddScoped<IAuditWriter>(sp=>new FailEvaluation(new ItAssetManagement.Infrastructure.Mvp.AuditWriter(sp.GetRequiredService<AppDbContext>(),sp.GetRequiredService<IActor>())));
        }));
        using var writer=await LocalAssignmentCompletionTests.LoginAsync(failing);
        using var failed=await writer.PostAsJsonAsync(path,new {assetIds=new[]{asset}});
        Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);
        await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(id,(await db.Set<ReplacementRecommendation>().SingleAsync(x=>x.AssetId==asset&&x.IsCurrent)).Id);
        Assert.Equal(1,await db.Set<ReplacementRecommendation>().CountAsync(x=>x.AssetId==asset));
        using var historical=await admin.PostAsJsonAsync(path,new {assetIds=new[]{asset},asOfDate="2000-01-01"});
        Assert.Equal(HttpStatusCode.BadRequest,historical.StatusCode);
        Assert.False(await db.Set<AuditLog>().AnyAsync(x=>x.Action=="replacement.supersede"&&x.EntityId==id.ToString()));
        var legacy=await db.Set<ReplacementRule>().SingleAsync(x=>x.Code=="LEGACY");
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async ()=> {
            legacy.IsActive=true;scope.ServiceProvider.GetRequiredService<IAuditWriter>().Record("test.fixture",legacy,null,"SYSTEM");await db.SaveChangesAsync();return true;
        });
        using var incomplete=await admin.PostAsJsonAsync(path,new {assetIds=new[]{asset}});
        Assert.Equal(HttpStatusCode.Conflict,incomplete.StatusCode);
        db.ChangeTracker.Clear();Assert.Equal(id,(await db.Set<ReplacementRecommendation>().SingleAsync(x=>x.AssetId==asset&&x.IsCurrent)).Id);
    }
}
