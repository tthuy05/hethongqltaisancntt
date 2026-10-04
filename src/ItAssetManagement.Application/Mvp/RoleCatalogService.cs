using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class RoleListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string SortBy { get; set; } = "name";
    public string SortDirection { get; set; } = "asc";
}
public sealed class RoleSummaryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Code { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? IsActive { get; set; }
}
public sealed class PermissionDto
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Module { get; set; } = "";
    public bool IsActive { get; set; }
}
public sealed record RoleDetailDto(long Id, string Code, string Name, bool IsActive, string? Description, IReadOnlyList<PermissionDto> Permissions);

// Fixed definitions are read-only; users/roles writes stay behind their existing policies.
public sealed class RoleCatalogService(IRepository repo, IActor actor)
{
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền xem danh mục vai trò.");
    }
    public static void Validate(RoleListQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        query.Keyword = Validation.Optional(query.Keyword, 200, "keyword");
        if (query.Status is not (null or "Active" or "Inactive")) throw Validation.Invalid("status", "Active hoặc Inactive.");
        if (query.SortBy is not ("id" or "name") || query.SortDirection is not ("asc" or "desc"))
            throw Validation.Invalid("sortBy", "Chỉ sắp xếp theo id/name và asc/desc.");
    }
    public async Task<PagedResponse<RoleSummaryDto>> ListAsync(RoleListQuery query, CancellationToken ct)
    {
        Require(Permissions.RoleRead); Validate(query);
        var detailed = actor.Has(Permissions.RolePermissionsRead);
        var roles = repo.Query<Role>().Where(x => Permissions.Roles.Contains(x.Code));
        // Limited readers can search only the label they can see, never hidden code/description.
        if (query.Keyword != null)
        {
            var keyword = query.Keyword.ToLowerInvariant();
            roles = roles.Where(x => x.Name.ToLower().Contains(keyword));
        }
        if (query.Status != null) roles = roles.Where(x => x.IsActive == (query.Status == "Active"));
        var count = await repo.CountAsync(roles, ct);
        var order = (query.SortBy, query.SortDirection) switch
        {
            ("id", "asc") => roles.OrderBy(x => x.Id), ("id", "desc") => roles.OrderByDescending(x => x.Id),
            ("name", "desc") => roles.OrderByDescending(x => x.Name), _ => roles.OrderBy(x => x.Name)
        };
        var items = await repo.ListAsync(order.ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new RoleSummaryDto { Id = x.Id, Name = x.Name, Code = detailed ? x.Code : null,
                IsActive = detailed ? x.IsActive : null }), ct);
        return new(items, query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize));
    }
    private IQueryable<PermissionDto> PermissionQuery() => repo.Query<Permission>()
        .Where(x => Permissions.All.Contains(x.Code))
        .Select(x => new PermissionDto { Id = x.Id, Code = x.Code, Name = x.Name, Module = x.Module, IsActive = x.IsActive });
    public Task<List<PermissionDto>> PermissionsAsync(string? module, string? keyword, CancellationToken ct)
    {
        Require(Permissions.RolePermissionsRead);
        module = Validation.Optional(module, 100, "module"); keyword = Validation.Optional(keyword, 200, "keyword");
        var query = PermissionQuery();
        if (module != null) query = query.Where(x => x.Module == module);
        if (keyword != null)
        {
            var key = keyword.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(key) || x.Code.ToLower().Contains(key));
        }
        return repo.ListAsync(query.OrderBy(x => x.Code).ThenBy(x => x.Id), ct);
    }
    public async Task<RoleDetailDto> GetAsync(long id, CancellationToken ct)
    {
        Require(Permissions.RolePermissionsRead);
        if (id <= 0) throw Validation.Invalid("roleId", "ID phải > 0.");
        var role = await repo.FirstAsync(repo.Query<Role>().Where(x => x.Id == id && Permissions.Roles.Contains(x.Code)), ct)
            ?? throw new BusinessException(404, "ROLE_NOT_FOUND", "Không tìm thấy vai trò cố định.");
        var links = repo.Query<RolePermission>().Where(x => x.RoleId == id).Select(x => x.PermissionId);
        var permissions = await repo.ListAsync(PermissionQuery().Where(x => links.Contains(x.Id)).OrderBy(x => x.Code).ThenBy(x => x.Id), ct);
        return new(role.Id, role.Code, role.Name, role.IsActive, role.Description, permissions);
    }
}
