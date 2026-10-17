using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class AssetStatusTransitionIntegrationTests(MvpFixture fixture)
{
    private sealed class Actor(long id) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/isolated-fixture/shared-status";
        public bool Has(string permission) => false;
    }

    private async Task<AssetDto> CreateAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }

    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_(m1|full_schema)_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }

    [NeonFact]
    public async Task Retire_preserves_http_contract_history_audit_correlation_and_stale_write_protection()
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var created = await CreateAsync(client);
        var command = new StatusRequest("Retired", "  Shared status contract  ", created.RowVersion);
        using var response = await client.PatchAsJsonAsync($"/api/v1/assets/{created.Id}/status", command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AssetDto>())!;
        Assert.Equal("Retired", result.Status); Assert.NotEqual(created.RowVersion, result.RowVersion);

        using var repeated = await client.PatchAsJsonAsync($"/api/v1/assets/{created.Id}/status", command);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var history = await db.Set<AssetStatusHistory>().SingleAsync(x => x.AssetId == created.Id && x.Source == "ADMIN");
        var audit = await db.Set<AuditLog>().SingleAsync(x => x.EntityType == "Asset" &&
            x.EntityId == created.Id.ToString() && x.Action == "assets.status.change" && x.Outcome == "SUCCESS");
        Assert.Equal("IN_STOCK", history.FromStatus); Assert.Equal("RETIRED", history.ToStatus);
        Assert.Equal("Shared status contract", history.Reason);
        Assert.Equal(user.Id, history.ChangedByUserId); Assert.Equal(user.Id, audit.ActorUserId);
        Assert.NotEqual(Guid.Empty, history.CorrelationId); Assert.Equal(history.CorrelationId, audit.CorrelationId);
        Assert.Equal(DateTimeKind.Utc, history.ChangedAtUtc.Kind);
        Assert.DoesNotContain("Shared status contract", audit.NewValuesJson ?? "");
        Assert.Equal(2, await db.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == created.Id));
    }

    [NeonFact]
    public Task Missing_audit_rolls_back_shared_status_and_history() => VerifyRollbackAsync(recordAudit: false);

    [NeonFact]
    public Task Failure_after_audit_rolls_back_status_history_and_audit() => VerifyRollbackAsync(recordAudit: true);

    private async Task VerifyRollbackAsync(bool recordAudit)
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var created = await CreateAsync(client); var actor = new Actor(user.Id);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider; var db = provider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
            var repo = provider.GetRequiredService<IRepository>(); var uow = provider.GetRequiredService<IUnitOfWork>();
            var audit = new AuditWriter(db, actor);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => uow.RunAsync(async () =>
            {
                await repo.LockAssetAsync(created.Id, default);
                var asset = await db.Set<Asset>().SingleAsync(x => x.Id == created.Id);
                var before = new { asset.CurrentStatus, asset.IsArchived };
                repo.ExpectVersion(asset, Convert.FromBase64String(created.RowVersion));
                var history = AssetStatusTransitions.Apply(asset, "MAINTENANCE", AssetStatusChangeSource.Maintenance,
                    actor, DateTime.UtcNow, "Isolated rollback probe");
                repo.Add(history); await repo.SaveAsync(default);
                Assert.Equal(actor.CorrelationId, history.CorrelationId);
                if (recordAudit)
                {
                    audit.Record("maintenance.start", asset, actor.UserId, "USER", before: before,
                        after: new { asset.CurrentStatus, asset.IsArchived });
                    await repo.SaveAsync(default);
                    throw new InvalidOperationException("Simulated workflow failure after save.");
                }
                return true; // AuditedUnitOfWork must refuse the missing audit at commit.
            }));
            Assert.Equal(recordAudit ? "Simulated workflow failure after save." :
                "Every business write requires a transactional audit event.", error.Message);
        }
        await using var verify = fixture.Factory.Services.CreateAsyncScope();
        var verificationDb = verify.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(verificationDb);
        var persisted = await verificationDb.Set<Asset>().SingleAsync(x => x.Id == created.Id);
        Assert.Equal("IN_STOCK", persisted.CurrentStatus);
        Assert.Equal(Convert.FromBase64String(created.RowVersion), persisted.RowVersion);
        Assert.Equal(1, await verificationDb.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == created.Id));
        Assert.False(await verificationDb.Set<AuditLog>().AnyAsync(x => x.CorrelationId == actor.CorrelationId));
    }
}
