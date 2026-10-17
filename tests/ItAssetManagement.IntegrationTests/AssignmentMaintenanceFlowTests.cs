using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Week 4 Thuy integration protocol: real AssignmentService + shared status/audit
// helpers + PostgreSQL. Maintenance commands below stage fixture-owned rows only;
// they are not a replacement MaintenanceService, controller or permission workflow.
[Collection("isolated-neon")]
public sealed class AssignmentMaintenanceFlowTests(MvpFixture fixture)
{
    private sealed record Prepared(long AssetId, AssignmentDto Assignment, long TicketId);
    private sealed record Outcome(string Status, Guid Correlation, int AssetHistories, int TicketHistories);

    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }

    private async Task<T> AsAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider; VerifyTarget(provider.GetRequiredService<AppDbContext>());
        var accessor = provider.GetRequiredService<IHttpContextAccessor>(); var previous = accessor.HttpContext;
        var claims = new List<Claim> { new("sub", user.Id.ToString()) };
        claims.AddRange(user.Permissions.Select(code => new Claim("permission", code)));
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "isolated-fixture")) };
        accessor.HttpContext.Request.Method = "POST";
        accessor.HttpContext.Request.Path = "/isolated-fixture/cross-module-flow";
        try { return await work(provider); }
        finally { accessor.HttpContext = previous; }
    }

    private async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        return await work(db);
    }

    private async Task<Prepared> PrepareAsync()
    {
        using var client = await fixture.ClientAsync();
        using var created = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!;
        var assignment = await AsAsync(provider => provider.GetRequiredService<AssignmentService>().AssignAsync(new()
        { AssetId = asset.Id, UserId = provider.GetRequiredService<IActor>().UserId }, default));
        var ticketId = await AsAsync(provider => provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var db = provider.GetRequiredService<AppDbContext>(); var repo = provider.GetRequiredService<IRepository>();
            var actor = provider.GetRequiredService<IActor>(); var audit = provider.GetRequiredService<IAuditWriter>();
            await repo.LockAssetAsync(asset.Id, default);
            var technician = await db.Set<User>().SingleAsync(user => user.Email == fixture.SupportEmail);
            var now = DateTime.UtcNow;
            var ticket = new MaintenanceTicket
            {
                AssetId = asset.Id, TicketCode = "FLOW" + Guid.NewGuid().ToString("N"), Title = "Isolated cross-module fixture",
                Description = "Private fixture diagnosis", RequestedByUserId = actor.UserId!.Value,
                AssignedToUserId = technician.Id, OpenedAtUtc = now
            };
            repo.Add(ticket); await repo.SaveAsync(default);
            var history = new MaintenanceHistory
            {
                MaintenanceTicketId = ticket.Id, EventType = "CREATED", ToStatus = "PENDING",
                ToAssignedToUserId = technician.Id, PerformedAtUtc = now,
                PerformedByUserId = actor.UserId, CorrelationId = actor.CorrelationId
            };
            repo.Add(history); await repo.SaveAsync(default);
            MaintenanceAudit.Record(audit, actor, ticket, history, null); await repo.SaveAsync(default);
            return ticket.Id;
        }));
        var prepared = new Prepared(asset.Id, assignment, ticketId);
        await StageMaintenanceAsync(prepared, finish: false, usable: true);
        return prepared;
    }

    // A known-valid fixture step exercises the shared transaction protocol. Production
    // ticket state machine, scope/version validation and technician checks remain Thien-owned.
    private Task<Outcome> StageMaintenanceAsync(Prepared flow, bool finish, bool usable, bool failAfterSave = false) =>
        AsAsync(provider => provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var db = provider.GetRequiredService<AppDbContext>(); var repo = provider.GetRequiredService<IRepository>();
            var actor = provider.GetRequiredService<IActor>(); var audit = provider.GetRequiredService<IAuditWriter>();
            await repo.LockAssetAsync(flow.AssetId, default);
            var asset = await db.Set<Asset>().SingleAsync(a => a.Id == flow.AssetId);
            var ticket = await db.Set<MaintenanceTicket>().SingleAsync(t => t.Id == flow.TicketId);
            Assert.Equal(finish ? "IN_PROGRESS" : "PENDING", ticket.Status);
            Assert.Equal(finish ? "MAINTENANCE" : "IN_USE", asset.CurrentStatus);
            var before = MaintenanceAuditState.Capture(ticket);
            var assetBefore = new { asset.CurrentStatus, asset.IsArchived };
            var fromStatus = ticket.Status; var now = DateTime.UtcNow;
            var hasAssignment = await db.Set<AssetAssignment>().AnyAsync(a => a.AssetId == asset.Id &&
                !a.IsArchived && a.ReturnedAtUtc == null);
            var destination = finish ? AssetStatusTransitions.AfterMaintenance(usable, hasAssignment) : "MAINTENANCE";
            repo.ExpectVersion(ticket, ticket.RowVersion.ToArray());
            ticket.Status = finish ? usable ? "RESOLVED" : "FAILED" : "IN_PROGRESS";
            if (finish)
            {
                ticket.Resolution = "Private fixture repair outcome"; ticket.ResolvedAtUtc = now; ticket.ActualCost = 81.25m;
            }
            else ticket.StartedAtUtc = now;
            repo.Add(AssetStatusTransitions.Apply(asset, destination, AssetStatusChangeSource.Maintenance,
                actor, now, "Isolated cross-module protocol"));
            var history = new MaintenanceHistory
            {
                MaintenanceTicketId = ticket.Id, EventType = finish ? usable ? "RESOLVED" : "FAILED" : "STATUS_CHANGED",
                FromStatus = fromStatus, ToStatus = ticket.Status, Cost = ticket.ActualCost,
                PerformedAtUtc = now, PerformedByUserId = actor.UserId, CorrelationId = actor.CorrelationId
            };
            repo.Add(history); await repo.SaveAsync(default);
            MaintenanceAudit.Record(audit, actor, ticket, history, before);
            audit.Record(finish ? "maintenance.finish" : "maintenance.start", asset, actor.UserId, "USER",
                before: assetBefore, after: new { asset.CurrentStatus, asset.IsArchived });
            await repo.SaveAsync(default);
            if (failAfterSave) throw new InvalidOperationException("Injected cross-module failure after save.");
            return new Outcome(destination, actor.CorrelationId,
                await db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == asset.Id),
                await db.Set<MaintenanceHistory>().CountAsync(h => h.MaintenanceTicketId == ticket.Id));
        }));

    private Task<AssignmentDto> ReturnAsync(Prepared flow) => AsAsync(provider =>
        provider.GetRequiredService<AssignmentService>().ReturnAsync(flow.Assignment.Id,
            new() { RowVersion = flow.Assignment.RowVersion, Note = "Isolated flow return" }, default));

    private async Task VerifyOutcomeAsync(Prepared flow, Outcome outcome, bool usable, bool returned)
    {
        await ReadAsync(async db =>
        {
            var asset = await db.Set<Asset>().SingleAsync(a => a.Id == flow.AssetId);
            var ticket = await db.Set<MaintenanceTicket>().SingleAsync(t => t.Id == flow.TicketId);
            var assignment = await db.Set<AssetAssignment>().SingleAsync(a => a.Id == flow.Assignment.Id);
            Assert.Equal(outcome.Status, asset.CurrentStatus); Assert.Equal(usable ? "RESOLVED" : "FAILED", ticket.Status);
            Assert.Equal(returned, assignment.ReturnedAtUtc != null); Assert.True(ticket.ResolvedAtUtc >= ticket.StartedAtUtc);
            var assetHistory = await db.Set<AssetStatusHistory>().SingleAsync(h => h.CorrelationId == outcome.Correlation && h.AssetId == asset.Id);
            var ticketHistory = await db.Set<MaintenanceHistory>().SingleAsync(h => h.CorrelationId == outcome.Correlation && h.MaintenanceTicketId == ticket.Id);
            Assert.Equal("MAINTENANCE", assetHistory.FromStatus); Assert.Equal(outcome.Status, assetHistory.ToStatus);
            Assert.Equal("MAINTENANCE", assetHistory.Source); Assert.Equal("IN_PROGRESS", ticketHistory.FromStatus);
            Assert.Equal(ticket.Status, ticketHistory.ToStatus); Assert.Equal(assetHistory.ChangedAtUtc, ticketHistory.PerformedAtUtc);
            Assert.Equal(assetHistory.ChangedByUserId, ticketHistory.PerformedByUserId);
            Assert.Equal(81.25m, ticket.ActualCost); Assert.Equal(ticket.ActualCost, ticketHistory.Cost);
            var logs = await db.Set<AuditLog>().Where(log => log.CorrelationId == outcome.Correlation).ToListAsync();
            Assert.Equal(3, logs.Count);
            Assert.All(logs, log =>
            {
                Assert.Equal(ticketHistory.PerformedByUserId, log.ActorUserId);
                Assert.False(AuditSnapshot.Read(log.NewValuesJson, false).Redacted);
                Assert.DoesNotContain("81.25", log.NewValuesJson!); Assert.DoesNotContain("Private", log.NewValuesJson!);
            });
            Assert.Equal(4, await db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == asset.Id));
            Assert.Equal(3, await db.Set<MaintenanceHistory>().CountAsync(h => h.MaintenanceTicketId == ticket.Id));
            return true;
        });
    }

    [NeonFact]
    public async Task Repair_with_active_assignment_returns_in_use_then_real_return_moves_to_stock()
    {
        var flow = await PrepareAsync(); var outcome = await StageMaintenanceAsync(flow, true, true);
        Assert.Equal("IN_USE", outcome.Status); await VerifyOutcomeAsync(flow, outcome, true, false);
        await ReturnAsync(flow);
        Assert.Equal("IN_STOCK", await ReadAsync(db => db.Set<Asset>().Where(a => a.Id == flow.AssetId).Select(a => a.CurrentStatus).SingleAsync()));
    }

    [NeonFact]
    public async Task Return_during_maintenance_does_not_add_status_history_and_repair_returns_stock()
    {
        var flow = await PrepareAsync(); await ReturnAsync(flow);
        Assert.Equal("MAINTENANCE", await ReadAsync(db => db.Set<Asset>().Where(a => a.Id == flow.AssetId).Select(a => a.CurrentStatus).SingleAsync()));
        Assert.Equal(3, await ReadAsync(db => db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == flow.AssetId)));
        var outcome = await StageMaintenanceAsync(flow, true, true);
        Assert.Equal("IN_STOCK", outcome.Status); await VerifyOutcomeAsync(flow, outcome, true, true);
    }

    [NeonFact]
    public async Task Failed_repair_stays_broken_even_after_real_assignment_return()
    {
        var flow = await PrepareAsync(); var outcome = await StageMaintenanceAsync(flow, true, false);
        Assert.Equal("BROKEN", outcome.Status); await VerifyOutcomeAsync(flow, outcome, false, false);
        await ReturnAsync(flow);
        Assert.Equal("BROKEN", await ReadAsync(db => db.Set<Asset>().Where(a => a.Id == flow.AssetId).Select(a => a.CurrentStatus).SingleAsync()));
        Assert.Equal(4, await ReadAsync(db => db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == flow.AssetId)));
    }

    [NeonFact]
    public async Task Returned_assignment_does_not_override_broken_failure_result()
    {
        var flow = await PrepareAsync(); await ReturnAsync(flow);
        var outcome = await StageMaintenanceAsync(flow, true, false);
        Assert.Equal("BROKEN", outcome.Status); await VerifyOutcomeAsync(flow, outcome, false, true);
    }

    [NeonFact]
    public async Task Failed_completion_save_rolls_back_all_modules_and_explicit_retry_can_finish()
    {
        var flow = await PrepareAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => StageMaintenanceAsync(flow, true, true, true));
        Assert.Equal("Injected cross-module failure after save.", error.Message);
        await ReadAsync(async db =>
        {
            var asset = await db.Set<Asset>().SingleAsync(a => a.Id == flow.AssetId);
            var ticket = await db.Set<MaintenanceTicket>().SingleAsync(t => t.Id == flow.TicketId);
            Assert.Equal("MAINTENANCE", asset.CurrentStatus); Assert.Equal("IN_PROGRESS", ticket.Status);
            Assert.Null(ticket.ActualCost); Assert.Null(ticket.ResolvedAtUtc); Assert.Null(ticket.Resolution);
            Assert.Equal(3, await db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == asset.Id));
            Assert.Equal(2, await db.Set<MaintenanceHistory>().CountAsync(h => h.MaintenanceTicketId == ticket.Id));
            Assert.False(await db.Set<AuditLog>().AnyAsync(log => log.EntityType == "MaintenanceTicket" &&
                log.EntityId == ticket.Id.ToString() && log.Action == "maintenance.resolve"));
            Assert.True(await db.Set<AssetAssignment>().AnyAsync(a => a.Id == flow.Assignment.Id && a.ReturnedAtUtc == null));
            return true;
        });
        var outcome = await StageMaintenanceAsync(flow, true, true);
        await VerifyOutcomeAsync(flow, outcome, true, false);
    }
}
