using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class ReplacementRuleWriteRequest
{
    public string Severity { get; set; } = "";
    public string Name { get; set; } = "";
    public long? AssetTypeId { get; set; }
    public int? MinimumAgeMonths { get; set; }
    public decimal? MaximumMaintenanceCostRatio { get; set; }
    public int? MinimumMaintenanceCount { get; set; }
    public bool RequireWarrantyExpired { get; set; }
    public int? MinimumFailureCount { get; set; }
    private decimal? cost;
    public decimal? EstimatedUnitCost { get => cost; set { cost = value; HasEstimatedUnitCost = true; } }
    [JsonIgnore] public bool HasEstimatedUnitCost { get; private set; }
    public int Priority { get; set; } = 100;
    public string? Description { get; set; }
    public string? RowVersion { get; set; }
}
public sealed class ReplacementRuleStatusRequest
{
    public bool? IsActive { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ReplacementRuleCommandService(IRepository repo, IUnitOfWork uow, IActor actor,
    IAuditWriter audit, ReplacementRuleQueryService queries)
{
    private static BusinessException Denied() => new(403, "FORBIDDEN", "Không có quyền quản lý quy tắc thay thế.");
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.ReplacementRuleRead) || !actor.Has(permission)) throw Denied();
    }
    private async Task AuthorizeAsync(string permission, bool writesCost, CancellationToken ct)
    {
        await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.ReplacementRuleRead, permission);
        // Check active membership and financial grants from the database inside the identity lock.
        // A role name in a token cannot authorize these administrative commands.
        await repo.LockAsync("users-identity", ct);
        var user = await repo.FirstAsync(repo.Query<User>().Where(x => x.Id == actor.UserId), ct);
        var admin = from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
                    where link.UserId == actor.UserId && role.Code == "ADMIN_IT" && role.IsActive select role.Id;
        if (user == null || !user.IsActive || !user.IsEmailConfirmed || user.IsAdminLocked || user.LockoutEndUtc > DateTime.UtcNow ||
            !await repo.AnyAsync(admin, ct)) throw Denied();
        var grants = await repo.ListAsync(EffectivePermissionQuery.ForUser(repo, user.Id), ct);
        if (!grants.Contains(permission) || !grants.Contains(Permissions.ReplacementRuleRead)) throw Denied();
        if ((writesCost || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost)) &&
            !grants.Contains(Permissions.AssetCost) && !grants.Contains(Permissions.ReportCost)) throw Denied();
    }
    private static void Validate(ReplacementRuleWriteRequest request)
    {
        if (request.Severity is not ("LOW" or "MEDIUM" or "HIGH" or "CRITICAL"))
            throw Validation.Invalid("severity", "Chọn mức LOW, MEDIUM, HIGH hoặc CRITICAL.");
        request.Name = Validation.Required(request.Name, 250, "name");
        request.Description = Validation.Optional(request.Description, 2000, "description");
        if (request.AssetTypeId is <= 0) throw Validation.Invalid("assetTypeId", "ID phải là số nguyên dương.");
        if (request.MinimumAgeMonths is <= 0 || request.MinimumMaintenanceCount is <= 0 || request.MinimumFailureCount is <= 0)
            throw Validation.Invalid("threshold", "Ngưỡng số lượng/tuổi phải lớn hơn 0.");
        if (request.MaximumMaintenanceCostRatio is < 0 or > 1 || request.MaximumMaintenanceCostRatio is {} ratio && decimal.Round(ratio, 4) != ratio)
            throw Validation.Invalid("maximumMaintenanceCostRatio", "Tỷ lệ phải từ 0 đến 1, tối đa 4 chữ số thập phân.");
        if (request.RequireWarrantyExpired != request.MinimumFailureCount.HasValue)
            throw Validation.Invalid("minimumFailureCount", "Điều kiện hết bảo hành phải đi cùng ngưỡng số lần thất bại.");
        if (request.MinimumAgeMonths == null && request.MaximumMaintenanceCostRatio == null && request.MinimumMaintenanceCount == null && !request.RequireWarrantyExpired)
            throw Validation.Invalid("threshold", "Cần ít nhất một điều kiện đánh giá.");
        if (request.EstimatedUnitCost is < 0 or > 9999999999999999.99m || request.EstimatedUnitCost is {} cost && decimal.Round(cost, 2) != cost)
            throw Validation.Invalid("estimatedUnitCost", "Giá phải không âm, trong numeric(18,2).");
        if (request.Priority < 0) throw Validation.Invalid("priority", "Thứ tự không được âm.");
    }
    private async Task<ReplacementRule> HeadAsync(long id, byte[] expected, CancellationToken ct)
    {
        var row = await repo.FirstAsync(repo.Query<ReplacementRule>(true).Where(x => x.Id == id), ct)
            ?? throw new BusinessException(404, "REPLACEMENT_RULE_NOT_FOUND", "Không tìm thấy quy tắc thay thế.");
        if (!row.RowVersion.SequenceEqual(expected)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Quy tắc đã thay đổi. Hãy tải lại.");
        repo.ExpectVersion(row, expected);
        var code = row.Code.ToLowerInvariant();
        if (await repo.AnyAsync(repo.Query<ReplacementRule>().Where(x => x.Code.ToLower() == code && x.Version > row.Version), ct))
            throw new BusinessException(409, "RULE_SUPERSEDED", "Chỉ được thay đổi phiên bản mới nhất.");
        return row;
    }
    public async Task<ReplacementRuleDto> WriteAsync(long? id, ReplacementRuleWriteRequest request, CancellationToken ct)
    {
        var permission = id.HasValue ? Permissions.ReplacementRuleUpdate : Permissions.ReplacementRuleCreate;
        Require(permission); Validate(request);
        if (id is <= 0) throw Validation.Invalid("ruleId", "ID phải là số nguyên dương.");
        var expected = id.HasValue ? Validation.Version(request.RowVersion) : null;
        if (!id.HasValue && request.RowVersion != null) throw Validation.Invalid("rowVersion", "Không gửi phiên bản khi tạo mới.");
        if (request.HasEstimatedUnitCost && !actor.Has(Permissions.AssetCost) && !actor.Has(Permissions.ReportCost)) throw Denied();
        return await uow.RunAsync(async () => {
            // Global rule coordinator also serializes future evaluators with publication.
            await repo.LockAsync("replacement-rules", ct);
            await AuthorizeAsync(permission, request.HasEstimatedUnitCost, ct);
            var old = id.HasValue ? await HeadAsync(id.Value, expected!, ct) : null;
            var now = DateTime.UtcNow;
            if (old != null && (old.EffectiveFromUtc >= now || old.EffectiveToUtc.HasValue || old.Version == int.MaxValue))
                throw new BusinessException(409, "RULE_REVISION_UNAVAILABLE", "Không thể tạo phiên bản mới từ quy tắc này.");
            if (request.AssetTypeId is {} type && (old == null || old.AssetTypeId != type) &&
                !await repo.AnyAsync(repo.Query<AssetType>().Where(x => x.Id == type && x.IsActive), ct))
                throw Validation.Invalid("assetTypeId", "Loại tài sản phải tồn tại và đang hoạt động.");
            if (old != null) {
                old.IsActive = false; old.EffectiveToUtc = now; old.UpdatedAtUtc = now;
                await repo.SaveAsync(ct); // Release the unique active-family slot before inserting the revision.
                audit.Record("replacement-rules.supersede", old, actor.UserId, "USER", after: new { ruleVersion = old.Version, old.IsActive });
            }
            var row = new ReplacementRule {
                Severity = request.Severity,
                Code = old?.Code ?? "RR-" + Guid.NewGuid().ToString("N"), Version = old == null ? 1 : old.Version + 1,
                Name = request.Name, AssetTypeId = request.AssetTypeId, MinimumAgeMonths = request.MinimumAgeMonths,
                MaximumMaintenanceCostRatio = request.MaximumMaintenanceCostRatio, MinimumMaintenanceCount = request.MinimumMaintenanceCount,
                RequireWarrantyExpired = request.RequireWarrantyExpired, MinimumFailureCount = request.MinimumFailureCount,
                EstimatedUnitCost = request.HasEstimatedUnitCost ? request.EstimatedUnitCost : old?.EstimatedUnitCost,
                Priority = request.Priority, Description = request.Description, EffectiveFromUtc = now, CreatedAtUtc = now,
                IsActive = true, RowVersion = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)
            };
            repo.Add(row); await repo.SaveAsync(ct);
            audit.Record(old == null ? "replacement-rules.create" : "replacement-rules.revise", row, actor.UserId, "USER",
                after: new { row.Code, ruleVersion = row.Version, row.IsActive });
            await repo.SaveAsync(ct);
            return await queries.GetAsync(row.Id, ct);
        }, ct);
    }
    public async Task<ReplacementRuleDto> StatusAsync(long id, ReplacementRuleStatusRequest request, CancellationToken ct)
    {
        Require(Permissions.ReplacementRuleUpdate);
        if (id <= 0) throw Validation.Invalid("ruleId", "ID phải là số nguyên dương.");
        if (request.IsActive == null) throw Validation.Invalid("isActive", "Trạng thái là bắt buộc.");
        var expected = Validation.Version(request.RowVersion);
        return await uow.RunAsync(async () => {
            await repo.LockAsync("replacement-rules", ct);
            await AuthorizeAsync(Permissions.ReplacementRuleUpdate, false, ct);
            var row = await HeadAsync(id, expected, ct);
            if (request.IsActive == true && (row.Severity == null || ReplacementEvaluationEngine.SeverityRank(row.Severity) == 0))
                throw new BusinessException(409, "RULE_CONFIGURATION_INCOMPLETE", "Cần revision để cấu hình mức ưu tiên trước khi kích hoạt.");
            if (row.EffectiveToUtc.HasValue || row.EffectiveFromUtc > DateTime.UtcNow)
                throw new BusinessException(409, "RULE_REVISION_UNAVAILABLE", "Quy tắc không còn hiệu lực hoặc chưa hiệu lực.");
            if (row.IsActive == request.IsActive.Value) throw new BusinessException(409, "RULE_STATUS_UNCHANGED", "Trạng thái không thay đổi.");
            var before = new { row.IsActive };
            row.IsActive = request.IsActive.Value; row.UpdatedAtUtc = DateTime.UtcNow;
            audit.Record("replacement-rules.status", row, actor.UserId, "USER", before: before, after: new { row.IsActive });
            await repo.SaveAsync(ct); return await queries.GetAsync(row.Id, ct);
        }, ct);
    }
}
