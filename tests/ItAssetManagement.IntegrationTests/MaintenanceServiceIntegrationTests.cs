using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Actual owner service + audited UoW/EF/PostgreSQL on an EXISTING isolated target.
// No schema creation/migration, production flags, permissions or shared DB writes.
[Collection("isolated-neon")]
public sealed class MaintenanceServiceIntegrationTests(MvpFixture fixture)
{
    private sealed class Actor(long id) : IActor
    {
        public long? UserId => id; public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST"; public string? Path => "/isolated-fixture/maintenance";
        public bool Has(string p) => Grants.Contains(p);
        public static readonly string[] Grants = [Permissions.MaintenanceRead, Permissions.MaintenanceCreate,
            Permissions.MaintenanceResolve, Permissions.MaintenanceCostWrite];
    }
    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }
    private static MaintenanceService Service(IServiceProvider p, Actor actor, IAuditWriter? audit = null) => new(
        p.GetRequiredService<IRepository>(), p.GetRequiredService<IUnitOfWork>(), actor,
        audit ?? new AuditWriter(p.GetRequiredService<AppDbContext>(), actor),
        new MaintenanceAccessPolicy(new(actor.UserId!.Value, "Fixture", "fixture@test.invalid", null, ["ADMIN_IT"], Actor.Grants)));

    private async Task<(long UserId, long AssetId)> NewAsset()
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        using var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); response.EnsureSuccessStatusCode();
        return (user.Id, (await response.Content.ReadFromJsonAsync<AssetDto>())!.Id);
    }

    // A synthetic start fixture, NOT a newly implemented assign/start API. Thien
    // still owns those commands. Every fixture write is audited under the same lock.
    private static async Task<string> Start(IServiceProvider p, long id, Actor actor, bool assigned = false)
    {
        var db = p.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var repo = p.GetRequiredService<IRepository>(); var audit = new AuditWriter(db, actor);
        return await p.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var assetId = await db.Set<MaintenanceTicket>().Where(t => t.Id == id).Select(t => t.AssetId).SingleAsync();
            await repo.LockAssetAsync(assetId, default);
            var ticket = await db.Set<MaintenanceTicket>().SingleAsync(t => t.Id == id);
            var before = MaintenanceAuditState.Capture(ticket);
            var asset = await db.Set<Asset>().SingleAsync(a => a.Id == assetId); var now = DateTime.UtcNow;
            if (assigned)
            {
                var link = new AssetAssignment { AssetId = asset.Id, AssignedUserId = actor.UserId,
                    AssignedByUserId = actor.UserId!.Value, AssignedAtUtc = now, CreatedAtUtc = now };
                db.Add(link); await repo.SaveAsync(default); audit.Record("test.fixture", link, actor.UserId, "USER");
            }
            ticket.Status = "IN_PROGRESS"; ticket.AssignedToUserId = actor.UserId; ticket.StartedAtUtc = now;
            repo.Add(AssetStatusTransitions.Apply(asset, "MAINTENANCE", AssetStatusChangeSource.Maintenance, actor, now, "Synthetic start"));
            var history = new MaintenanceHistory { MaintenanceTicketId = id, EventType = "STATUS_CHANGED", FromStatus = "PENDING",
                ToStatus = "IN_PROGRESS", PerformedByUserId = actor.UserId, PerformedAtUtc = now, CorrelationId = actor.CorrelationId };
            repo.Add(history); await repo.SaveAsync(default);
            MaintenanceAudit.Record(audit, actor, ticket, history, before);
            audit.Record("test.fixture", asset, actor.UserId, "USER"); await repo.SaveAsync(default);
            return Convert.ToBase64String(ticket.RowVersion);
        });
    }

    [NeonFact]
    public async Task Create_satisfies_real_constraints_and_pending_ticket_blocks_asset_archive()
    {
        var (user, assetId) = await NewAsset(); await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var p = scope.ServiceProvider; var db = p.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var actor = new Actor(user); var result = await Service(p, actor).CreateAsync(new(assetId, "Synthetic issue", 12.25m), default);
        db.ChangeTracker.Clear();
        Assert.Equal("PENDING", result.Status); Assert.Null(result.StartedAtUtc);
        Assert.Equal("IN_STOCK", (await db.Set<Asset>().SingleAsync(a => a.Id == assetId)).CurrentStatus);
        Assert.Single(await db.Set<MaintenanceHistory>().Where(h => h.MaintenanceTicketId == result.Id && h.EventType == "CREATED").ToListAsync());
        Assert.Equal(2, await db.Set<AuditLog>().CountAsync(a => a.CorrelationId == actor.CorrelationId));
        using var client = await fixture.ClientAsync();
        var asset = (await client.GetFromJsonAsync<AssetDto>("/api/v1/assets/" + assetId))!;
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/assets/" + assetId);
        request.Headers.IfMatch.ParseAdd('"' + asset.RowVersion + '"');
        using var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed class FailHistoryAudit(IAuditWriter inner) : IAuditWriter
    {
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null)
        {
            if (entity is MaintenanceHistory) throw new InvalidOperationException("Synthetic audit failure");
            inner.Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
        }
    }

    [NeonFact]
    public async Task Create_audit_failure_rolls_back_inserted_ticket_history_and_logs()
    {
        var (user, assetId) = await NewAsset(); await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var p = scope.ServiceProvider; var db = p.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var actor = new Actor(user); var service = Service(p, actor, new FailHistoryAudit(new AuditWriter(db, actor)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new(assetId, "Synthetic rollback", null), default));
        Assert.False(await db.Set<MaintenanceTicket>().AnyAsync(t => t.AssetId == assetId));
        Assert.False(await db.Set<AuditLog>().AnyAsync(a => a.CorrelationId == actor.CorrelationId));
        Assert.Equal("IN_STOCK", (await db.Set<Asset>().SingleAsync(a => a.Id == assetId)).CurrentStatus);
    }

    [NeonFact]
    public async Task Resolve_audit_failure_rolls_back_ticket_asset_history_and_version()
    {
        var (user, assetId) = await NewAsset(); await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var p = scope.ServiceProvider; var db = p.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var ticket = await Service(p, new(user)).CreateAsync(new(assetId, "Synthetic rollback resolve", null), default);
        var version = await Start(p, ticket.Id, new(user)); db.ChangeTracker.Clear();
        var actor = new Actor(user); var service = Service(p, actor, new FailHistoryAudit(new AuditWriter(db, actor)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(ticket.Id, new("Fixed", 0m, version), default));
        var stored = await db.Set<MaintenanceTicket>().AsNoTracking().SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal("IN_PROGRESS", stored.Status); Assert.Equal(version, Convert.ToBase64String(stored.RowVersion));
        Assert.Null(stored.ResolvedAtUtc); Assert.Null(stored.ActualCost);
        Assert.Equal("MAINTENANCE", (await db.Set<Asset>().AsNoTracking().SingleAsync(a => a.Id == assetId)).CurrentStatus);
        Assert.False(await db.Set<MaintenanceHistory>().AnyAsync(h => h.MaintenanceTicketId == ticket.Id && h.EventType == "RESOLVED"));
        Assert.False(await db.Set<AuditLog>().AnyAsync(a => a.CorrelationId == actor.CorrelationId));
    }

    [NeonFact]
    public async Task Concurrent_resolve_only_commits_once_and_preserves_active_assignment()
    {
        var (user, assetId) = await NewAsset(); long id; string version;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            VerifyTarget(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            id = (await Service(scope.ServiceProvider, new(user)).CreateAsync(new(assetId, "Synthetic race", null), default)).Id;
            version = await Start(scope.ServiceProvider, id, new(user), assigned: true);
        }
        async Task<string> Resolve()
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            VerifyTarget(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            try { await Service(scope.ServiceProvider, new(user)).CompleteAsync(id, new("Fixed", 99.25m, version), default); return "OK"; }
            catch (BusinessException error) { Assert.Equal(409, error.Status); return error.Code; }
        }
        var results = await Task.WhenAll(Resolve(), Resolve());
        Assert.Single(results, r => r == "OK"); Assert.Single(results, r => r == "CONCURRENCY_CONFLICT");
        await using var verify = fixture.Factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        Assert.Equal("RESOLVED", (await db.Set<MaintenanceTicket>().SingleAsync(t => t.Id == id)).Status);
        Assert.Equal("IN_USE", (await db.Set<Asset>().SingleAsync(a => a.Id == assetId)).CurrentStatus);
        Assert.Equal(1, await db.Set<MaintenanceHistory>().CountAsync(h => h.MaintenanceTicketId == id && h.EventType == "RESOLVED"));
        Assert.Equal(1, await db.Set<AssetAssignment>().CountAsync(a => a.AssetId == assetId && a.ReturnedAtUtc == null));
    }
}
