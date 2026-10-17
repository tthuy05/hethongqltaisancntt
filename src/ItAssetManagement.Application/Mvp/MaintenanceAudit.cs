using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Immutable command-local capture; financial values stay internal and are never
// serialized into AuditLog. Capture before changing the tracked ticket.
public sealed class MaintenanceAuditState
{
    internal long TicketId { get; }
    internal long AssetId { get; }
    internal string Status { get; }
    internal string Priority { get; }
    internal long? TechnicianId { get; }
    internal bool IsArchived { get; }
    internal decimal? EstimatedCost { get; }
    internal decimal? ActualCost { get; }
    private MaintenanceAuditState(MaintenanceTicket ticket)
    {
        TicketId = ticket.Id; AssetId = ticket.AssetId; Status = ticket.Status; Priority = ticket.Priority;
        TechnicianId = ticket.AssignedToUserId; IsArchived = ticket.IsArchived;
        EstimatedCost = ticket.EstimatedCost; ActualCost = ticket.ActualCost;
    }
    public static MaintenanceAuditState Capture(MaintenanceTicket ticket) => new(ticket);
    internal object Snapshot() => new
    {
        maintenanceTicketId = TicketId, assetId = AssetId, maintenanceStatus = Status,
        maintenancePriority = Priority, technicianId = TechnicianId, isArchived = IsArchived
    };
}

/// <summary>
/// Shared audit integration boundary. Caller owns authorization, history creation,
/// workflow validation and transaction. Save new identities before recording, then
/// save these audit entries in the same audited unit of work; never commit separately.
/// </summary>
public static class MaintenanceAudit
{
    public static void Record(IAuditWriter audit, IActor actor, MaintenanceTicket ticket,
        MaintenanceHistory history, MaintenanceAuditState? before)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(history);
        var action = history.EventType switch
        {
            "CREATED" => "maintenance.create", "STATUS_CHANGED" => "maintenance.status.change",
            "ASSIGNED" => "maintenance.assign", "COMMENTED" => "maintenance.comment",
            "COST_UPDATED" => "maintenance.cost.update", "RESOLVED" => "maintenance.resolve",
            "FAILED" => "maintenance.fail", "CANCELLED" => "maintenance.cancel",
            _ => throw new InvalidOperationException("Unknown maintenance history event.")
        };
        if (ticket.Id <= 0 || ticket.AssetId <= 0 || history.Id <= 0 || history.MaintenanceTicketId != ticket.Id ||
            actor.UserId is not > 0 || history.PerformedByUserId != actor.UserId ||
            actor.CorrelationId == Guid.Empty || history.CorrelationId != actor.CorrelationId ||
            history.PerformedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Maintenance audit/history identity, actor or correlation mismatch.");
        if ((history.EventType == "CREATED") != (before is null) || before is not null &&
            (before.TicketId != ticket.Id || before.AssetId != ticket.AssetId))
            throw new InvalidOperationException("Maintenance audit requires the matching pre-command snapshot.");
        if (history.FromStatus is not null && history.FromStatus != before?.Status ||
            history.ToStatus is not null && history.ToStatus != ticket.Status ||
            history.EventType == "ASSIGNED" && (history.FromAssignedToUserId != before?.TechnicianId ||
                history.ToAssignedToUserId != ticket.AssignedToUserId) ||
            history.EventType == "COST_UPDATED" && history.Cost != ticket.ActualCost)
            throw new InvalidOperationException("Maintenance history does not match the command snapshot.");

        // Record both entities: MaintenanceHistory participates in audited-write
        // coverage, unlike AssetStatusHistory's special asset coverage guard.
        audit.Record(action, ticket, actor.UserId, "USER", before: before?.Snapshot(),
            after: new
            {
                maintenanceTicketId = ticket.Id, assetId = ticket.AssetId, maintenanceStatus = ticket.Status,
                maintenancePriority = ticket.Priority, technicianId = ticket.AssignedToUserId, ticket.IsArchived,
                estimatedCostChanged = before?.EstimatedCost != ticket.EstimatedCost,
                actualCostChanged = before?.ActualCost != ticket.ActualCost
            });
        audit.Record(action, history, actor.UserId, "USER", after: new
        {
            maintenanceTicketId = ticket.Id, eventType = history.EventType,
            performedAtUtc = history.PerformedAtUtc, commentProvided = !string.IsNullOrWhiteSpace(history.Comment)
        });
        // Asset mutations require their own audit entry from the calling workflow.
    }
}
