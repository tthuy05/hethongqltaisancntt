using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Shared audit boundary on an opt-in existing isolated schema. No MaintenanceService
// or endpoint is introduced; fixture records remain in the isolated database.
[Collection("isolated-neon")]
public sealed class MaintenanceAuditIntegrationTests(MvpFixture fixture)
{
    private sealed class Actor(long id) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/isolated-fixture/maintenance-audit";
        public bool Has(string permission) => false;
    }

    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }

    [NeonFact]
    public Task Ticket_history_and_audit_commit_with_matching_context_and_safe_snapshots() => ProbeAsync("success");
    [NeonFact]
    public Task Missing_history_audit_prevents_commit_and_rolls_back_both_business_rows() => ProbeAsync("missing-history-audit");
    [NeonFact]
    public Task Failure_after_recording_rolls_back_ticket_history_and_audit_together() => ProbeAsync("after-audit-failure");

    private async Task ProbeAsync(string mode)
    {
        using var client = await fixture.ClientAsync();
        var current = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        using var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var asset = (await response.Content.ReadFromJsonAsync<AssetDto>())!;
        var actor = new Actor(current.Id); var code = "AUD" + Guid.NewGuid().ToString("N");
        long ticketId = 0, historyId = 0;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider; var db = provider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
            var repo = provider.GetRequiredService<IRepository>(); var uow = provider.GetRequiredService<IUnitOfWork>();
            var writer = new AuditWriter(db, actor);
            Task<bool> Execute() => uow.RunAsync(async () =>
            {
                await repo.LockAssetAsync(asset.Id, default);
                var now = DateTime.UtcNow;
                var ticket = new MaintenanceTicket
                {
                    AssetId = asset.Id, TicketCode = code, Title = "Isolated audit boundary",
                    Description = "Private fixture description", RequestedByUserId = current.Id,
                    OpenedAtUtc = now, ActualCost = 42.25m
                };
                repo.Add(ticket); await repo.SaveAsync(default); ticketId = ticket.Id;
                var history = new MaintenanceHistory
                {
                    MaintenanceTicketId = ticket.Id, EventType = "CREATED", ToStatus = "PENDING", Cost = ticket.ActualCost,
                    Comment = "Private fixture diagnosis", PerformedAtUtc = now,
                    PerformedByUserId = current.Id, CorrelationId = actor.CorrelationId
                };
                repo.Add(history); await repo.SaveAsync(default); historyId = history.Id;
                if (mode == "missing-history-audit") writer.Record("maintenance.create", ticket, actor.UserId, "USER");
                else MaintenanceAudit.Record(writer, actor, ticket, history, null);
                await repo.SaveAsync(default);
                if (mode == "after-audit-failure") throw new InvalidOperationException("Isolated injected failure after audit.");
                return true;
            });
            if (mode == "success") Assert.True(await Execute());
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Execute());
                Assert.Equal(mode == "missing-history-audit" ? "Every business write requires a transactional audit event." :
                    "Isolated injected failure after audit.", error.Message);
            }
        }
        await using var verify = fixture.Factory.Services.CreateAsyncScope();
        var verification = verify.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(verification);
        var logs = await verification.Set<AuditLog>().Where(log => log.CorrelationId == actor.CorrelationId).ToListAsync();
        if (mode != "success")
        {
            Assert.False(await verification.Set<MaintenanceTicket>().AnyAsync(t => t.TicketCode == code));
            Assert.False(await verification.Set<MaintenanceHistory>().AnyAsync(h => h.Id == historyId));
            Assert.Empty(logs); return;
        }
        var stored = await verification.Set<MaintenanceHistory>().SingleAsync(h => h.Id == historyId);
        Assert.Equal(ticketId, stored.MaintenanceTicketId); Assert.Equal(actor.CorrelationId, stored.CorrelationId);
        Assert.Equal(actor.UserId, stored.PerformedByUserId); Assert.Equal(42.25m, stored.Cost);
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log =>
        {
            Assert.Equal("maintenance.create", log.Action); Assert.Equal(actor.UserId, log.ActorUserId);
            var safe = AuditSnapshot.Read(log.NewValuesJson, false); Assert.False(safe.Redacted);
            Assert.DoesNotContain("42.25", log.NewValuesJson!); Assert.DoesNotContain("Private", log.NewValuesJson!);
        });
        Assert.Contains(logs, log => log.EntityType == "MaintenanceTicket" && log.EntityId == ticketId.ToString());
        Assert.Contains(logs, log => log.EntityType == "MaintenanceHistory" && log.EntityId == historyId.ToString());
    }
}
