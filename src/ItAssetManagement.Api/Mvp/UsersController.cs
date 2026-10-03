using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/users")]
public sealed class UsersController(UserLookupService service) : ControllerBase
{
    [HttpGet("lookup"), Authorize(Policy = Permissions.UserLookup)]
    [ProducesResponseType<PagedResponse<UserLookupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<PagedResponse<UserLookupDto>> Lookup(
        CancellationToken ct,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "pageSize")] int pageSize = 20,
        [FromQuery(Name = "keyword")] string? keyword = null,
        [FromQuery(Name = "departmentId")] long? departmentId = null,
        [FromQuery(Name = "status")] string? status = null,
        [FromQuery(Name = "sortBy")] string sortBy = "displayName",
        [FromQuery(Name = "sortDirection")] string sortDirection = "asc")
    {
        Errors.Query(Request, "page", "pageSize", "keyword", "departmentId", "status", "sortBy", "sortDirection");
        return service.ListAsync(new() { Page = page, PageSize = pageSize, Keyword = keyword, DepartmentId = departmentId,
            Status = status, SortBy = sortBy, SortDirection = sortDirection }, ct);
    }
}
