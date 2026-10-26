using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

public sealed class RevealKeyRequest
{
    public string? Reason { get; set; }
}

[ApiController, Route("api/v1/software-licenses")]
public sealed class SoftwareLicenseController(LicenseService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.LicenseRead)]
    public Task<PagedResponse<LicenseDto>> List([FromQuery] LicenseQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "softwareId", "keyword", "vendor", "status", "expirationFrom", "expirationTo", "sortBy", "sortDirection");
        return service.ListAsync(query, ct);
    }

    [HttpGet("software-options"), Authorize(Policy = Permissions.LicenseRead)]
    public Task<PagedResponse<LicenseSoftwareDto>> SoftwareOptions([FromQuery] int page = 1, [FromQuery] string? keyword = null, CancellationToken ct = default)
    {
        Errors.Query(Request, "page", "keyword");
        return service.SoftwareOptionsAsync(page, keyword, ct);
    }

    [HttpGet("{id:long:min(1)}"), Authorize(Policy = Permissions.LicenseRead)]
    public async Task<LicenseDto> Get(long id, CancellationToken ct)
    {
        var dto = await service.GetAsync(id, ct);
        Response.Headers.ETag = '"' + dto.RowVersion + '"';
        return dto;
    }

    [HttpPost, Authorize(Policy = LicensePermissions.Create)]
    public async Task<IActionResult> Create([FromBody] LicenseRequest request, CancellationToken ct)
    {
        var dto = await service.WriteAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long:min(1)}"), Authorize(Policy = LicensePermissions.Update)]
    public Task<LicenseDto> Update(long id, [FromBody] LicenseRequest request, CancellationToken ct) =>
        service.WriteAsync(id, request, ct);

    [HttpDelete("{id:long:min(1)}"), Authorize(Policy = LicensePermissions.Archive)]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
    {
        await service.ArchiveAsync(id, Validation.IfMatch(Request.Headers.IfMatch.ToString()), ct);
        return NoContent();
    }

    [HttpPost("{id:long:min(1)}/reveal-key"), Authorize(Policy = Permissions.LicenseKeyReveal)]
    public async Task<LicenseKeyRevealResponse> RevealKey(long id, [FromBody] RevealKeyRequest request, [FromServices] LicenseKeyRevealService revealService, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return await revealService.RevealAsync(id, request?.Reason, ct);
    }
}
