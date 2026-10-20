using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/reports")]
public sealed class ReportsController(ReportQueryService service) : ControllerBase
{
    // EP-076: GET /api/v1/reports/asset-inventory
    [HttpGet("asset-inventory"), Authorize]
    public Task<PagedResponse<AssetInventoryRowDto>> AssetInventory([FromQuery] AssetInventoryReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "keyword", "status", "sortBy", "sortDirection", "assetTypeId", "departmentId", "purchasedFrom", "purchasedTo", "includeArchived");
        return service.GetAssetInventoryReportAsync(query, ct);
    }

    // EP-077: GET /api/v1/reports/assets-by-department
    [HttpGet("assets-by-department"), Authorize]
    public Task<IReadOnlyList<DashboardSeriesDto>> AssetsByDepartment([FromQuery] ReportGroupQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "status", "assetTypeId", "departmentId");
        return service.GetAssetsByDepartmentReportAsync(query, ct);
    }

    // EP-078: GET /api/v1/reports/assets-by-type
    [HttpGet("assets-by-type"), Authorize]
    public Task<IReadOnlyList<DashboardSeriesDto>> AssetsByType([FromQuery] ReportGroupQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "status", "assetTypeId", "departmentId");
        return service.GetAssetsByTypeReportAsync(query, ct);
    }

    // EP-079: GET /api/v1/reports/asset-status
    [HttpGet("asset-status"), Authorize]
    public Task<IReadOnlyList<DashboardSeriesDto>> AssetStatus([FromQuery] ReportGroupQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "status", "assetTypeId", "departmentId");
        return service.GetAssetStatusReportAsync(query, ct);
    }

    // EP-080: GET /api/v1/reports/assignment-history
    [HttpGet("assignment-history"), Authorize]
    public Task<PagedResponse<AssignmentReportRowDto>> AssignmentHistory([FromQuery] AssignmentReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "assetId", "userId", "departmentId", "fromUtc", "toUtc", "includeArchived");
        return service.GetAssignmentReportAsync(query, ct);
    }

    // EP-081: GET /api/v1/reports/maintenance-history
    [HttpGet("maintenance-history"), Authorize]
    public Task<PagedResponse<MaintenanceReportRowDto>> MaintenanceHistory([FromQuery] MaintenanceReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "assetId", "technicianId", "status", "fromUtc", "toUtc", "includeArchived");
        return service.GetMaintenanceReportAsync(query, ct);
    }

    // EP-082: GET /api/v1/reports/maintenance-cost
    [HttpGet("maintenance-cost"), Authorize]
    public Task<CostReportDto> MaintenanceCost([FromQuery] MaintenanceCostReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "fromUtc", "toUtc", "departmentId", "assetTypeId", "groupBy");
        return service.GetMaintenanceCostReportAsync(query, ct);
    }

    // EP-083: GET /api/v1/reports/warranty-expiration
    [HttpGet("warranty-expiration"), Authorize]
    public Task<PagedResponse<WarrantyReportRowDto>> WarrantyExpiration([FromQuery] WarrantyExpiryReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "fromDate", "toDate", "departmentId", "assetTypeId");
        return service.GetWarrantyExpirationReportAsync(query, ct);
    }

    // EP-084: GET /api/v1/reports/license-expiration
    [HttpGet("license-expiration"), Authorize]
    public Task<PagedResponse<LicenseExpiryRowDto>> LicenseExpiration([FromQuery] LicenseExpiryReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "fromUtc", "toUtc", "softwareId", "status");
        return service.GetLicenseExpirationReportAsync(query, ct);
    }

    // EP-085: GET /api/v1/reports/replacement-recommendations
    [HttpGet("replacement-recommendations"), Authorize]
    public Task<PagedResponse<ReplacementReportRowDto>> ReplacementRecommendations([FromQuery] ReplacementReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "generatedFromUtc", "generatedToUtc", "departmentId", "priority", "disposition", "plannedYear");
        return service.GetReplacementReportAsync(query, ct);
    }

    // EP-086: GET /api/v1/reports/replacement-budget
    [HttpGet("replacement-budget"), Authorize]
    public Task<ReplacementBudgetReportDto> ReplacementBudget([FromQuery] ReplacementBudgetReportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "year", "departmentId", "priority");
        return service.GetReplacementBudgetReportAsync(query, ct);
    }
}
