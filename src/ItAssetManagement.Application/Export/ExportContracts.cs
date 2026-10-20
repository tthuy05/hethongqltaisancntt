using ItAssetManagement.Application.Import;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;

namespace ItAssetManagement.Application.Export;

/// <summary>
/// Week 7 — EP-088 đến EP-092: Cấu hình và hợp đồng xuất dữ liệu an toàn.
/// Tuân thủ BR-018, BR-032, BR-047, BR-051, BR-052, BR-053.
/// </summary>
public static class ExportSecurityLimits
{
    public const int MaxExportRows = 5000;
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Tạo tên tệp xuất khẩu an toàn, chỉ chứa ký tự chữ cái, số, dấu gạch nối và dấu gạch dưới.
    /// Chống Path Traversal và Header Injection trong Content-Disposition.
    /// </summary>
    public static string GenerateSafeFilename(string prefix)
    {
        var cleanPrefix = string.Concat(prefix.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        if (string.IsNullOrWhiteSpace(cleanPrefix)) cleanPrefix = "export";
        return $"{cleanPrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
    }

    /// <summary>
    /// Chuẩn hóa giá trị ô xuất dữ liệu: vô hiệu hóa công thức độc hại (Spreadsheet Formula Injection).
    /// </summary>
    public static string SanitizeExportCell(string? rawValue)
    {
        if (string.IsNullOrEmpty(rawValue)) return "";
        return ImportSecurityLimits.NeutralizeFormula(rawValue) ?? "";
    }
}

/// <summary>
/// DTO bộ lọc xuất dữ liệu tài sản (EP-088).
/// </summary>
public sealed class AssetExportQuery
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public long? AssetTypeId { get; set; }
    public long? DepartmentId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";

    public void Validate()
    {
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        ReportingValidation.Choice(Status, "InStock", "InUse", "Maintenance", "Broken", "Retired");
        ReportingValidation.Choice(SortBy, "assetCode", "name", "createdAt");
        ReportingValidation.Choice(SortDirection, "asc", "desc");
        if (Keyword?.Length > 200) throw new BusinessException(400, "INVALID_QUERY", "Từ khóa quá dài.");
        if (DepartmentId.HasValue && DepartmentId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "DepartmentId phải là số nguyên dương.");
        if (AssetTypeId.HasValue && AssetTypeId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "AssetTypeId phải là số nguyên dương.");
        if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value > ToUtc.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Thời điểm bắt đầu không được lớn hơn thời điểm kết thúc.");
    }
}

/// <summary>
/// DTO bộ lọc xuất lịch sử bảo trì (EP-089).
/// </summary>
public sealed class MaintenanceExportQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? AssetId { get; set; }
    public long? TechnicianId { get; set; }
    public string? Status { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        ReportingValidation.Choice(Status, "PENDING", "IN_PROGRESS", "RESOLVED", "FAILED", "CANCELLED");
        if (AssetId.HasValue && AssetId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "AssetId phải là số nguyên dương.");
        if (TechnicianId.HasValue && TechnicianId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "TechnicianId phải là số nguyên dương.");
        if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value > ToUtc.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Thời điểm bắt đầu không được lớn hơn thời điểm kết thúc.");
    }
}

/// <summary>
/// DTO bộ lọc xuất lịch sử phân bổ (EP-090).
/// </summary>
public sealed class AssignmentExportQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? AssetId { get; set; }
    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        if (AssetId.HasValue && AssetId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "AssetId phải là số nguyên dương.");
        if (UserId.HasValue && UserId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "UserId phải là số nguyên dương.");
        if (DepartmentId.HasValue && DepartmentId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "DepartmentId phải là số nguyên dương.");
        if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value > ToUtc.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Thời điểm bắt đầu không được lớn hơn thời điểm kết thúc.");
    }
}

/// <summary>
/// DTO bộ lọc xuất báo cáo hạn bảo hành (EP-091).
/// </summary>
public sealed class WarrantyExportQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long? DepartmentId { get; set; }
    public long? AssetTypeId { get; set; }

    public void Validate()
    {
        ReportingValidation.ValidateDateRange(FromUtc, ToUtc);
        if (DepartmentId.HasValue && DepartmentId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "DepartmentId phải là số nguyên dương.");
        if (AssetTypeId.HasValue && AssetTypeId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "AssetTypeId phải là số nguyên dương.");
        if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value > ToUtc.Value)
            throw new BusinessException(400, "INVALID_QUERY", "Thời điểm bắt đầu không được lớn hơn thời điểm kết thúc.");
    }
}

/// <summary>
/// DTO bộ lọc xuất báo cáo đề xuất thay thế & ngân sách (EP-092).
/// </summary>
public sealed class ReplacementExportQuery
{
    public int? Year { get; set; }
    public long? DepartmentId { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }

    public void Validate()
    {
        ReportingValidation.Choice(Priority, "LOW", "MEDIUM", "HIGH", "CRITICAL");
        ReportingValidation.Choice(Status, "ACTIVE", "PLANNED", "DISMISSED", "SUPERSEDED");
        if (Year.HasValue && Year.Value is < 2000 or > 2100)
            throw new BusinessException(400, "INVALID_QUERY", "Năm phải từ 2000 trở đi.");
        if (DepartmentId.HasValue && DepartmentId.Value <= 0)
            throw new BusinessException(400, "INVALID_QUERY", "DepartmentId phải là số nguyên dương.");
    }
}
