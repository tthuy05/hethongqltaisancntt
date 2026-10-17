using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Proposed capacity protocol invariants for Software License allocation.
// Enforces single-seat capacity, quantity reduction floor, archive guards,
// target XOR, and effective-period boundaries.
public static class LicenseCapacityPolicy
{
    public static void ValidateTarget(long? userId, long? assetId)
    {
        var hasUser = userId is > 0;
        var hasAsset = assetId is > 0;
        if (hasUser == hasAsset)
            throw Validation.Invalid("target", "Phải chọn đúng 1 đối tượng nhận (User hoặc Asset).");
        if (userId is <= 0)
            throw Validation.Invalid("userId", "ID người dùng phải > 0.");
        if (assetId is <= 0)
            throw Validation.Invalid("assetId", "ID tài sản phải > 0.");
    }

    public static void CheckCanAssign(SoftwareLicense license, int activeCount, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(license);
        ValidateActiveCount(activeCount);
        CheckEffective(license, nowUtc);

        if (activeCount >= license.TotalQuantity)
            throw new BusinessException(409, "LICENSE_CAPACITY_EXHAUSTED",
                $"License đã hết chỗ khả dụng ({activeCount}/{license.TotalQuantity}).");
    }

    private static void CheckEffective(SoftwareLicense license, DateTime nowUtc)
    {
        if (!license.IsActive)
            throw new BusinessException(409, "LICENSE_INACTIVE", "License đang ngừng hoạt động.");

        if (license.StartsAtUtc.HasValue && license.StartsAtUtc.Value > nowUtc)
            throw new BusinessException(409, "LICENSE_NOT_EFFECTIVE", "License chưa đến ngày có hiệu lực.");

        if (license.ExpiresAtUtc.HasValue && license.ExpiresAtUtc.Value < nowUtc)
            throw new BusinessException(409, "LICENSE_EXPIRED", "License đã hết hạn sử dụng.");
    }

    public static void CheckCanUpdateQuantity(SoftwareLicense license, int newTotalQuantity, int activeCount)
    {
        ArgumentNullException.ThrowIfNull(license);
        ValidateActiveCount(activeCount);
        if (newTotalQuantity <= 0)
            throw Validation.Invalid("totalQuantity", "Tổng số lượng license phải > 0.");

        if (newTotalQuantity < activeCount)
            throw new BusinessException(409, "QUANTITY_BELOW_ACTIVE_ALLOCATIONS",
                $"Không thể giảm số lượng xuống {newTotalQuantity} vì hiện có {activeCount} chỗ đang được cấp phát.");
    }

    public static void CheckCanArchive(SoftwareLicense license, int activeCount)
    {
        ArgumentNullException.ThrowIfNull(license);
        ValidateActiveCount(activeCount);
        if (activeCount > 0)
            throw new BusinessException(409, "LICENSE_HAS_ACTIVE_ALLOCATIONS",
                $"Không thể lưu trữ license khi còn {activeCount} chỗ đang được cấp phát.");
    }

    public static void CheckCanTransfer(SoftwareLicense license, LicenseAssignment oldAssignment,
        long? newUserId, long? newAssetId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(oldAssignment);

        if (oldAssignment.SoftwareLicenseId != license.Id)
            throw new BusinessException(409, "ASSIGNMENT_LICENSE_MISMATCH", "Bản ghi cấp phát không thuộc license đang điều chuyển.");

        if (oldAssignment.RevokedAtUtc != null || oldAssignment.IsArchived)
            throw new BusinessException(409, "ASSIGNMENT_NOT_ACTIVE", "Bản ghi cấp phát không còn hiệu lực.");

        CheckEffective(license, nowUtc);

        ValidateTarget(newUserId, newAssetId);

        if (newUserId == oldAssignment.AssignedUserId && newAssetId == oldAssignment.AssignedAssetId)
            throw new BusinessException(400, "SAME_TARGET", "Đối tượng nhận mới phải khác đối tượng hiện tại.");
    }

    private static void ValidateActiveCount(int activeCount)
    {
        if (activeCount < 0)
            throw Validation.Invalid("activeCount", "Số chỗ đang cấp phát không được âm.");
    }
}
