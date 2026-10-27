using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/license-assignments")]
public sealed class LicenseAssignmentController(LicenseAssignmentService service) : ControllerBase
{
    private string Version(string value) {
        if (!Request.Headers.TryGetValue("If-Match", out var header)) return value;
        var expected = Validation.IfMatch(header.ToString());
        if (!string.IsNullOrWhiteSpace(value) && !Validation.Version(value).SequenceEqual(expected)) throw Validation.Invalid("rowVersion", "If-Match và rowVersion phải khớp.");
        return Convert.ToBase64String(expected);
    }
    [HttpGet, Authorize(Policy = LicensePermissions.AssignmentRead)]
    public Task<PagedResponse<LicenseAssignmentDto>> List([FromQuery] LicenseAssignmentQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "licenseId", "userId", "assetId", "status", "targetType", "from", "to", "sortBy", "sortDirection");
        return service.ListAsync(query, ct);
    }

    [HttpGet("{id:long:min(1)}"), Authorize(Policy = LicensePermissions.AssignmentRead)]
    public async Task<LicenseAssignmentDto> Get(long id, CancellationToken ct)
    {
        var dto = await service.GetAsync(id, ct);
        Response.Headers.ETag = '"' + dto.RowVersion + '"';
        return dto;
    }

    [HttpPost, Authorize(Policy = LicensePermissions.AssignmentAssign)]
    public async Task<IActionResult> Assign(AssignLicenseRequest request, CancellationToken ct)
    {
        var dto = await service.AssignAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPost("{id:long:min(1)}/revoke"), Authorize(Policy = LicensePermissions.AssignmentRevoke)]
    public async Task<LicenseAssignmentDto> Revoke(long id, RevokeLicenseAssignmentRequest request, CancellationToken ct)
    {
        request.RowVersion = Version(request.RowVersion);
        return await service.RevokeAsync(id, request, ct);
    }

    [HttpPost("{id:long:min(1)}/transfer"), Authorize(Policy = LicensePermissions.AssignmentTransfer)]
    public async Task<TransferResultDto> Transfer(long id, TransferLicenseAssignmentRequest request, CancellationToken ct)
    {
        request.RowVersion = Version(request.RowVersion);
        return await service.TransferAsync(id, request, ct);
    }

    [HttpGet("~/api/v1/software-licenses/{licenseId:long:min(1)}/assignments"), Authorize(Policy = LicensePermissions.AssignmentRead)]
    public Task<PagedResponse<LicenseAssignmentDto>> ListByLicense(long licenseId, [FromQuery] LicenseAssignmentQuery query, CancellationToken ct)
    {
        Errors.Query(Request, "page", "pageSize", "status", "targetType", "from", "to", "sortBy", "sortDirection");
        query.LicenseId = licenseId;
        return service.ListAsync(query, ct);
    }
}
