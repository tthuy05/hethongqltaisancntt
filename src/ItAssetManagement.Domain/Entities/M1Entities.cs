namespace ItAssetManagement.Domain.Entities;

// Persistence shapes from Schema Baseline V1. M1 services exist; Week 4–7 workflows remain PLANNED.
public sealed class Department
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public long? ParentDepartmentId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class User
{
    public long Id { get; set; }
    public string Username { get; set; } = null!;
    public string NormalizedUsername { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? EmployeeCode { get; set; }
    public long? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdminLocked { get; set; }
    public DateTime? AdminLockedAtUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public int TokenVersion { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class Role
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class UserRole
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public long? AssignedByUserId { get; set; }
}

public sealed class Permission
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Module { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class RolePermission
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public long? GrantedByUserId { get; set; }
}

public sealed class AssetType
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? DefaultUsefulLifeMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class Asset
{
    public long Id { get; set; }
    public string AssetCode { get; set; } = null!;
    public long AssetTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public DateOnly? WarrantyEndDate { get; set; }
    public long OwningDepartmentId { get; set; }
    public string CurrentStatus { get; set; } = "IN_STOCK";
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class AssetStatusHistory
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? Reason { get; set; }
    public string Source { get; set; } = null!;
    public DateTime ChangedAtUtc { get; set; }
    public long? ChangedByUserId { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public long? ActorUserId { get; set; }
    public string ActorType { get; set; } = "USER";
    public string Action { get; set; } = null!;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string Outcome { get; set; } = null!;
    public Guid CorrelationId { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? MetadataJson { get; set; }
    public string? FailureReasonCode { get; set; }
    public byte[]? PreviousEntryHash { get; set; }
    public byte[]? EntryHash { get; set; }
}
