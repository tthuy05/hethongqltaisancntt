using System.Globalization;
using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.Application.Reporting;

public static class ReportingValidation
{
    public static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue) throw new BusinessException(400, "INVALID_QUERY", "Page/offset không hợp lệ.");
        if (pageSize is < 1 or > 100) throw new BusinessException(400, "INVALID_QUERY", "PageSize phải từ 1 đến 100.");
    }

    public static void ValidatePositiveId(long? id, string paramName)
    {
        if (id.HasValue && id.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", $"{paramName} phải là số nguyên dương (> 0).");
    }

    public static void ValidateDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && fromUtc.Value.Kind != DateTimeKind.Utc || toUtc.HasValue && toUtc.Value.Kind != DateTimeKind.Utc)
            throw new BusinessException(400, "INVALID_QUERY", "Dùng thời gian ISO UTC có hậu tố Z.");
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Thời điểm bắt đầu không được lớn hơn thời điểm kết thúc.");
    }

    public static void Choice(string? value, params string[] choices)
    {
        if (value != null && !choices.Contains(value)) throw new BusinessException(400, "INVALID_QUERY", "Giá trị bộ lọc không hỗ trợ.");
    }
}

// ---------------------------------------------------------
// Dashboard Queries & DTOs (EP-072 – EP-075)
// ---------------------------------------------------------

public sealed class DashboardSummaryQuery
{
    public long? DepartmentId { get; set; }
    public void Validate() => ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
}

public sealed class DashboardAssetGroupQuery
{
    public string GroupBy { get; set; } = "status"; // "department" | "type" | "status"
    public long? DepartmentId { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        if (string.IsNullOrWhiteSpace(GroupBy) ||
            GroupBy.ToLowerInvariant() is not ("department" or "type" or "status"))
        {
            throw new BusinessException(400, "INVALID_QUERY", "GroupBy chỉ chấp nhận: department, type, status.");
        }
    }
}

public sealed class DashboardAlertsQuery
{
    public long? DepartmentId { get; set; }
    public int DaysAhead { get; set; } = 30;

    public void Validate()
    {
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        if (DaysAhead is < 1 or > 365)
            throw new BusinessException(400, "INVALID_QUERY", "DaysAhead phải từ 1 đến 365 ngày.");
    }
}

public sealed class DashboardCostsQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? DepartmentId { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
    }
}

public sealed record DashboardSummaryDto(
    int TotalAssets,
    int InUse,
    int Maintenance,
    int InStock,
    int OpenTickets,
    int? ActiveLicenses,
    int PendingReplacements,
    IReadOnlyList<DashboardSeriesDto> ByStatus,
    IReadOnlyList<RecentAssetDto> RecentAssets,
    DateTime AsOfUtc
);

public sealed record RecentAssetDto(
    long Id,
    string AssetCode,
    string Name,
    string AssetTypeName,
    string DepartmentName,
    string Status,
    DateTime CreatedAtUtc
);

public sealed record DashboardSeriesDto(
    string Key,
    string Label,
    int Count,
    decimal? Value
);

public sealed record DashboardAlertsDto(
    int ExpiringWarrantiesCount,
    IReadOnlyList<AssetAlertItemDto> ExpiringWarranties,
    int ExpiringLicensesCount,
    IReadOnlyList<LicenseAlertItemDto>? ExpiringLicenses,
    int OpenTicketsCount,
    int HighPriorityTicketsCount,
    int OverdueTicketsCount,
    IReadOnlyList<TicketAlertItemDto> OpenTickets
);

public sealed record AssetAlertItemDto(
    long AssetId,
    string AssetCode,
    string Name,
    string DepartmentName,
    DateOnly? WarrantyEndDate,
    int DaysRemaining
);

public sealed record LicenseAlertItemDto(
    long LicenseId,
    string SoftwareName,
    string LicenseCode,
    string LicenseType,
    string? LicenseKeyLast4,
    DateTime? ExpiresAtUtc,
    int DaysRemaining,
    int AvailableSeats
);

public sealed record TicketAlertItemDto(
    long TicketId,
    string TicketCode,
    string Title,
    string Priority,
    string Status,
    DateTime? DueAtUtc,
    bool IsOverdue
);

