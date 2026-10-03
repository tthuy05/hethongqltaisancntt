using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => service.LoginAsync(request, ct);
    [HttpGet("me"), Authorize]
    public CurrentUser Me() => (CurrentUser)HttpContext.Items[typeof(CurrentUser)]!;
}
[ApiController, Route("api/v1/assets")]
public sealed class AssetsController(AssetService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.AssetRead)]
    public Task<PagedResponse<AssetDto>> List([FromQuery] ListQuery query, CancellationToken ct)
    { Errors.Query(Request, true); return service.ListAsync(query, ct); }
    [HttpGet("{id:long:min(1)}"), Authorize(Policy = Permissions.AssetRead)]
    public async Task<AssetDto> Get(long id, CancellationToken ct)
    { var result = await service.GetAsync(id, ct); Response.Headers.ETag = '"' + result.RowVersion + '"'; return result; }
    [HttpPost, Authorize(Policy = Permissions.AssetCreate)]
    public async Task<IActionResult> Create(AssetRequest request, CancellationToken ct)
    { var result = await service.CreateAsync(request, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:long:min(1)}"), Authorize(Policy = Permissions.AssetUpdate)]
    public Task<AssetDto> Update(long id, AssetRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);
    [HttpDelete("{id:long:min(1)}"), Authorize(Policy = Permissions.AssetArchive)]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
    { await service.ArchiveAsync(id, Validation.IfMatch(Request.Headers.IfMatch.ToString()), ct); return NoContent(); }
    [HttpPatch("{id:long:min(1)}/status"), Authorize(Policy = Permissions.AssetStatus)]
    public Task<AssetDto> Status(long id, StatusRequest request, CancellationToken ct) => service.StatusAsync(id, request, ct);
    [HttpGet("{id:long:min(1)}/status-history"), Authorize(Policy = Permissions.AssetHistory)]
    public Task<PagedResponse<HistoryDto>> History(long id, [FromQuery] ListQuery q, CancellationToken ct)
    { Errors.Query(Request, true, true); return service.HistoryAsync(id, q, ct); }
}
[ApiController, Route("api/v1/departments")]
public sealed class DepartmentsController(MasterService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.DepartmentRead)]
    public Task<PagedResponse<MasterDto>> List([FromQuery] ListQuery query, CancellationToken ct)
    { Errors.Query(Request, false); return service.ListAsync(true, query, ct); }
    [HttpGet("{id:long:min(1)}"), Authorize(Policy = Permissions.DepartmentRead)]
    public Task<MasterDto> Get(long id, CancellationToken ct) => service.GetAsync(true, id, ct);
    [HttpPost, Authorize(Policy = Permissions.DepartmentCreate)]
    public async Task<IActionResult> Create(MasterRequest request, CancellationToken ct)
    { var result = await service.WriteAsync(true, null, request, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:long:min(1)}"), Authorize(Policy = Permissions.DepartmentUpdate)]
    public Task<MasterDto> Update(long id, MasterRequest request, CancellationToken ct) => service.WriteAsync(true, id, request, ct);
    [HttpPatch("{id:long:min(1)}/status"), Authorize(Policy = Permissions.DepartmentArchive)]
    public Task<MasterDto> Status(long id, StatusRequest request, CancellationToken ct) => service.StatusAsync(true, id, request, ct);
}
[ApiController, Route("api/v1/asset-types")]
public sealed class AssetTypesController(MasterService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.TypeRead)]
    public Task<PagedResponse<MasterDto>> List([FromQuery] ListQuery query, CancellationToken ct)
    { Errors.Query(Request, false); return service.ListAsync(false, query, ct); }
    [HttpGet("{id:long:min(1)}"), Authorize(Policy = Permissions.TypeRead)]
    public Task<MasterDto> Get(long id, CancellationToken ct) => service.GetAsync(false, id, ct);
    [HttpPost, Authorize(Policy = Permissions.TypeCreate)]
    public async Task<IActionResult> Create(MasterRequest request, CancellationToken ct)
    { var result = await service.WriteAsync(false, null, request, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:long:min(1)}"), Authorize(Policy = Permissions.TypeUpdate)]
    public Task<MasterDto> Update(long id, MasterRequest request, CancellationToken ct) => service.WriteAsync(false, id, request, ct);
    [HttpPatch("{id:long:min(1)}/status"), Authorize(Policy = Permissions.TypeArchive)]
    public Task<MasterDto> Status(long id, StatusRequest request, CancellationToken ct) => service.StatusAsync(false, id, request, ct);
}
