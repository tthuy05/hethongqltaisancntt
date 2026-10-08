using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController]
[Route("api/v1/asset-assignments")]
public sealed class AssetAssignmentsController(AssignmentService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.AssignmentRead)]
    public Task<PagedResponse<AssignmentDto>> List(CancellationToken ct,
        [FromQuery(Name = "page")] int page = 1, [FromQuery(Name = "pageSize")] int pageSize = 20,
        [FromQuery(Name = "assetId")] long? assetId = null, [FromQuery(Name = "userId")] long? userId = null,
        [FromQuery(Name = "departmentId")] long? departmentId = null, [FromQuery(Name = "active")] bool activeOnly = false)
    {
        Errors.Query(Request, "page", "pageSize", "assetId", "userId", "departmentId", "active");
        return service.ListAsync(assetId, userId, departmentId, activeOnly, page, pageSize, ct);
    }

    [HttpGet("{id:long:min(1)}")]
    [Authorize(Policy = Permissions.AssignmentRead)]
    public async Task<AssignmentDto> Get(long id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        Response.Headers.ETag = '"' + result.RowVersion + '"';
        return result;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AssignmentAssign)]
    public async Task<IActionResult> Assign(AssignAssetRequest request, CancellationToken ct)
    {
        var result = await service.AssignAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:long:min(1)}/return")]
    [Authorize(Policy = Permissions.AssignmentReturn)]
    public Task<AssignmentDto> Return(long id, ReturnAssetRequest request, CancellationToken ct) =>
        service.ReturnAsync(id, request, ct);
}