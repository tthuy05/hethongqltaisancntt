using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

[ApiController, Route("api/v1/users")]
public sealed class UsersController(UserLookupService service, UserService users, UserAccountService accounts, UserAccountReadService accountReads) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissions.UserRead)]
    [ProducesResponseType<PagedResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<PagedResponse<UserDto>> List(CancellationToken ct,
        [FromQuery(Name = "page")] int page = 1, [FromQuery(Name = "pageSize")] int pageSize = 20,
        [FromQuery(Name = "keyword")] string? keyword = null, [FromQuery(Name = "departmentId")] long? departmentId = null,
        [FromQuery(Name = "roleId")] long? roleId = null, [FromQuery(Name = "status")] string? status = null,
        [FromQuery(Name = "sortBy")] string sortBy = "displayName", [FromQuery(Name = "sortDirection")] string sortDirection = "asc")
    {
        Errors.Query(Request, "page", "pageSize", "keyword", "departmentId", "roleId", "status", "sortBy", "sortDirection");
        return users.ListAsync(new() { Page = page, PageSize = pageSize, Keyword = keyword, DepartmentId = departmentId,
            RoleId = roleId, Status = status, SortBy = sortBy, SortDirection = sortDirection }, ct);
    }

    [HttpGet("{userId:long}"), Authorize(Policy = Permissions.UserRead)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UserDto> Get(long userId, CancellationToken ct) => users.GetAsync(userId, ct);

    [HttpPost, Authorize(Policy = Permissions.UserCreate)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { userId = result.Id }, result);
    }

    [HttpPut("{userId:long}"), Authorize(Policy = Permissions.UserUpdate)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserDto> Update(long userId, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(userId, request, ct);

    [HttpGet("{userId:long}/account"), Authorize(Policy = Permissions.UserRead)]
    [ProducesResponseType<UserAccountStateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UserAccountStateDto> Account(long userId, CancellationToken ct)
    {
        Errors.Query(Request, Array.Empty<string>()); return accountReads.GetAsync(userId, ct);
    }

    [HttpPatch("{userId:long}/status"), Authorize(Policy = Permissions.UserStatus)]
    [ProducesResponseType<AccountChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AccountChangeResult> Status(long userId, StatusRequest request, CancellationToken ct) => accounts.StatusAsync(userId, request, ct);

    [HttpPut("{userId:long}/roles"), Authorize(Policy = Permissions.RoleAssign)]
    [ProducesResponseType<AccountChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AccountChangeResult> Roles(long userId, ReplaceUserRolesRequest request, CancellationToken ct) => accounts.RolesAsync(userId, request, ct);

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
