using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/dashboard")]
public sealed class DashboardController(DashboardQueryService service) : ControllerBase
{
    // EP-072: GET /api/v1/dashboard/summary
    [HttpGet("summary"), Authorize(Policy = Permissions.DashboardRead)]
    public Task<DashboardSummaryDto> Summary([FromQuery] DashboardSummaryQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "departmentId");
        return service.GetSummaryAsync(query, ct);
    }

    // EP-073: GET /api/v1/dashboard/assets
    [HttpGet("assets"), Authorize(Policy = Permissions.DashboardRead)]
    public Task<IReadOnlyList<DashboardSeriesDto>> Assets([FromQuery] DashboardAssetGroupQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "groupBy", "departmentId");
        return service.GetAssetGroupsAsync(query, ct);
    }

    // EP-074: GET /api/v1/dashboard/alerts
    [HttpGet("alerts"), Authorize(Policy = Permissions.DashboardRead)]
    public Task<DashboardAlertsDto> Alerts([FromQuery] DashboardAlertsQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "daysAhead", "departmentId");
        return service.GetAlertsAsync(query, ct);
    }

    // EP-075: GET /api/v1/dashboard/costs
    [HttpGet("costs"), Authorize]
    public Task<DashboardCostSummaryDto> Costs([FromQuery] DashboardCostsQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "fromUtc", "toUtc", "departmentId");
        return service.GetCostsAsync(query, ct);
    }
}