public sealed record DashboardCostSummaryDto(
    string Currency,
    decimal? TotalAssetValue,
    decimal? MaintenanceCostYtd,
    decimal? TotalBudgetForecast,
    int MissingEstimateCount,
    IReadOnlyList<MonthlyCostDto> MonthlyMaintenanceCosts,
    IReadOnlyList<PriorityBudgetDto> BudgetByPriority
);

public sealed record MonthlyCostDto(int Year, int Month, decimal Cost);
public sealed record PriorityBudgetDto(string Priority, int Count, decimal TotalCost);

// ---------------------------------------------------------
// Report Queries & DTOs (EP-076 – EP-086)
// ---------------------------------------------------------

public sealed class AssetInventoryReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public long? AssetTypeId { get; set; }
    public long? DepartmentId { get; set; }
    public string? Status { get; set; }
    public DateOnly? PurchasedFrom { get; set; }
    public DateOnly? PurchasedTo { get; set; }
    public bool IncludeArchived { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";

    public void Validate()
    {
        ReportingValidation.ValidatePage(Page, PageSize);
        ReportingValidation.ValidatePositiveId(AssetTypeId, nameof(AssetTypeId));
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.Choice(Status, "IN_STOCK", "IN_USE", "MAINTENANCE", "BROKEN", "RETIRED");
        if (Keyword?.Length > 200 || SortBy != null && !new[] { "assetcode", "name", "purchasedate", "purchasecost", "createdat" }.Contains(SortBy.ToLowerInvariant()) || SortDirection is not ("asc" or "desc"))
            throw new BusinessException(400, "INVALID_QUERY", "Từ khóa hoặc sắp xếp không hỗ trợ.");
        if (PurchasedFrom.HasValue && PurchasedTo.HasValue && PurchasedFrom.Value > PurchasedTo.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Ngày mua bắt đầu không được lớn hơn ngày kết thúc.");
    }
}

public sealed record AssetInventoryRowDto(
    long Id,
    string AssetCode,
    string Name,
    string AssetTypeName,
    string DepartmentName,
    string CurrentStatus,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyEndDate,
    string? SerialNumber,
    bool IsArchived
);

public sealed class ReportGroupQuery
{
    public string? Status { get; set; }
    public long? AssetTypeId { get; set; }
    public long? DepartmentId { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePositiveId(AssetTypeId, nameof(AssetTypeId));
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.Choice(Status, "IN_STOCK", "IN_USE", "MAINTENANCE", "BROKEN", "RETIRED");
    }
}

public sealed class AssignmentReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? AssetId { get; set; }
    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }
    public bool IncludeArchived { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePage(Page, PageSize);
        ReportingValidation.ValidatePositiveId(AssetId, nameof(AssetId));
        ReportingValidation.ValidatePositiveId(UserId, nameof(UserId));
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
    }
}

public sealed record AssignmentReportRowDto(
    long Id,
    long AssetId,
    string AssetCode,
    string AssetName,
    string? AssignedUserName,
    string? AssignedDepartmentName,
    DateTime AssignedAtUtc,
    DateTime? ReturnedAtUtc,
    string Status
);

public sealed class MaintenanceReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? AssetId { get; set; }
    public long? TechnicianId { get; set; }
    public string? Status { get; set; }
    public bool IncludeArchived { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePage(Page, PageSize);
        ReportingValidation.ValidatePositiveId(AssetId, nameof(AssetId));
        ReportingValidation.ValidatePositiveId(TechnicianId, nameof(TechnicianId));
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        ReportingValidation.Choice(Status, "PENDING", "IN_PROGRESS", "RESOLVED", "FAILED", "CANCELLED");
    }
}

public sealed record MaintenanceReportRowDto(
    long Id,
    string TicketCode,
    long AssetId,
    string AssetCode,
    string Title,
    string Priority,
    string Status,
    string? TechnicianName,
    DateTime OpenedAtUtc,
    DateTime? ResolvedAtUtc,
    decimal? Cost
);

