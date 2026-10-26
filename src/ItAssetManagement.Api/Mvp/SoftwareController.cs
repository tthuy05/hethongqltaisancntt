using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/software")]
public sealed class SoftwareController(SoftwareService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = SoftwarePermissions.Read)]
    public Task<PagedResponse<SoftwareDto>> List([FromQuery] SoftwareListQuery query, CancellationToken ct) {
        Errors.Query(Request, "page", "pageSize", "keyword", "status", "vendor", "sortBy", "sortDirection");
        return service.ListAsync(query, ct);
    }
    [HttpGet("{id:long:min(1)}"), Authorize(Policy = SoftwarePermissions.Read)]
    public async Task<SoftwareDto> Get(long id, CancellationToken ct) {
        var dto = await service.GetAsync(id, ct); Response.Headers.ETag = '"' + dto.RowVersion + '"'; return dto;
    }
    [HttpPost, Authorize(Policy = SoftwarePermissions.Create)]
    public async Task<IActionResult> Create(SoftwareRequest request, CancellationToken ct) {
        var dto = await service.WriteAsync(null, request, ct); return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }
    [HttpPut("{id:long:min(1)}"), Authorize(Policy = SoftwarePermissions.Update)]
    public Task<SoftwareDto> Update(long id, SoftwareRequest request, CancellationToken ct) => service.WriteAsync(id, request, ct);
    [HttpDelete("{id:long:min(1)}"), Authorize(Policy = SoftwarePermissions.Archive)]
    public async Task<IActionResult> Archive(long id, CancellationToken ct) {
        await service.ArchiveAsync(id, Validation.IfMatch(Request.Headers.IfMatch.ToString()), ct); return NoContent();
    }
}
