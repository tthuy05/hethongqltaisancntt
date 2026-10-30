using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class ReplacementRecommendationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? AssetId { get; set; }
    public long? DepartmentId { get; set; }
    public DateTime? GeneratedFrom { get; set; }
    public DateTime? GeneratedTo { get; set; }
    public string SortBy { get; set; } = "recommendedAt";
    public string SortDirection { get; set; } = "desc";
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public bool? IsCurrent { get; set; }
}
public sealed record ReplacementRecommendationDto(long Id, long AssetId, long ReplacementRuleId, string Disposition,
    bool IsCurrent, string Priority, string Reason, short? PlannedReplacementYear, DateTime RecommendedAt,
    string? DispositionNote, string RowVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? EstimatedReplacementCost,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EvaluationSnapshotJson);
public sealed class ReplacementDispositionRequest
{
    public string Disposition { get; set; } = "";
    public short? PlannedReplacementYear { get; set; }
    public string Note { get; set; } = "";
    public string? RowVersion { get; set; }
}
public sealed class ReplacementRecommendationService(IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit)
{
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.AssetRead) || !actor.Has(Permissions.ReplacementRead) || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền xem hoặc xử lý đề xuất thay thế.");
    }
    private ReplacementRecommendationDto Map(ReplacementRecommendation row)
    {
        var cost = actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        return new(row.Id, row.AssetId, row.ReplacementRuleId, row.Disposition, row.IsCurrent, row.Priority,
            cost ? row.Reason : "Tài sản khớp quy tắc thay thế đã cấu hình.", row.PlannedReplacementYear, row.RecommendedAtUtc,
            row.DispositionNote, Convert.ToBase64String(row.RowVersion), cost ? row.EstimatedReplacementCost : null,
            cost ? row.EvaluationSnapshotJson : null);
    }
    public async Task<ReplacementRecommendationDto> GetAsync(long id, CancellationToken ct)
    {
        Require(Permissions.ReplacementRead);
        if (id <= 0) throw Validation.Invalid("recommendationId", "ID phải > 0.");
        return Map(await repo.FirstAsync(repo.Query<ReplacementRecommendation>().Where(x => x.Id == id && !x.IsArchived), ct)
            ?? throw new BusinessException(404, "RECOMMENDATION_NOT_FOUND", "Không tìm thấy đề xuất."));
    }
    public async Task<PagedResponse<ReplacementRecommendationDto>> ListAsync(ReplacementRecommendationQuery filter, CancellationToken ct)
    {
        Require(Permissions.ReplacementRead);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "Phân trang không hợp lệ.");
        if (filter.AssetId is <= 0 || filter.DepartmentId is <= 0) throw Validation.Invalid("assetId", "ID phải > 0.");
        if (filter.GeneratedFrom is {} from && from.Kind != DateTimeKind.Utc || filter.GeneratedTo is {} to && to.Kind != DateTimeKind.Utc ||
            filter.GeneratedFrom.HasValue && filter.GeneratedTo.HasValue && filter.GeneratedFrom >= filter.GeneratedTo)
            throw Validation.Invalid("generatedFrom", "Khoảng thời gian UTC phải có từ trước đến.");
        if (filter.SortBy is not ("recommendedAt" or "priority" or "plannedReplacementYear") || filter.SortDirection is not ("asc" or "desc"))
            throw Validation.Invalid("sortBy", "Cột hoặc chiều sắp xếp không hợp lệ.");
        if (filter.Priority is not (null or "LOW" or "MEDIUM" or "HIGH" or "CRITICAL")) throw Validation.Invalid("priority", "Mức ưu tiên không hợp lệ.");
        if (filter.Status is not (null or "ACTIVE" or "PLANNED" or "DISMISSED" or "SUPERSEDED")) throw Validation.Invalid("status", "Trạng thái không hợp lệ.");
        var query = repo.Query<ReplacementRecommendation>().Where(x => !x.IsArchived);
        if (filter.AssetId.HasValue) query = query.Where(x => x.AssetId == filter.AssetId.Value);
        if (filter.DepartmentId.HasValue) {
            var ids = repo.Query<Asset>().Where(x => x.OwningDepartmentId == filter.DepartmentId.Value).Select(x => x.Id);
            query = query.Where(x => ids.Contains(x.AssetId));
        }
        if (filter.GeneratedFrom.HasValue) query = query.Where(x => x.RecommendedAtUtc >= filter.GeneratedFrom.Value);
        if (filter.GeneratedTo.HasValue) query = query.Where(x => x.RecommendedAtUtc < filter.GeneratedTo.Value);
        if (filter.Priority != null) query = query.Where(x => x.Priority == filter.Priority);
        if (filter.Status != null) query = query.Where(x => x.Disposition == filter.Status);
        if (filter.IsCurrent.HasValue) query = query.Where(x => x.IsCurrent == filter.IsCurrent.Value);
        var total = await repo.CountAsync(query, ct);
        var asc = filter.SortDirection == "asc";
        var ordered = (filter.SortBy, asc) switch {
            ("priority", true) => query.OrderBy(x => x.Priority == "CRITICAL" ? 4 : x.Priority == "HIGH" ? 3 : x.Priority == "MEDIUM" ? 2 : 1),
            ("priority", false) => query.OrderByDescending(x => x.Priority == "CRITICAL" ? 4 : x.Priority == "HIGH" ? 3 : x.Priority == "MEDIUM" ? 2 : 1),
            ("plannedReplacementYear", true) => query.OrderBy(x => x.PlannedReplacementYear), ("plannedReplacementYear", false) => query.OrderByDescending(x => x.PlannedReplacementYear),
            (_, true) => query.OrderBy(x => x.RecommendedAtUtc), _ => query.OrderByDescending(x => x.RecommendedAtUtc)
        };
        ordered = asc ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);
        var rows = await repo.ListAsync(ordered
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize), ct);
        return new(rows.Select(Map).ToList(), filter.Page, filter.PageSize, total, (int)Math.Ceiling(total / (double)filter.PageSize));
    }
    public async Task<ReplacementRecommendationDto> DispositionAsync(long id, ReplacementDispositionRequest request, CancellationToken ct)
    {
        Require(Permissions.ReplacementUpdate);
        if (id <= 0) throw Validation.Invalid("recommendationId", "ID phải > 0.");
        if (request.Disposition is not ("PLANNED" or "DISMISSED")) throw Validation.Invalid("disposition", "Chọn PLANNED hoặc DISMISSED.");
        var note = Validation.Required(request.Note, 2000, "note"); var expected = Validation.Version(request.RowVersion);
        if (request.Disposition == "PLANNED" && request.PlannedReplacementYear is not (>= 2000 and <= 2100) ||
            request.Disposition == "DISMISSED" && request.PlannedReplacementYear != null)
            throw Validation.Invalid("plannedReplacementYear", "Chọn năm 2000–2100 khi lập kế hoạch, không gửi năm khi bỏ qua.");
        return await uow.RunAsync(async () => {
            await repo.LockAsync("replacement-rules", ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.AssetRead, Permissions.ReplacementRead, Permissions.ReplacementUpdate);
            if (actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost)) {
                var grants = await repo.ListAsync(EffectivePermissionQuery.ForUser(repo, actor.UserId!.Value), ct);
                if (!grants.Contains(Permissions.AssetCost) && !grants.Contains(Permissions.ReportCost))
                    throw new BusinessException(403, "FORBIDDEN", "Quyền xem giá đã thay đổi.");
            }
            var row = await repo.FirstAsync(repo.Query<ReplacementRecommendation>(true).Where(x => x.Id == id && !x.IsArchived), ct)
                ?? throw new BusinessException(404, "RECOMMENDATION_NOT_FOUND", "Không tìm thấy đề xuất.");
            if (!row.RowVersion.SequenceEqual(expected)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Đề xuất đã thay đổi. Hãy tải lại.");
            if (!row.IsCurrent) throw new BusinessException(409, "RECOMMENDATION_SUPERSEDED", "Đề xuất không còn hiện hành.");
            repo.ExpectVersion(row, expected);
            var before = new { row.Disposition, row.PlannedReplacementYear };
            if (row.Disposition == "PLANNED" && request.Disposition == "PLANNED") {
                if (row.PlannedReplacementYear == request.PlannedReplacementYear && row.DispositionNote == note)
                    throw new BusinessException(409, "DISPOSITION_UNCHANGED", "Kế hoạch không thay đổi.");
                row.DispositionNote = note; row.DispositionAtUtc = DateTime.UtcNow; row.DispositionByUserId = actor.UserId;
            } else ReplacementRecommendationPolicy.ApplyDisposition(row, request.Disposition, actor.UserId!.Value, DateTime.UtcNow, note);
            if (request.Disposition == "PLANNED") row.PlannedReplacementYear = request.PlannedReplacementYear;
            audit.Record("replacement.disposition", row, actor.UserId, "USER", before: before, after: new { row.Disposition, row.IsCurrent, row.PlannedReplacementYear });
            await repo.SaveAsync(ct); return Map(row);
        }, ct);
    }
}
