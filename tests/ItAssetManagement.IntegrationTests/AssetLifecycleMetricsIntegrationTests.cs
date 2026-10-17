using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Prepared evaluator input query only, not ReplacementService/API. All persisted
// fixtures are unique, retained and restricted to the already prepared isolated DB.
[Collection("isolated-neon")]
public sealed class AssetLifecycleMetricsIntegrationTests(MvpFixture fixture)
{
    private sealed class Actor(long id) : IActor
    {
        public long? UserId => id; public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST"; public string? Path => "/isolated-fixture/lifecycle-metrics";
        public bool Has(string permission) => permission is Permissions.ReplacementEvaluate or Permissions.AssetCost;
    }
    private static readonly DateOnly AssessmentDate = new(2026, 10, 14);
    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }
    private async Task<(long AssetId, long UserId)> AssetAsync(bool incomplete = false)
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var request = fixture.Asset(); request.PurchasePrice = incomplete ? 0m : 1000m;
        request.PurchaseDate = incomplete ? null : new DateOnly(2024, 10, 14);
        request.WarrantyExpirationDate = incomplete ? null : AssessmentDate;
        using var response = await client.PostAsJsonAsync("/api/v1/assets", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<AssetDto>())!.Id, user.Id);
    }

    [NeonFact]
    public async Task PostgreSql_uses_current_ticket_costs_not_repeated_history_snapshots_and_retains_archived_cost()
    {
        var fixtureIds = await AssetAsync(); var actor = new Actor(fixtureIds.UserId);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider; var db = provider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var repo = provider.GetRequiredService<IRepository>(); var writer = new AuditWriter(db, actor);
        await provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            await repo.LockAssetAsync(fixtureIds.AssetId, default); var now = DateTime.UtcNow;
            var tickets = new[] { Ticket("RESOLVED", 100m), Ticket("FAILED", 200m), Ticket("CANCELLED", 25m, true) };
            foreach (var ticket in tickets) repo.Add(ticket); await repo.SaveAsync(default);
            var histories = new List<MaintenanceHistory>();
            // Corrections are snapshots, not expenses: 900 ->500 ->100 on ticket 1.
            foreach (var cost in new[] { 900m, 500m, 100m })
                histories.Add(new() { MaintenanceTicketId = tickets[0].Id, EventType = "COST_UPDATED", Cost = cost,
                    PerformedAtUtc = now.AddMinutes(-3 + histories.Count), PerformedByUserId = actor.UserId, CorrelationId = actor.CorrelationId });
            foreach (var history in histories) repo.Add(history); await repo.SaveAsync(default);
            foreach (var ticket in tickets) writer.Record("fixture.metrics", ticket, actor.UserId, "USER");
            foreach (var history in histories) writer.Record("fixture.metrics", history, actor.UserId, "USER");
            await repo.SaveAsync(default); return true;

            MaintenanceTicket Ticket(string status, decimal cost, bool archived = false) => new()
            {
                AssetId = fixtureIds.AssetId, TicketCode = "MET" + Guid.NewGuid().ToString("N"),
                Title = "Isolated metric fixture", Description = "Synthetic fixture",
                RequestedByUserId = actor.UserId!.Value, AssignedToUserId = actor.UserId,
                OpenedAtUtc = now.AddHours(-1), StartedAtUtc = now.AddMinutes(-30), ResolvedAtUtc = now,
                Resolution = "Fixture outcome", Status = status, ActualCost = cost, IsArchived = archived
            };
        });
        var reader = new AssetLifecycleMetricsReader(repo, actor);
        var metrics = await reader.ReadAsync(fixtureIds.AssetId, AssessmentDate, default);
        Assert.Equal(24, metrics.AgeMonths); Assert.False(metrics.WarrantyExpired);
        Assert.Equal(fixture.TypeId, metrics.Facts.AssetTypeId); Assert.Equal("IN_STOCK", metrics.Facts.CurrentStatus);
        Assert.Equal(3, metrics.Facts.TotalTickets); Assert.Equal(1, metrics.Facts.ResolvedTickets);
        Assert.Equal(1, metrics.Facts.FailedTickets); Assert.Equal(1, metrics.Facts.CancelledTickets);
        Assert.Equal(1, metrics.Facts.ArchivedTickets); Assert.Equal(0, metrics.Facts.MissingCostTickets);
        Assert.Equal(325m, metrics.MaintenanceCostTotal); Assert.Equal(0.325m, metrics.MaintenanceCostRatio);
        Assert.Empty(metrics.InsufficientData); Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(1500m, await db.Set<MaintenanceHistory>().Where(h => h.MaintenanceTicketId ==
            db.Set<MaintenanceTicket>().Where(t => t.AssetId == fixtureIds.AssetId && t.Status == "RESOLVED").Select(t => t.Id).Single()).SumAsync(h => h.Cost));
    }

    [NeonFact]
    public async Task Null_ticket_cost_and_zero_purchase_price_are_not_reported_as_complete_zero_metrics()
    {
        var fixtureIds = await AssetAsync(true); var actor = new Actor(fixtureIds.UserId);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider; var db = provider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var repo = provider.GetRequiredService<IRepository>(); var writer = new AuditWriter(db, actor);
        await provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var ticket = new MaintenanceTicket { AssetId = fixtureIds.AssetId, TicketCode = "MET" + Guid.NewGuid().ToString("N"),
                Title = "Missing cost fixture", Description = "Synthetic", RequestedByUserId = actor.UserId!.Value, OpenedAtUtc = DateTime.UtcNow };
            repo.Add(ticket); await repo.SaveAsync(default); writer.Record("fixture.metrics", ticket, actor.UserId, "USER");
            await repo.SaveAsync(default); return true;
        });
        var reader = new AssetLifecycleMetricsReader(repo, actor);
        var metrics = await reader.ReadAsync(fixtureIds.AssetId, AssessmentDate, default);
        Assert.Equal(1, metrics.Facts.PendingTickets); Assert.Equal(1, metrics.Facts.MissingCostTickets);
        Assert.Equal(0m, metrics.Facts.KnownMaintenanceCost); Assert.Null(metrics.MaintenanceCostTotal);
        Assert.Null(metrics.MaintenanceCostRatio); Assert.Null(metrics.AgeMonths); Assert.Null(metrics.WarrantyExpired);
        Assert.Contains("IncompleteMaintenanceCost", metrics.InsufficientData); Assert.Contains("NonPositivePurchaseCost", metrics.InsufficientData);
        var missing = await Assert.ThrowsAsync<BusinessException>(() => reader.ReadAsync(long.MaxValue, AssessmentDate, default));
        Assert.Equal(404, missing.Status); Assert.False(db.ChangeTracker.HasChanges());
    }
}
