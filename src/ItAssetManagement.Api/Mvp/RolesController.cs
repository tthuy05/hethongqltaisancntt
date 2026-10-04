using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/roles")]
public sealed class RolesController(RoleCatalogService roles) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.RoleRead)]
    [ProducesResponseType<PagedResponse<RoleSummaryDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)] [ProducesResponseType<ProblemDetails>(401)] [ProducesResponseType<ProblemDetails>(403)]
    public Task<PagedResponse<RoleSummaryDto>> List(CancellationToken ct,
        [FromQuery(Name = "page")] int page = 1, [FromQuery(Name = "pageSize")] int pageSize = 20,
        [FromQuery(Name = "keyword")] string? keyword = null, [FromQuery(Name = "status")] string? status = null,
        [FromQuery(Name = "sortBy")] string sortBy = "name", [FromQuery(Name = "sortDirection")] string sortDirection = "asc")
    {
        Errors.Query(Request, "page", "pageSize", "keyword", "status", "sortBy", "sortDirection");
        return roles.ListAsync(new() { Page = page, PageSize = pageSize, Keyword = keyword, Status = status,
            SortBy = sortBy, SortDirection = sortDirection }, ct);
    }
    [HttpGet("permissions"), Authorize(Policy = Permissions.RolePermissionsRead)]
    [ProducesResponseType<List<PermissionDto>>(200)]
    [ProducesResponseType<ProblemDetails>(400)] [ProducesResponseType<ProblemDetails>(401)] [ProducesResponseType<ProblemDetails>(403)]
    public Task<List<PermissionDto>> PermissionCatalog(CancellationToken ct, [FromQuery(Name = "module")] string? module = null,
        [FromQuery(Name = "keyword")] string? keyword = null)
    {
        Errors.Query(Request, "module", "keyword"); return roles.PermissionsAsync(module, keyword, ct);
    }
    [HttpGet("{roleId:long}"), Authorize(Policy = Permissions.RolePermissionsRead)]
    [ProducesResponseType<RoleDetailDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)] [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)] [ProducesResponseType<ProblemDetails>(404)]
    public Task<RoleDetailDto> Get(long roleId, CancellationToken ct)
    {
        Errors.Query(Request, Array.Empty<string>()); return roles.GetAsync(roleId, ct);
    }
}
