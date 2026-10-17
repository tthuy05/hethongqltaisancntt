using System.Text.Json;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public enum MaintenanceTicketAction { Read, Update, Work, Resolve, Cancel }

/// <summary>
/// Prepared Week 4 policy, not an enabled endpoint. Construct only from CurrentUser
/// refreshed by server-side authentication, never from a request DTO or stale JWT roles.
/// Queue access defaults to denied until an explicit queue policy has been reviewed.
/// </summary>
public sealed class MaintenanceAccessPolicy
{
    private readonly long userId;
    private readonly HashSet<string> permissions;
    private readonly bool globalScope;

    public MaintenanceAccessPolicy(CurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0) throw Forbidden();
        userId = user.Id;
        permissions = new(user.Permissions, StringComparer.Ordinal);
        // Baseline full scope is reserved to these roles; a custom group or a cost
        // grant alone must not silently confer access to every technician's tickets.
        globalScope = user.Roles.Contains("ADMIN_IT", StringComparer.Ordinal) ||
            user.Roles.Contains("SYSTEM_MANAGER", StringComparer.Ordinal);
    }

    public IQueryable<MaintenanceTicket> ScopeRead(IQueryable<MaintenanceTicket> tickets)
    {
        RequirePermission(Permissions.MaintenanceRead);
        return globalScope ? tickets : tickets.Where(ticket => ticket.AssignedToUserId == userId);
    }

    public void Require(MaintenanceTicket ticket, MaintenanceTicketAction action)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        RequirePermission(Permissions.MaintenanceRead);
        RequirePermission(action switch
        {
            MaintenanceTicketAction.Read => Permissions.MaintenanceRead,
            MaintenanceTicketAction.Update => Permissions.MaintenanceUpdate,
            MaintenanceTicketAction.Work => Permissions.MaintenanceWork,
            MaintenanceTicketAction.Resolve => Permissions.MaintenanceResolve,
            MaintenanceTicketAction.Cancel => Permissions.MaintenanceCancel,
            _ => throw Forbidden()
        });
        if (!globalScope && ticket.AssignedToUserId != userId) throw Forbidden();
    }

    public void RequireCreate()
    {
        RequirePermission(Permissions.MaintenanceRead);
        RequirePermission(Permissions.MaintenanceCreate);
        // The service must separately check visibility/eligibility of the target asset.
    }

    public void RequireAssign(MaintenanceTicket ticket, long technicianId)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        RequirePermission(Permissions.MaintenanceRead);
        RequirePermission(Permissions.MaintenanceAssign);
        // No general self-claim of all unassigned tickets. Queue policy is not ready.
        if (!globalScope) throw Forbidden();
        if (technicianId <= 0) throw Validation.Invalid("technicianId", "ID phải > 0.");
        // Technician eligibility, ticket state, concurrency and row locking are caller-owned.
    }

    public bool CanReadCost => globalScope && permissions.Contains(Permissions.MaintenanceRead) &&
        permissions.Contains(Permissions.MaintenanceCostWrite);

    public void RequireCostWrite(JsonElement requestBody)
    {
        RequirePermission(Permissions.MaintenanceRead);
        if (requestBody.ValueKind != JsonValueKind.Object)
            throw Validation.Invalid("body", "Request phải là JSON object.");
        // Check presence, not decimal.HasValue: null, zero, duplicate and case variants
        // must not let a scoped technician smuggle financial fields through binding.
        if (requestBody.EnumerateObject().Any(field => IsCostField(field.Name)) && !CanReadCost)
            throw Forbidden();
        // DTO binding must still reject unknown fields/invalid numbers and validate
        // nonnegative precision/scale. This helper is authorization, not a JSON validator.
    }

    public MaintenanceCostView? ReadCosts(MaintenanceTicket ticket)
    {
        Require(ticket, MaintenanceTicketAction.Read);
        return CanReadCost ? new(ticket.EstimatedCost, ticket.ActualCost) : null;
    }

    private static bool IsCostField(string name) =>
        name.Equals("cost", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("estimatedCost", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("actualCost", StringComparison.OrdinalIgnoreCase);

    private void RequirePermission(string permission)
    { if (!permissions.Contains(permission)) throw Forbidden(); }

    private static BusinessException Forbidden() => new(403, "FORBIDDEN", "Không có quyền thực hiện thao tác bảo trì.");
}

// Service-side projection only. Do not serialize MaintenanceTicket entities directly.
public sealed record MaintenanceCostView(decimal? EstimatedCost, decimal? ActualCost);
