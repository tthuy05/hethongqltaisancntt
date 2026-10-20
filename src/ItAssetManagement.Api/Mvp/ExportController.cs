using ItAssetManagement.Application.Export;
using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/export")]
public sealed class ExportController(AssetExportService exportService) : ControllerBase
{
    // EP-088: GET /api/v1/export/assets
    [HttpGet("assets"), Authorize]
    public async Task<IActionResult> ExportAssets([FromQuery] AssetExportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "keyword", "status", "assetTypeId", "departmentId", "fromUtc", "toUtc", "sortBy", "sortDirection");
        var (bytes, filename) = await exportService.ExportAssetsAsync(query, ct);
        return File(bytes, ExportSecurityLimits.ContentType, filename);
    }

    // EP-089: GET /api/v1/export/maintenance
    [HttpGet("maintenance"), Authorize]
    public async Task<IActionResult> ExportMaintenance([FromQuery] MaintenanceExportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "fromUtc", "toUtc", "assetId", "technicianId", "status");
        var (bytes, filename) = await exportService.ExportMaintenanceAsync(query, ct);
        return File(bytes, ExportSecurityLimits.ContentType, filename);
    }

    // EP-090: GET /api/v1/export/assignment-history
    [HttpGet("assignment-history"), Authorize]
    public async Task<IActionResult> ExportAssignments([FromQuery] AssignmentExportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "fromUtc", "toUtc", "assetId", "userId", "departmentId");
        var (bytes, filename) = await exportService.ExportAssignmentsAsync(query, ct);
        return File(bytes, ExportSecurityLimits.ContentType, filename);
    }

    // EP-091: GET /api/v1/export/warranty-expiration
    [HttpGet("warranty-expiration"), Authorize]
    public async Task<IActionResult> ExportWarranty([FromQuery] WarrantyExportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "fromUtc", "toUtc", "departmentId", "assetTypeId");
        var (bytes, filename) = await exportService.ExportWarrantyAsync(query, ct);
        return File(bytes, ExportSecurityLimits.ContentType, filename);
    }

    // EP-092: GET /api/v1/export/replacement-report
    [HttpGet("replacement-report"), Authorize]
    public async Task<IActionResult> ExportReplacement([FromQuery] ReplacementExportQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "year", "departmentId", "priority", "status");
        var (bytes, filename) = await exportService.ExportReplacementAsync(query, ct);
        return File(bytes, ExportSecurityLimits.ContentType, filename);
    }
}
