using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseCapacityPolicyTests
{
    private static SoftwareLicense License(int total = 5, bool active = true, DateTime? starts = null, DateTime? expires = null) =>
        new() { Id = 1, TotalQuantity = total, IsActive = active, StartsAtUtc = starts, ExpiresAtUtc = expires };

    [Theory]
    [InlineData(null, null)]
    [InlineData(1L, 2L)]
    [InlineData(0L, null)]
    [InlineData(-5L, null)]
    [InlineData(null, 0L)]
    [InlineData(null, -1L)]
    public void ValidateTarget_rejects_non_xor_or_non_positive_ids(long? userId, long? assetId)
    {
        Assert.Equal(400, Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.ValidateTarget(userId, assetId)).Status);
    }

    [Fact]
    public void ValidateTarget_accepts_either_user_or_asset_strictly()
    {
        LicenseCapacityPolicy.ValidateTarget(10L, null);
        LicenseCapacityPolicy.ValidateTarget(null, 20L);
    }

    [Fact]
    public void CheckCanAssign_rejects_inactive_or_out_of_window_licenses()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var inactive = License(5, active: false);
        Assert.Equal("LICENSE_INACTIVE", Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanAssign(inactive, 0, now)).Code);

        var future = License(5, starts: now.AddDays(1));
        Assert.Equal("LICENSE_NOT_EFFECTIVE", Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanAssign(future, 0, now)).Code);

        var expired = License(5, expires: now.AddDays(-1));
        Assert.Equal("LICENSE_EXPIRED", Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanAssign(expired, 0, now)).Code);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 6)]
    public void CheckCanAssign_rejects_when_active_seats_reach_or_exceed_capacity(int total, int active)
    {
        var now = DateTime.UtcNow;
        var ex = Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanAssign(License(total), active, now));
        Assert.Equal(409, ex.Status);
        Assert.Equal("LICENSE_CAPACITY_EXHAUSTED", ex.Code);
    }

    [Fact]
    public void CheckCanAssign_allows_assignment_when_seats_available()
    {
        var now = DateTime.UtcNow;
        LicenseCapacityPolicy.CheckCanAssign(License(5), 4, now);
        LicenseCapacityPolicy.CheckCanAssign(License(1), 0, now);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CheckCanUpdateQuantity_rejects_non_positive_quantity(int newQuantity)
    {
        Assert.Equal(400, Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanUpdateQuantity(License(5), newQuantity, 2)).Status);
    }

    [Fact]
    public void CheckCanUpdateQuantity_rejects_reduction_below_active_allocations()
    {
        var ex = Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanUpdateQuantity(License(10), 3, activeCount: 4));
        Assert.Equal(409, ex.Status);
        Assert.Equal("QUANTITY_BELOW_ACTIVE_ALLOCATIONS", ex.Code);
    }

    [Fact]
    public void CheckCanUpdateQuantity_allows_reduction_down_to_active_count_or_increase()
    {
        LicenseCapacityPolicy.CheckCanUpdateQuantity(License(10), 4, activeCount: 4);
        LicenseCapacityPolicy.CheckCanUpdateQuantity(License(5), 15, activeCount: 5);
    }

    [Fact]
    public void CheckCanArchive_blocks_archive_when_active_allocations_exist()
    {
        var ex = Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanArchive(License(5), activeCount: 1));
        Assert.Equal(409, ex.Status);
        Assert.Equal("LICENSE_HAS_ACTIVE_ALLOCATIONS", ex.Code);

        // Allowed when no active allocations
        LicenseCapacityPolicy.CheckCanArchive(License(5), activeCount: 0);
    }

    [Fact]
    public void CheckCanTransfer_rejects_revoked_or_archived_current_assignment()
    {
        var now = DateTime.UtcNow;
        var revoked = new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 10, RevokedAtUtc = now.AddMinutes(-5) };
        Assert.Equal("ASSIGNMENT_NOT_ACTIVE", Assert.Throws<BusinessException>(() =>
            LicenseCapacityPolicy.CheckCanTransfer(License(5), revoked, 20L, null, now)).Code);

        var archived = new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 10, IsArchived = true };
        Assert.Equal("ASSIGNMENT_NOT_ACTIVE", Assert.Throws<BusinessException>(() =>
            LicenseCapacityPolicy.CheckCanTransfer(License(5), archived, 20L, null, now)).Code);
    }

    [Fact]
    public void CheckCanTransfer_rejects_same_target()
    {
        var now = DateTime.UtcNow;
        var current = new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 10 };
        var ex = Assert.Throws<BusinessException>(() =>
            LicenseCapacityPolicy.CheckCanTransfer(License(5), current, 10L, null, now));
        Assert.Equal(400, ex.Status);
        Assert.Equal("SAME_TARGET", ex.Code);
    }

    [Fact]
    public void CheckCanTransfer_allows_transfer_to_different_user_or_asset()
    {
        var now = DateTime.UtcNow;
        var currentUser = new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 10 };
        LicenseCapacityPolicy.CheckCanTransfer(License(5), currentUser, 11L, null, now);
        LicenseCapacityPolicy.CheckCanTransfer(License(5), currentUser, null, 100L, now);

        var currentAsset = new LicenseAssignment { SoftwareLicenseId = 1, AssignedAssetId = 100 };
        LicenseCapacityPolicy.CheckCanTransfer(License(5), currentAsset, 10L, null, now);
        LicenseCapacityPolicy.CheckCanTransfer(License(5), currentAsset, null, 101L, now);
    }

    [Fact]
    public void Transfer_rejects_foreign_license_and_not_yet_effective_license()
    {
        var now = DateTime.UtcNow;
        var foreign = new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 10 };
        Assert.Equal("ASSIGNMENT_LICENSE_MISMATCH", Assert.Throws<BusinessException>(() =>
            LicenseCapacityPolicy.CheckCanTransfer(License(), foreign, 20, null, now)).Code);
        var current = new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 10 };
        Assert.Equal("LICENSE_NOT_EFFECTIVE", Assert.Throws<BusinessException>(() =>
            LicenseCapacityPolicy.CheckCanTransfer(License(starts: now.AddDays(1)), current, 20, null, now)).Code);
    }

    [Fact]
    public void Negative_active_counts_are_rejected_instead_of_authorizing_capacity_changes()
    {
        Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanAssign(License(), -1, DateTime.UtcNow));
        Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanUpdateQuantity(License(), 1, -1));
        Assert.Throws<BusinessException>(() => LicenseCapacityPolicy.CheckCanArchive(License(), -1));
    }
}
