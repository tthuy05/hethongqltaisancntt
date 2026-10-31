using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalReplacementRuleCommandTests
{
    private const string Path = "/api/v1/replacement-rules";
    private sealed class Ready : IReplacementRuleReadiness
    { public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(true); }
    private static WebApplicationFactory<Program> Factory() => LocalAssignmentCompletionTests.Factory().WithWebHostBuilder(builder => {
        builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?> {[ReplacementRuleRollout.Key]="true"}));
        builder.ConfigureServices(services=> {services.RemoveAll<IReplacementRuleReadiness>();services.AddSingleton<IReplacementRuleReadiness,Ready>();});
    });
    private static ReplacementRuleWriteRequest Request(string? version=null) => new() {
        Name="Replacement fixture "+Guid.NewGuid(),MinimumAgeMonths=60,Severity="HIGH",RowVersion=version
    };
    private static async Task<ReplacementRuleDto> CreateAsync(HttpClient client)
    {
        var request=Request();request.EstimatedUnitCost=123.45m;
        using var response=await client.PostAsJsonAsync(Path,request);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<ReplacementRuleDto>())!;
    }
    [LocalPostgresFact]
    public async Task Revision_is_append_only_serialized_and_status_cannot_reactivate_old_versions()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app);
        var first=await CreateAsync(admin);Assert.Matches("^RR-[a-f0-9]{32}$",first.Code);
        var writes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>admin.PutAsJsonAsync(Path+"/"+first.Id,
            new {name="Concurrent rule revision",minimumAgeMonths=72,severity="HIGH",rowVersion=first.RowVersion})));
        ReplacementRuleDto current;
        try {
            Assert.Single(writes,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(writes,x=>x.StatusCode==HttpStatusCode.Conflict);
            current=(await writes.Single(x=>x.StatusCode==HttpStatusCode.OK).Content.ReadFromJsonAsync<ReplacementRuleDto>())!;
        } finally {foreach(var response in writes)response.Dispose();}
        Assert.NotEqual(first.Id,current.Id);Assert.Equal(first.Code,current.Code);Assert.Equal(2,current.Version);
        Assert.Equal(123.45m,current.EstimatedUnitCost); // Omitted price preserves the old estimate.
        var old=(await admin.GetFromJsonAsync<ReplacementRuleDto>(Path+"/"+first.Id))!;
        Assert.False(old.IsActive);Assert.NotNull(old.EffectiveTo);Assert.Equal(first.Name,old.Name);Assert.Equal(first.MinimumAgeMonths,old.MinimumAgeMonths);
        using var forbidden=await admin.PatchAsJsonAsync(Path+"/"+old.Id+"/status",new {isActive=true,rowVersion=old.RowVersion});
        Assert.Equal(HttpStatusCode.Conflict,forbidden.StatusCode);
        using var stopped=await admin.PatchAsJsonAsync(Path+"/"+current.Id+"/status",new {isActive=false,rowVersion=current.RowVersion});
        Assert.Equal(HttpStatusCode.OK,stopped.StatusCode);
        var inactive=(await stopped.Content.ReadFromJsonAsync<ReplacementRuleDto>())!;
        using var activated=await admin.PatchAsJsonAsync(Path+"/"+current.Id+"/status",new {isActive=true,rowVersion=inactive.RowVersion});
        Assert.Equal(HttpStatusCode.OK,activated.StatusCode);
        await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2,await db.Set<ReplacementRule>().CountAsync(x=>x.Code==first.Code));
        Assert.Equal(1,await db.Set<ReplacementRule>().CountAsync(x=>x.Code==first.Code&&x.IsActive));
        Assert.Equal(1,await db.Set<AuditLog>().CountAsync(x=>x.Action=="replacement-rules.revise"&&x.EntityId==current.Id.ToString()));
    }
    [LocalPostgresFact]
    public async Task Financial_omission_preserves_hidden_estimate_and_explicit_null_is_denied()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app);var first=await CreateAsync(admin);
        var actor=await LocalAssignmentCompletionTests.ActorAsync(app,"ADMIN_IT",Permissions.ReplacementRuleRead,Permissions.ReplacementRuleUpdate);
        using var limited=await LocalAssignmentCompletionTests.LoginAsync(app,actor.Email);
        // Serialize anonymous DTO: the nullable public property must truly be omitted.
        using var response=await limited.PutAsJsonAsync(Path+"/"+first.Id,new {name="Hidden estimate revision",minimumAgeMonths=72,severity="HIGH",rowVersion=first.RowVersion});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.DoesNotContain("estimatedUnitCost",await response.Content.ReadAsStringAsync());
        var current=(await response.Content.ReadFromJsonAsync<ReplacementRuleDto>())!;
        using var clear=await limited.PutAsJsonAsync(Path+"/"+current.Id,new {name="Denied",minimumAgeMonths=72,severity="HIGH",estimatedUnitCost=(decimal?)null,rowVersion=current.RowVersion});
        Assert.Equal(HttpStatusCode.Forbidden,clear.StatusCode);
        Assert.Equal(123.45m,(await admin.GetFromJsonAsync<ReplacementRuleDto>(Path+"/"+current.Id))!.EstimatedUnitCost);
        using var code=await admin.PostAsJsonAsync(Path,new {name="Rejected code",minimumAgeMonths=60,severity="HIGH",code="MY-CODE"});
        Assert.Equal(HttpStatusCode.BadRequest,code.StatusCode);
    }
    private sealed class FailRevision(IAuditWriter inner) : IAuditWriter
    {
        public void Record(string action,object? entity,long? actorId,string actorType,string outcome="SUCCESS",object? before=null,object? after=null,string? failureCode=null)
        {
            inner.Record(action,entity,actorId,actorType,outcome,before,after,failureCode);
            if(action=="replacement-rules.revise")throw new InvalidOperationException("Synthetic rule audit failure");
        }
    }
    [LocalPostgresFact]
    public async Task Permission_revoked_while_waiting_for_rule_lock_denies_publication()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        var actor=await LocalAssignmentCompletionTests.ActorAsync(app,"ADMIN_IT",Permissions.ReplacementRuleRead,Permissions.ReplacementRuleCreate);
        using var writer=await LocalAssignmentCompletionTests.LoginAsync(app,actor.Email);
        await using var blocker=LocalSequenceAuthenticationTests.Database();await using var tx=await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"replacement-rules"},0))");
        var name="Denied after rule lock "+Guid.NewGuid();
        var pending=writer.PostAsJsonAsync(Path,new {name,minimumAgeMonths=60,severity="HIGH"});
        try {
            await using var observer=LocalSequenceAuthenticationTests.Database();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while(!await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted) AS \"Value\"").SingleAsync(deadline.Token)) await Task.Delay(20,deadline.Token);
            await using var scope=app.Services.CreateAsyncScope();var sp=scope.ServiceProvider;var db=sp.GetRequiredService<AppDbContext>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async ()=> {
                await sp.GetRequiredService<IRepository>().LockAsync("users-identity",default);
                var permission=await db.Set<Permission>().SingleAsync(x=>x.Code==Permissions.ReplacementRuleCreate);
                var grant=await db.Set<UserPermission>().SingleAsync(x=>x.UserId==actor.Id&&x.PermissionId==permission.Id);
                sp.GetRequiredService<IAccountPersistence>().RemoveUserPermission(grant);
                sp.GetRequiredService<IAuditWriter>().Record("test.revoke",grant,null,"SYSTEM");await db.SaveChangesAsync();return true;
            });
        } finally {await tx.RollbackAsync();}
        using var response=await pending;Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        await using var verify=LocalSequenceAuthenticationTests.Database();Assert.False(await verify.Set<ReplacementRule>().AnyAsync(x=>x.Name==name));
        Assert.False(verify.Database.HasPendingModelChanges());
    }
    [LocalPostgresFact]
    public async Task Non_admin_with_all_rule_grants_cannot_publish()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        var roleCode="RULE_EDITOR_"+Guid.NewGuid().ToString("N");
        await using(var scope=app.Services.CreateAsyncScope()) {
            var sp=scope.ServiceProvider;var db=sp.GetRequiredService<AppDbContext>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async ()=> {
                var role=new Role {Code=roleCode,Name=roleCode};db.Add(role);await db.SaveChangesAsync();
                sp.GetRequiredService<IAuditWriter>().Record("test.fixture",role,null,"SYSTEM");
                foreach(var code in new[]{Permissions.ReplacementRuleRead,Permissions.ReplacementRuleCreate,Permissions.ReplacementRuleUpdate}) {
                    var permission=await db.Set<Permission>().SingleAsync(x=>x.Code==code);
                    var grant=new RolePermission {RoleId=role.Id,PermissionId=permission.Id,GrantedAtUtc=DateTime.UtcNow};
                    db.Add(grant);sp.GetRequiredService<IAuditWriter>().Record("test.fixture",grant,null,"SYSTEM");
                }
                await db.SaveChangesAsync();return true;
            });
        }
        var actor=await LocalAssignmentCompletionTests.ActorAsync(app,roleCode,Permissions.ReplacementRuleRead,Permissions.ReplacementRuleCreate,Permissions.ReplacementRuleUpdate);
        using var writer=await LocalAssignmentCompletionTests.LoginAsync(app,actor.Email);
        Assert.Equal(HttpStatusCode.OK,(await writer.GetAsync(Path)).StatusCode);
        using var response=await writer.PostAsJsonAsync(Path,new {name="Denied despite misgrant",minimumAgeMonths=60,severity="HIGH"});
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [LocalPostgresFact]
    public async Task Failed_audit_rolls_back_old_rule_closure_new_revision_and_logs()
    {
        await using var app=Factory();await LocalAssignmentCompletionTests.SeedAsync(app);
        using var admin=await LocalAssignmentCompletionTests.LoginAsync(app);var first=await CreateAsync(admin);
        await using var failing=app.WithWebHostBuilder(builder=>builder.ConfigureServices(services=> {
            services.RemoveAll<IAuditWriter>();services.AddScoped<IAuditWriter>(sp=>new FailRevision(new ItAssetManagement.Infrastructure.Mvp.AuditWriter(sp.GetRequiredService<AppDbContext>(),sp.GetRequiredService<IActor>())));
        }));
        using var writer=await LocalAssignmentCompletionTests.LoginAsync(failing);
        using var response=await writer.PutAsJsonAsync(Path+"/"+first.Id,Request(first.RowVersion));
        Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
        var unchanged=(await admin.GetFromJsonAsync<ReplacementRuleDto>(Path+"/"+first.Id))!;
        Assert.True(unchanged.IsActive);Assert.Null(unchanged.EffectiveTo);Assert.Equal(first.RowVersion,unchanged.RowVersion);
        await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1,await db.Set<ReplacementRule>().CountAsync(x=>x.Code==first.Code));
        Assert.False(await db.Set<AuditLog>().AnyAsync(x=>x.Action=="replacement-rules.supersede"&&x.EntityId==first.Id.ToString()));
        var row=await db.Set<ReplacementRule>().SingleAsync(x=>x.Id==first.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async ()=> {
            row.MinimumAgeMonths=99;await db.SaveChangesAsync();return true;
        }));
    }
}
