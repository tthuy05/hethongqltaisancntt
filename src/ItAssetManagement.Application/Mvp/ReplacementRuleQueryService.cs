using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class ReplacementRuleQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? AssetTypeId { get; set; }
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public string SortBy { get; set; } = "priority";
    public string SortDirection { get; set; } = "asc";
}

public sealed record ReplacementRuleDto(long Id, string Code, int Version, string Name, long? AssetTypeId, string? Severity,
    int? MinimumAgeMonths, decimal? MaximumMaintenanceCostRatio, int? MinimumMaintenanceCount,
    bool RequireWarrantyExpired, int? MinimumFailureCount, int Priority,
    DateTime EffectiveFrom, DateTime? EffectiveTo, bool IsActive, string? Description,
    DateTime CreatedAt, DateTime? UpdatedAt, string RowVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? EstimatedUnitCost);

// Read-only catalog. Evaluation and version-changing commands are separate slices.
public sealed class ReplacementRuleQueryService(IRepository repo, IActor actor)
{
    private void Require()
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.ReplacementRuleRead))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền xem quy tắc thay thế.");
    }
    private ReplacementRuleDto Map(ReplacementRule x) => new(x.Id, x.Code, x.Version, x.Name, x.AssetTypeId, x.Severity,
        x.MinimumAgeMonths, x.MaximumMaintenanceCostRatio, x.MinimumMaintenanceCount, x.RequireWarrantyExpired,
        x.MinimumFailureCount, x.Priority, x.EffectiveFromUtc, x.EffectiveToUtc, x.IsActive, x.Description,
        x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion),
        actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost) ? x.EstimatedUnitCost : null);

    public async Task<ReplacementRuleDto> GetAsync(long id, CancellationToken ct)
    {
        Require();
        if (id <= 0) throw Validation.Invalid("ruleId", "ID phải là số nguyên dương.");
        return Map(await repo.FirstAsync(repo.Query<ReplacementRule>().Where(x => x.Id == id), ct)
            ?? throw new BusinessException(404, "REPLACEMENT_RULE_NOT_FOUND", "Không tìm thấy quy tắc thay thế."));
    }

    public async Task<PagedResponse<ReplacementRuleDto>> ListAsync(ReplacementRuleQuery filter, CancellationToken ct)
    {
        Require();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "Phân trang không hợp lệ.");
        if (filter.AssetTypeId is <= 0) throw Validation.Invalid("assetTypeId", "ID phải là số nguyên dương.");
        if (filter.Status is not (null or "Active" or "Inactive")) throw Validation.Invalid("status", "Trạng thái không hợp lệ.");
        if (filter.SortBy is not ("code" or "name" or "version" or "priority" or "createdAt") || filter.SortDirection is not ("asc" or "desc"))
            throw Validation.Invalid("sortBy", "Cột/chiều sắp xếp không hợp lệ.");
        var term = Validation.Optional(filter.Keyword, 200, "keyword")?.ToLowerInvariant();
        var query = repo.Query<ReplacementRule>();
        if (filter.AssetTypeId.HasValue) query = query.Where(x => x.AssetTypeId == filter.AssetTypeId.Value);
        if (filter.Status != null) { var active = filter.Status == "Active"; query = query.Where(x => x.IsActive == active); }
        if (term != null) query = query.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        var total = await repo.CountAsync(query, ct);
        var ascending = filter.SortDirection == "asc";
        var ordered = (filter.SortBy, ascending) switch {
            ("code", true) => query.OrderBy(x => x.Code), ("code", false) => query.OrderByDescending(x => x.Code),
            ("name", true) => query.OrderBy(x => x.Name), ("name", false) => query.OrderByDescending(x => x.Name),
            ("version", true) => query.OrderBy(x => x.Version), ("version", false) => query.OrderByDescending(x => x.Version),
            ("createdAt", true) => query.OrderBy(x => x.CreatedAtUtc), ("createdAt", false) => query.OrderByDescending(x => x.CreatedAtUtc),
            (_, true) => query.OrderBy(x => x.Priority), _ => query.OrderByDescending(x => x.Priority)
        };
        ordered = ascending ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);
        var rows = await repo.ListAsync(ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize), ct);
        return new(rows.Select(Map).ToList(), filter.Page, filter.PageSize, total, (int)Math.Ceiling(total / (double)filter.PageSize));
    }
}
