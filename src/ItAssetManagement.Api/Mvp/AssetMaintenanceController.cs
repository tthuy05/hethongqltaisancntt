using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController]
[Route("api/v1/asset-maintenance")]
[Route("api/v1/maintenance-tickets")]
public sealed class AssetMaintenanceController(MaintenanceService service) : ControllerBase
{
    [HttpGet("technicians")]
    [Authorize(Policy = Permissions.MaintenanceAssign)]
    public Task<PagedResponse<UserLookupDto>> Technicians(CancellationToken ct, string? keyword = null, int page = 1, int pageSize = 20)
    {
        Errors.Query(Request, "keyword", "page", "pageSize");
        return service.TechniciansAsync(keyword, page, pageSize, ct);
    }
    [HttpGet]
    [Authorize(Policy = Permissions.MaintenanceRead)]
    public Task<PagedResponse<MaintenanceDto>> List([FromQuery] MaintenanceListQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "assetId", "page", "pageSize", "reporterId", "technicianId", "status", "priority", "from", "to", "keyword", "sortBy", "sortDirection");
        return service.ListAsync(query, ct);
    }

    [HttpGet("{id:long:min(1)}")]
    [Authorize(Policy = Permissions.MaintenanceRead)]
    public async Task<MaintenanceDto> Get(long id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        Response.Headers.ETag = '"' + result.RowVersion + '"';
        return result;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.MaintenanceCreate)]
    public async Task<IActionResult> Create(CreateMaintenanceRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:long:min(1)}/complete")]
    [HttpPost("{id:long:min(1)}/resolve")]
    [Authorize(Policy = Permissions.MaintenanceComplete)]
    public Task<MaintenanceDto> Complete(long id, CompleteMaintenanceRequest request, CancellationToken ct) =>
        service.CompleteAsync(id, request, ct);

    [HttpPut("{id:long:min(1)}")]
    [Authorize(Policy = Permissions.MaintenanceUpdate)]
    public Task<MaintenanceDto> Update(long id, UpdateMaintenanceRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("{id:long:min(1)}/assign")]
    [Authorize(Policy = Permissions.MaintenanceAssign)]
    public Task<MaintenanceDto> Assign(long id, AssignTechnicianRequest request, CancellationToken ct) => service.AssignAsync(id, request, ct);

    [HttpPost("{id:long:min(1)}/start")]
    [Authorize(Policy = Permissions.MaintenanceWork)]
    public Task<MaintenanceDto> Start(long id, StartMaintenanceRequest request, CancellationToken ct) => service.StartAsync(id, request, ct);

    [HttpPost("{id:long:min(1)}/fail")]
    [Authorize(Policy = Permissions.MaintenanceResolve)]
    public Task<MaintenanceDto> Fail(long id, FailMaintenanceRequest request, CancellationToken ct) => service.FailAsync(id, request, ct);

    [HttpPost("{id:long:min(1)}/cancel")]
    [Authorize(Policy = Permissions.MaintenanceCancel)]
    public Task<MaintenanceDto> Cancel(long id, CancelMaintenanceRequest request, CancellationToken ct) => service.CancelAsync(id, request, ct);

    [HttpGet("{id:long:min(1)}/history")]
    [Authorize(Policy = Permissions.MaintenanceRead)]
    public Task<PagedResponse<MaintenanceHistoryDto>> History(long id, CancellationToken ct, int page = 1, int pageSize = 20,
        DateTime? from = null, DateTime? to = null, string? eventType = null)
    {
        Errors.Query(Request, "page", "pageSize", "from", "to", "eventType");
        return service.HistoryAsync(id, page, pageSize, from, to, eventType, ct);
    }
}
