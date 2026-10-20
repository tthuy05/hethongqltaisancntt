namespace ItAssetManagement.Domain.Entities;

// Schema Baseline V1 persistence shapes. Module services/API remain PLANNED.
public sealed class MaintenanceHistory
{
    public long Id { get; set; }
    public long MaintenanceTicketId { get; set; }
    public string EventType { get; set; } = null!;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public long? FromAssignedToUserId { get; set; }
    public long? ToAssignedToUserId { get; set; }
    public string? Comment { get; set; }
    public decimal? Cost { get; set; }
    public long? PerformedByUserId { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class Software
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Publisher { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class SoftwareLicense
{
    public long Id { get; set; }
    public long SoftwareId { get; set; }
    public string LicenseCode { get; set; } = null!;
    public string? Vendor { get; set; }
    public string LicenseType { get; set; } = null!;
    public int TotalQuantity { get; set; }
    public byte[]? LicenseKeyCiphertext { get; set; }
    public string? LicenseKeyLast4 { get; set; }
    public string? KeyVersion { get; set; }
    public DateTime? PurchasedAtUtc { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class LicenseAssignment
{
    public long Id { get; set; }
    public long SoftwareLicenseId { get; set; }
    public long? AssignedUserId { get; set; }
    public long? AssignedAssetId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public long AssignedByUserId { get; set; }
    public long? RevokedByUserId { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class ReplacementRule
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public int Version { get; set; } = 1;
    public string Name { get; set; } = null!;
    public long? AssetTypeId { get; set; }
    public int? MinimumAgeMonths { get; set; }
    public decimal? MaximumMaintenanceCostRatio { get; set; }
    public int? MinimumMaintenanceCount { get; set; }
    public bool RequireWarrantyExpired { get; set; }
    public int? MinimumFailureCount { get; set; }
    public decimal? EstimatedUnitCost { get; set; }
    public int Priority { get; set; } = 100;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}

public sealed class ReplacementRecommendation
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long ReplacementRuleId { get; set; }
    public string Disposition { get; set; } = "ACTIVE";
    public bool IsCurrent { get; set; } = true;
    public string Priority { get; set; } = null!;
    public decimal? Score { get; set; }
    public string Reason { get; set; } = null!;
    public decimal? EstimatedReplacementCost { get; set; }
    public short? PlannedReplacementYear { get; set; }
    public string EvaluationSnapshotJson { get; set; } = null!;
    public DateTime RecommendedAtUtc { get; set; }
    public DateTime? DispositionAtUtc { get; set; }
    public long? DispositionByUserId { get; set; }
    public string? DispositionNote { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
