using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class UserLookupQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public long? DepartmentId { get; set; }
    public string? Status { get; set; }
    public string SortBy { get; set; } = "displayName";
    public string SortDirection { get; set; } = "asc";
}

public sealed record UserLookupDto
{
    public long Id { get; init; }
    public string DisplayName { get; init; } = "";
    public long? DepartmentId { get; init; }
}

// Workflow picker only, not a user directory/admin API or a technician-eligibility check.
public sealed class UserLookupService(IRepository repo, IActor actor)
{
    public async Task<PagedResponse<UserLookupDto>> ListAsync(UserLookupQuery query, CancellationToken ct)
    {
        if (!actor.Has(Permissions.UserLookup))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền tra cứu người nhận tài sản.");
        Validate(query);
        var users = repo.Query<User>().Where(x => x.IsActive);
        if (query.DepartmentId != null) users = users.Where(x => x.DepartmentId == query.DepartmentId);
        // Project before materialization: never select security/contact fields into the response.
        var result = users.Select(x => new UserLookupDto { Id = x.Id, DisplayName = x.FullName, DepartmentId = x.DepartmentId });
        if (query.Keyword != null)
        {
            var keyword = query.Keyword.ToLowerInvariant();
            result = result.Where(x => x.DisplayName.ToLower().Contains(keyword));
        }
        var total = await repo.CountAsync(result, ct);
        var ordered = (query.SortBy, query.SortDirection) switch
        {
            ("id", "asc") => result.OrderBy(x => x.Id),
            ("id", "desc") => result.OrderByDescending(x => x.Id),
            ("displayName", "desc") => result.OrderByDescending(x => x.DisplayName).ThenBy(x => x.Id),
            _ => result.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
        };
        var items = await repo.ListAsync(ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize), ct);
        return new(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public static void Validate(UserLookupQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        query.Keyword = Validation.Optional(query.Keyword, 200, "keyword");
        if (query.DepartmentId <= 0) throw Validation.Invalid("departmentId", "ID phải > 0.");
        if (query.Status is not (null or "Active"))
            throw Validation.Invalid("status", "Lookup chỉ hỗ trợ tài khoản Active.");
        if (query.SortBy is not ("displayName" or "id")) throw Validation.Invalid("sortBy", "Chỉ hỗ trợ displayName hoặc id.");
        if (query.SortDirection is not ("asc" or "desc")) throw Validation.Invalid("sortDirection", "Chỉ hỗ trợ asc hoặc desc.");
    }
}
