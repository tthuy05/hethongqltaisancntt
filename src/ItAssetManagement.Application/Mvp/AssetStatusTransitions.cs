using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public enum AssetStatusChangeSource { Assignment, Maintenance, Admin }

/// <summary>
/// Shared BR-007 transition/history contract. This only changes the tracked entity
/// in memory; the caller must hold the asset row lock inside an audited unit of work,
/// enforce workflow/permission/version checks, add the returned history and save its
/// business changes plus audit together. It never opens or commits a transaction.
/// </summary>
public static class AssetStatusTransitions
{
    public static string AfterMaintenance(bool usable, bool hasActiveAssignment) =>
        !usable ? "BROKEN" : hasActiveAssignment ? "IN_USE" : "IN_STOCK";

    public static AssetStatusHistory Apply(Asset asset, string toStatus, AssetStatusChangeSource source,
        IActor actor, DateTime changedAtUtc, string reason)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.UserId is not > 0)
            throw new BusinessException(403, "FORBIDDEN", "Thay đổi trạng thái yêu cầu người thực hiện hợp lệ.");
        if (asset.Id <= 0 || actor.CorrelationId == Guid.Empty)
            throw new InvalidOperationException("A persisted asset and request correlation are required.");
        if (changedAtUtc.Kind != DateTimeKind.Utc)
            throw Validation.Invalid("changedAt", "Thời điểm phải là UTC.");
        var normalizedReason = Validation.Required(reason, 1000, "reason");
        if (asset.IsArchived)
            throw new BusinessException(409, "ASSET_ARCHIVED", "Không thể đổi trạng thái tài sản đã archive.");

        var allowed = (source, asset.CurrentStatus, toStatus) switch
        {
            (AssetStatusChangeSource.Assignment, "IN_STOCK", "IN_USE") => true,
            (AssetStatusChangeSource.Assignment, "IN_USE", "IN_STOCK") => true,
            (AssetStatusChangeSource.Maintenance, "IN_STOCK" or "IN_USE" or "BROKEN", "MAINTENANCE") => true,
            (AssetStatusChangeSource.Maintenance, "MAINTENANCE", "IN_STOCK" or "IN_USE" or "BROKEN") => true,
            (AssetStatusChangeSource.Admin, "IN_STOCK" or "BROKEN", "RETIRED") => true,
            _ => false
        };
        if (!allowed)
            throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Chuyển trạng thái không hợp lệ.");

        // Validate everything before touching the tracked entity. Preserve RowVersion;
        // AppDbContext owns concurrency-token generation when SaveChanges succeeds.
        var history = new AssetStatusHistory
        {
            AssetId = asset.Id, FromStatus = asset.CurrentStatus, ToStatus = toStatus,
            Source = source switch
            {
                AssetStatusChangeSource.Assignment => "ASSIGNMENT",
                AssetStatusChangeSource.Maintenance => "MAINTENANCE",
                _ => "ADMIN"
            },
            ChangedAtUtc = changedAtUtc, ChangedByUserId = actor.UserId,
            CorrelationId = actor.CorrelationId, Reason = normalizedReason
        };
        asset.CurrentStatus = toStatus;
        asset.UpdatedByUserId = actor.UserId;
        return history;
    }
}
