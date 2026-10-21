using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController]
[Route("api/v1/asset-maintenance")]
public sealed class AssetMaintenanceController(MaintenanceService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MaintenanceRead)]
    public Task<PagedResponse<MaintenanceDto>> List(CancellationToken ct,
        [FromQuery(Name = "assetId")] long? assetId = null,
        [FromQuery(Name = "page")] int page = 1, [FromQuery(Name = "pageSize")] int pageSize = 20)
    {
        return service.ListAsync(assetId, page, pageSize, ct);
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
    [Authorize(Policy = Permissions.MaintenanceComplete)]
    public Task<MaintenanceDto> Complete(long id, CompleteMaintenanceRequest request, CancellationToken ct) =>
        service.CompleteAsync(id, request, ct);
}