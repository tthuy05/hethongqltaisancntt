using ItAssetManagement.Application.Export;
using ItAssetManagement.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController,Route("api/v1/reports/export"),Authorize]
public sealed class ReportExportsController(ReportExportService service):ControllerBase
{
    private async Task<IActionResult> Download(Task<(byte[],string)> result) { var (bytes,name)=await result;Response.Headers.CacheControl="no-store";return File(bytes,ExportSecurityLimits.ContentType,name); }
    [HttpGet("license-expiration")]
    public Task<IActionResult> Licenses([FromQuery]LicenseExpiryReportQuery query,CancellationToken ct) {
        Errors.Query(Request,"fromUtc","toUtc","softwareId","status");return Download(service.Licenses(query,ct));
    }
    [HttpGet("asset-groups")]
    public Task<IActionResult> Groups([FromQuery]ReportGroupQuery query,[FromQuery]string groupBy="department",CancellationToken ct=default) {
        Errors.Query(Request,"departmentId","assetTypeId","status","groupBy");return Download(service.Groups(query,groupBy,ct));
    }
    [HttpGet("maintenance-cost")]
    public Task<IActionResult> Costs([FromQuery]MaintenanceCostReportQuery query,CancellationToken ct) {
        Errors.Query(Request,"fromUtc","toUtc","departmentId","assetTypeId","groupBy");return Download(service.Costs(query,ct));
    }
    [HttpGet("replacement-budget")]
    public Task<IActionResult> Budget([FromQuery]ReplacementBudgetReportQuery query,CancellationToken ct) {
        Errors.Query(Request,"year","departmentId","priority");return Download(service.Budget(query,ct));
    }
    [HttpGet("replacement-history")]
    public Task<IActionResult> History([FromQuery]ReplacementReportQuery query,CancellationToken ct) {
        Errors.Query(Request,"generatedFromUtc","generatedToUtc","departmentId","priority","disposition","plannedYear");return Download(service.History(query,ct));
    }
}
