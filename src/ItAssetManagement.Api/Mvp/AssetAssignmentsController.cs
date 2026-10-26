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
    public Task<PagedResponse<AssignmentDto>> List([FromQuery] AssignmentListQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "assetId", "userId", "departmentId", "active", "status", "from", "to", "sortBy", "sortDirection");
        return service.ListAsync(query, ct);
    }

    [HttpGet("/api/v1/assets/{assetId:long:min(1)}/assignments")]
    [Authorize(Policy = Permissions.AssignmentRead)]
    public Task<PagedResponse<AssignmentDto>> History(long assetId, [FromQuery] AssignmentListQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "status", "from", "to", "sortBy", "sortDirection");
        return service.HistoryAsync(assetId, query, ct);
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

    [HttpPost("{id:long:min(1)}/transfer")]
    [Authorize(Policy = Permissions.AssignmentTransfer)]
    public Task<AssignmentTransferDto> Transfer(long id, TransferAssetRequest request, CancellationToken ct) =>
        service.TransferAsync(id, request, ct);
}