public sealed class MaintenanceCostReportQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? DepartmentId { get; set; }
    public long? AssetTypeId { get; set; }
    public string GroupBy { get; set; } = "month"; // "month" | "type" | "department"

    public void Validate()
    {
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.ValidatePositiveId(AssetTypeId, nameof(AssetTypeId));
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        if (string.IsNullOrWhiteSpace(GroupBy) || GroupBy.ToLowerInvariant() is not ("month" or "type" or "department"))
            throw new BusinessException(400, "INVALID_QUERY", "GroupBy chỉ chấp nhận: month, type, department.");
    }
}

public sealed record CostReportDto(
    string Currency,
    decimal TotalCost,
    IReadOnlyList<CostGroupItemDto> Groups
);

public sealed record CostGroupItemDto(
    string GroupKey,
    string GroupLabel,
    int TicketCount,
    decimal TotalCost
);

public sealed class WarrantyExpiryReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long? DepartmentId { get; set; }
    public long? AssetTypeId { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePage(Page, PageSize);
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.ValidatePositiveId(AssetTypeId, nameof(AssetTypeId));
        if (FromDate.HasValue && ToDate.HasValue && FromDate.Value > ToDate.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Ngày bắt đầu không được lớn hơn ngày kết thúc.");
    }
}

public sealed record WarrantyReportRowDto(
    long Id,
    string AssetCode,
    string Name,
    string AssetTypeName,
    string DepartmentName,
    DateOnly? WarrantyEndDate,
    int DaysRemaining,
    bool IsExpired
);

public sealed class LicenseExpiryReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? SoftwareId { get; set; }
    public string? Status { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePage(Page, PageSize);
        ReportingValidation.ValidatePositiveId(SoftwareId, nameof(SoftwareId));
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        ReportingValidation.Choice(Status, "Expired", "Expiring", "Valid");
    }
}

public sealed record LicenseExpiryRowDto(
    long Id,
    long SoftwareId,
    string SoftwareName,
    string LicenseCode,
    string LicenseType,
    int TotalQuantity,
    int ActiveSeats,
    string? LicenseKeyLast4,
    DateTime? ExpiresAtUtc,
    int DaysRemaining,
    bool IsExpired
);

public sealed class ReplacementReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? GeneratedFromUtc { get; set; }
    public DateTime? GeneratedToUtc { get; set; }
    public long? DepartmentId { get; set; }
    public string? Priority { get; set; }
    public string? Disposition { get; set; }
    public short? PlannedYear { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidatePage(Page, PageSize);
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.ValidateDateRange(GeneratedFromUtc, GeneratedToUtc);
        ReportingValidation.Choice(Priority, "LOW", "MEDIUM", "HIGH", "CRITICAL");
        ReportingValidation.Choice(Disposition, "ACTIVE", "PLANNED", "DISMISSED", "SUPERSEDED");
        if (PlannedYear is < 2000 or > 2100) throw new BusinessException(400, "INVALID_QUERY", "Năm không hợp lệ.");
    }
}

public sealed record ReplacementReportRowDto(
    long Id,
    long AssetId,
    string AssetCode,
    string AssetName,
    string DepartmentName,
    string RuleCode,
    string Priority,
    string Disposition,
    decimal? Score,
    decimal? EstimatedCost,
    short? PlannedYear,
    bool IsCurrent,
    DateTime RecommendedAtUtc
);

public sealed class ReplacementBudgetReportQuery
{
    public int Year { get; set; }
    public long? DepartmentId { get; set; }
    public string? Priority { get; set; }

    public void Validate()
    {
        if (Year is < 2000 or > 2100)
            throw new BusinessException(400, "INVALID_QUERY", "Năm ngân sách phải từ 2000 đến 2100.");
        ReportingValidation.ValidatePositiveId(DepartmentId, nameof(DepartmentId));
        ReportingValidation.Choice(Priority, "LOW", "MEDIUM", "HIGH", "CRITICAL");
    }
}

public sealed record ReplacementBudgetReportDto(
    int Year,
    string Currency,
    int TotalRecommendations,
    decimal TotalBudget,
    int MissingEstimateCount,
    IReadOnlyList<PriorityBudgetDto> ByPriority
);
