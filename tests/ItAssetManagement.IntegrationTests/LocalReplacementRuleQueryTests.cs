using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalReplacementRuleQueryTests
{
    private sealed class Ready : IReplacementRuleReadiness
    { public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(true); }

    [LocalPostgresFact]
    public async Task Real_JWT_query_translates_to_Postgres_and_redacts_cost_without_grant()
    {
        await using var baseline = LocalAssignmentCompletionTests.Factory();
        await using var app = baseline.WithWebHostBuilder(builder => {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?> {
                [ReplacementRuleRollout.Key] = "true"
            }));
            builder.ConfigureServices(services => { services.RemoveAll<IReplacementRuleReadiness>(); services.AddSingleton<IReplacementRuleReadiness,Ready>(); });
        });
        await LocalAssignmentCompletionTests.SeedAsync(app);
        var name = "Replacement % " + Guid.NewGuid();
        long id;
        await using (var scope = app.Services.CreateAsyncScope()) {
            var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
            id = await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                // The generic seed preserves existing role matrices on rerun.
                // This fixture explicitly grants the new read permission on its isolated DB.
                var manager = await db.Set<Role>().SingleAsync(x=>x.Code=="SYSTEM_MANAGER");
                var permission = await db.Set<Permission>().SingleAsync(x=>x.Code==Permissions.ReplacementRuleRead);
                if (!await db.Set<RolePermission>().AnyAsync(x=>x.RoleId==manager.Id && x.PermissionId==permission.Id)) {
                    var grant = new RolePermission {RoleId=manager.Id,PermissionId=permission.Id,GrantedAtUtc=DateTime.UtcNow};
                    db.Add(grant); sp.GetRequiredService<IAuditWriter>().Record("test.fixture",grant,null,"SYSTEM");
                }
                var rule = new ReplacementRule { Code = "RULE-"+Guid.NewGuid(), Name = name,
                    MinimumAgeMonths = 60, EstimatedUnitCost = 345.67m, EffectiveFromUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow, RowVersion = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16) };
                db.Add(rule); await db.SaveChangesAsync();
                sp.GetRequiredService<IAuditWriter>().Record("test.fixture",rule,null,"SYSTEM");
                await db.SaveChangesAsync(); return rule.Id;
            });
        }
        var reader = await LocalAssignmentCompletionTests.ActorAsync(app,"SYSTEM_MANAGER",Permissions.ReplacementRuleRead);
        using var client = await LocalAssignmentCompletionTests.LoginAsync(app,reader.Email);
        using var response = await client.GetAsync("/api/v1/replacement-rules?keyword="+Uri.EscapeDataString(name)+"&status=Active&sortBy=priority");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<PagedResponse<ReplacementRuleDto>>();
        Assert.Equal(id,Assert.Single(list!.Items).Id);
        Assert.Null(list.Items[0].EstimatedUnitCost);
        Assert.DoesNotContain("estimatedUnitCost",await response.Content.ReadAsStringAsync());
        using var detail = await client.GetAsync("/api/v1/replacement-rules/"+id);
        Assert.Equal(HttpStatusCode.OK,detail.StatusCode); Assert.NotNull(detail.Headers.ETag);
        Assert.True(detail.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/replacement-rules?sortBy=estimatedUnitCost")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/replacement-rules?unexpected=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/v1/replacement-rules/9223372036854775807")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/v1/replacement-rules",new {name="Denied"})).StatusCode);
        var support = await LocalAssignmentCompletionTests.ActorAsync(app,"TECHNICAL_SUPPORT");
        using var denied = await LocalAssignmentCompletionTests.LoginAsync(app,support.Email);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync("/api/v1/replacement-rules")).StatusCode);
        using var admin = await LocalAssignmentCompletionTests.LoginAsync(app);
        using var priced = await admin.GetAsync("/api/v1/replacement-rules/"+id);
        Assert.Equal(345.67m,(await priced.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estimatedUnitCost").GetDecimal());
        await using var verifyScope = app.Services.CreateAsyncScope();
        var stored = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ReplacementRule>().SingleAsync(x=>x.Id==id);
        Assert.Equal(name,stored.Name); Assert.True(stored.IsActive);
    }
}
