using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/replacement-rules")]
public sealed class ReplacementRuleController(ReplacementRuleQueryService service, ReplacementRuleCommandService commands) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.ReplacementRuleRead)]
    public Task<PagedResponse<ReplacementRuleDto>> List([FromQuery] ReplacementRuleQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "assetTypeId", "status", "keyword", "sortBy", "sortDirection");
        Response.Headers.CacheControl = "no-store";
        return service.ListAsync(query, ct);
    }

    [HttpGet("{id:long:min(1)}"), Authorize(Policy = Permissions.ReplacementRuleRead)]
    public async Task<ReplacementRuleDto> Get(long id, CancellationToken ct)
    {
        var item = await service.GetAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.ETag = '"' + item.RowVersion + '"';
        return item;
    }

    [HttpPost, Authorize(Policy = Permissions.ReplacementRuleCreate)]
    public async Task<IActionResult> Create(ReplacementRuleWriteRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await commands.WriteAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:long:min(1)}"), Authorize(Policy = Permissions.ReplacementRuleUpdate)]
    public Task<ReplacementRuleDto> Revise(long id, ReplacementRuleWriteRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return commands.WriteAsync(id, request, ct);
    }

    [HttpPatch("{id:long:min(1)}/status"), Authorize(Policy = Permissions.ReplacementRuleUpdate)]
    public Task<ReplacementRuleDto> Status(long id, ReplacementRuleStatusRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return commands.StatusAsync(id, request, ct);
    }
}
