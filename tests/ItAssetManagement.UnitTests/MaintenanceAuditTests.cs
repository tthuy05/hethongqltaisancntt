using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.UnitTests;

public sealed class MaintenanceAuditTests
{
    private sealed class Actor : IActor
    {
        public long? UserId => 7;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/fixture";
        public bool Has(string permission) => false;
    }
    private sealed class Writer : IAuditWriter
    {
        public int Calls;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null) => Calls++;
    }
    private static MaintenanceTicket Ticket() => new()
    { Id = 1, AssetId = 2, Status = "IN_PROGRESS", AssignedToUserId = 7, ActualCost = 100m, EstimatedCost = 150m };
    private static MaintenanceHistory History(Actor actor) => new()
    {
        Id = 3, MaintenanceTicketId = 1, EventType = "COST_UPDATED", FromStatus = "IN_PROGRESS", ToStatus = "IN_PROGRESS",
        Cost = 200m, PerformedByUserId = 7, CorrelationId = actor.CorrelationId, PerformedAtUtc = DateTime.UtcNow,
        Comment = "private customer diagnosis"
    };

    [Fact]
    public void Capture_is_immutable_and_cost_changes_round_trip_without_amounts_or_free_text()
    {
        var actor = new Actor(); var ticket = Ticket(); var before = MaintenanceAuditState.Capture(ticket);
        ticket.ActualCost = 200m; ticket.EstimatedCost = 175m;
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=offline_audit").Options);
        MaintenanceAudit.Record(new AuditWriter(db, actor), actor, ticket, History(actor), before);
        var logs = db.Set<AuditLog>().Local.ToArray(); Assert.Equal(2, logs.Length);
        Assert.All(logs, log =>
        {
            Assert.Equal("maintenance.cost.update", log.Action); Assert.Equal(actor.CorrelationId, log.CorrelationId);
            Assert.Equal(actor.UserId, log.ActorUserId);
            var safe = AuditSnapshot.Read(log.NewValuesJson, false); Assert.False(safe.Redacted);
            Assert.DoesNotContain("private customer diagnosis", log.NewValuesJson!);
            Assert.DoesNotContain("ActualCost", log.NewValuesJson!); Assert.DoesNotContain("EstimatedCost", log.NewValuesJson!);
            Assert.DoesNotContain("Cost", JsonSerializer.Serialize(before)); // No public financial properties.
        });
        var ticketAudit = Assert.Single(logs, log => log.EntityType == "MaintenanceTicket");
        var values = AuditSnapshot.Read(ticketAudit.NewValuesJson, false).Values!;
        Assert.True(values["actualCostChanged"].GetBoolean()); Assert.True(values["estimatedCostChanged"].GetBoolean());
        Assert.Equal(7, values["technicianId"].GetInt64());
        var historyAudit = Assert.Single(logs, log => log.EntityType == "MaintenanceHistory");
        Assert.True(AuditSnapshot.Read(historyAudit.NewValuesJson, false).Values!["commentProvided"].GetBoolean());
    }

    [Theory]
    [InlineData("actor")][InlineData("correlation")][InlineData("ticket-id")][InlineData("history-id")]
    [InlineData("timestamp")][InlineData("event")][InlineData("snapshot")][InlineData("from-status")]
    [InlineData("to-status")][InlineData("cost")][InlineData("assignee")]
    public void Invalid_history_context_records_no_partial_audit(string mode)
    {
        var actor = new Actor(); var ticket = Ticket(); var before = MaintenanceAuditState.Capture(ticket);
        ticket.ActualCost = 200m; var history = History(actor);
        switch (mode)
        {
            case "actor": history.PerformedByUserId = 8; break;
            case "correlation": history.CorrelationId = Guid.NewGuid(); break;
            case "ticket-id": history.MaintenanceTicketId = 9; break;
            case "history-id": history.Id = 0; break;
            case "timestamp": history.PerformedAtUtc = DateTime.SpecifyKind(history.PerformedAtUtc, DateTimeKind.Unspecified); break;
            case "event": history.EventType = "UNKNOWN"; break;
            case "snapshot": before = MaintenanceAuditState.Capture(new() { Id = 9 }); break;
            case "from-status": history.FromStatus = "PENDING"; break;
            case "to-status": history.ToStatus = "RESOLVED"; break;
            case "cost": history.Cost = 199m; break;
            case "assignee": history.EventType = "ASSIGNED"; history.ToAssignedToUserId = 9; break;
        }
        var writer = new Writer();
        Assert.Throws<InvalidOperationException>(() => MaintenanceAudit.Record(writer, actor, ticket, history, before));
        Assert.Equal(0, writer.Calls);
    }

    [Theory]
    [InlineData("{\"maintenanceStatus\":\"UNKNOWN\"}")]
    [InlineData("{\"maintenancePriority\":\"secret diagnosis\"}")]
    [InlineData("{\"eventType\":\"UNREVIEWED\"}")]
    [InlineData("{\"technicianId\":0}")]
    [InlineData("{\"actualCostChanged\":123}")]
    [InlineData("{\"cost\":200,\"resolution\":\"private\"}")]
    public void Historic_untrusted_maintenance_payload_is_filtered_at_read_boundary(string json)
    {
        var safe = AuditSnapshot.Read(json, true); Assert.True(safe.Redacted); Assert.Empty(safe.Values!);
    }
}
