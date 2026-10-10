using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class AssignmentDto
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long? AssignedUserId { get; set; }
    public long? AssignedDepartmentId { get; set; }
    [JsonPropertyName("assignedAt")] public DateTime AssignedAtUtc { get; set; }
    [JsonPropertyName("returnedAt")] public DateTime? ReturnedAtUtc { get; set; }
    public string? AssignmentNote { get; set; }
    public string? ReturnNote { get; set; }
    public string RowVersion { get; set; } = null!;
}

public sealed class AssignAssetRequest
{
    public long AssetId { get; set; }
    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }
    [JsonPropertyName("assignedAt")] public DateTime? AssignedAtUtc { get; set; }
    public string? Note { get; set; }
}

public sealed class ReturnAssetRequest
{
    public string? Note { get; set; }
    public string? RowVersion { get; set; }
    [JsonPropertyName("returnedAt")] public DateTime? ReturnedAtUtc { get; set; }
}

public sealed class AssignmentService(IRepository repo, IUnitOfWork uow, IAuditWriter audit, IActor actor)
{
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền thực hiện thao tác cấp phát.");
    }

    private static void PositiveId(long id, string field)
    {
        if (id <= 0) throw Validation.Invalid(field, "ID phải > 0.");
    }

    private static void Utc(DateTime? value, string field)
    {
        if (value is { Kind: not DateTimeKind.Utc })
            throw Validation.Invalid(field, "Thời điểm phải là UTC.");
    }

    private IQueryable<AssignmentDto> Query() => repo.Query<AssetAssignment>().Where(x => !x.IsArchived).Select(x => new AssignmentDto
    {
        Id = x.Id, AssetId = x.AssetId, AssignedUserId = x.AssignedUserId, AssignedDepartmentId = x.AssignedDepartmentId,
        AssignedAtUtc = x.AssignedAtUtc, ReturnedAtUtc = x.ReturnedAtUtc, AssignmentNote = x.AssignmentNote,
        ReturnNote = x.ReturnNote, RowVersion = Convert.ToBase64String(x.RowVersion),
    });

    public async Task<PagedResponse<AssignmentDto>> ListAsync(long? assetId, long? userId, long? departmentId,
        bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        Require(Permissions.AssignmentRead);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        if (assetId <= 0 || userId <= 0 || departmentId <= 0)
            throw Validation.Invalid("filter", "ID phải > 0 hoặc null.");

        var query = Query();
        if (assetId is { } a) query = query.Where(x => x.AssetId == a);
        if (userId is { } u) query = query.Where(x => x.AssignedUserId == u);
        if (departmentId is { } d) query = query.Where(x => x.AssignedDepartmentId == d);
        if (activeOnly) query = query.Where(x => x.ReturnedAtUtc == null);

        var total = await repo.CountAsync(query, ct);
        var order = query.OrderByDescending(x => x.AssignedAtUtc).ThenByDescending(x => x.Id);
        var items = await repo.ListAsync(order.Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<AssignmentDto> GetAsync(long id, CancellationToken ct)
    {
        Require(Permissions.AssignmentRead); PositiveId(id, "assignmentId");
        return await DetailAsync(id, ct);
    }

    // Command responses use the command's permission, not an extra read check after commit.
    private async Task<AssignmentDto> DetailAsync(long id, CancellationToken ct) =>
        await repo.FirstAsync(Query().Where(x => x.Id == id), ct) ?? throw NotFound();

    public async Task<AssignmentDto> AssignAsync(AssignAssetRequest x, CancellationToken ct)
    {
        Require(Permissions.AssignmentAssign); PositiveId(x.AssetId, "assetId");
        if ((x.UserId is null) == (x.DepartmentId is null))
            throw Validation.Invalid("userId", "Phải chọn đúng một trong hai: user hoặc department.");
        if (x.UserId <= 0 || x.DepartmentId <= 0) throw Validation.Invalid("target", "ID phải > 0.");
        var note = Validation.Optional(x.Note, 1000, "note"); Utc(x.AssignedAtUtc, "assignedAt");

        var resultId = await uow.RunAsync(async () =>
        {
            // Share the parent-row protocol with AssetService archive/status/update.
            await repo.LockAssetAsync(x.AssetId, ct);

            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == x.AssetId), ct)
                ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");
            if (asset.IsArchived) throw new BusinessException(409, "ASSET_ARCHIVED", "Không thể cấp phát tài sản đã archive.");
            if (asset.CurrentStatus != "IN_STOCK")
                throw new BusinessException(409, "ASSET_NOT_IN_STOCK", "Chỉ tài sản IN_STOCK mới cấp phát được.");

            var hasActive = await repo.AnyAsync(repo.Query<AssetAssignment>()
                .Where(a => a.AssetId == x.AssetId && a.ReturnedAtUtc == null && !a.IsArchived), ct);
            if (hasActive) throw new BusinessException(409, "ASSET_ALREADY_ASSIGNED", "Tài sản đã có assignment hiệu lực.");

            // Asset -> master-data -> users-identity is consistent with metadata writes.
            // These shared locks prevent target deactivation during validation/commit.
            await repo.LockAsync("master-data", ct);
            await repo.LockAsync("users-identity", ct);
            if (x.UserId is { } userId && !await repo.AnyAsync(repo.Query<User>().Where(u => u.Id == userId && u.IsActive), ct))
                throw Validation.Invalid("userId", "Người nhận không tồn tại/không hoạt động.");
            if (x.DepartmentId is { } departmentId && !await repo.AnyAsync(repo.Query<Department>().Where(d => d.Id == departmentId && d.IsActive), ct))
                throw Validation.Invalid("departmentId", "Phòng ban nhận không tồn tại/không hoạt động.");

            var assignedAt = x.AssignedAtUtc ?? DateTime.UtcNow;
            var assetBefore = new { asset.CurrentStatus, asset.IsArchived };

            var assignment = new AssetAssignment
            {
                AssetId = x.AssetId, AssignedUserId = x.UserId, AssignedDepartmentId = x.DepartmentId,
                AssignedAtUtc = assignedAt, AssignedByUserId = actor.UserId!.Value, AssignmentNote = note,
            };
            repo.Add(assignment);

            var oldStatus = asset.CurrentStatus;
            asset.CurrentStatus = "IN_USE";
            asset.UpdatedByUserId = actor.UserId;

            var history = new AssetStatusHistory
            {
                AssetId = asset.Id, FromStatus = oldStatus, ToStatus = "IN_USE", Source = "ASSIGNMENT",
                ChangedAtUtc = assignedAt, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId,
                Reason = note ?? "Cấp phát tài sản",
            };
            repo.Add(history);

            await repo.SaveAsync(ct);
            audit.Record("assignments.assign", assignment, actor.UserId, "USER",
                after: Snapshot(assignment));
            audit.Record("assignments.assign", asset, actor.UserId, "USER", before: assetBefore,
                after: new { asset.CurrentStatus, asset.IsArchived });
            await repo.SaveAsync(ct);
            return assignment.Id;
        }, ct);

        return await DetailAsync(resultId, ct);
    }

    public async Task<AssignmentDto> ReturnAsync(long id, ReturnAssetRequest x, CancellationToken ct)
    {
        Require(Permissions.AssignmentReturn); PositiveId(id, "assignmentId");
        var version = Validation.Version(x.RowVersion);
        var note = Validation.Required(x.Note, 1000, "note"); Utc(x.ReturnedAtUtc, "returnedAt");
        await uow.RunAsync(async () =>
        {
            var observed = await repo.FirstAsync(repo.Query<AssetAssignment>().Where(a => a.Id == id), ct) ?? throw NotFound();
            await repo.LockAssetAsync(observed.AssetId, ct);

            var assignment = await repo.FirstAsync(repo.Query<AssetAssignment>(true).Where(a => a.Id == id && a.AssetId == observed.AssetId), ct)
                ?? throw NotFound();
            if (!assignment.RowVersion.SequenceEqual(version))
                throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi; tải lại trước khi lưu.");
            if (assignment.ReturnedAtUtc != null || assignment.IsArchived)
                throw new BusinessException(409, "ASSIGNMENT_ALREADY_RETURNED", "Assignment đã được thu hồi.");

            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == assignment.AssetId), ct)
                ?? throw new BusinessException(500, "DATA_INCONSISTENT", "Không tìm thấy tài sản liên kết với assignment này.");

            if (asset.IsArchived || asset.CurrentStatus is not ("IN_USE" or "MAINTENANCE" or "BROKEN"))
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Trạng thái tài sản không phù hợp để thu hồi.");
            var inProgress = await repo.AnyAsync(repo.Query<MaintenanceTicket>().Where(t =>
                t.AssetId == asset.Id && t.Status == "IN_PROGRESS" && !t.IsArchived), ct);
            if (asset.CurrentStatus == "MAINTENANCE" && !inProgress || asset.CurrentStatus == "IN_USE" && inProgress)
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Trạng thái bảo trì và tài sản không nhất quán.");

            var returnedAt = x.ReturnedAtUtc ?? DateTime.UtcNow;
            if (returnedAt < assignment.AssignedAtUtc)
                throw Validation.Invalid("returnedAt", "Không được trước thời điểm cấp phát.");
            var before = Snapshot(assignment);
            var assetBefore = new { asset.CurrentStatus, asset.IsArchived };
            repo.ExpectVersion(assignment, version);

            assignment.ReturnedAtUtc = returnedAt;
            assignment.ReturnedByUserId = actor.UserId;
            assignment.ReturnNote = note;

            var oldStatus = asset.CurrentStatus;
            if (oldStatus == "IN_USE")
            {
                asset.CurrentStatus = "IN_STOCK"; asset.UpdatedByUserId = actor.UserId;
                repo.Add(new AssetStatusHistory
                {
                    AssetId = asset.Id, FromStatus = oldStatus, ToStatus = "IN_STOCK", Source = "ASSIGNMENT",
                    ChangedAtUtc = returnedAt, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId, Reason = note,
                });
            }

            await repo.SaveAsync(ct);
            audit.Record("assignments.return", assignment, actor.UserId, "USER", before: before, after: Snapshot(assignment));
            audit.Record("assignments.return", asset, actor.UserId, "USER", before: assetBefore,
                after: new { asset.CurrentStatus, asset.IsArchived });
            await repo.SaveAsync(ct);
            return true;
        }, ct);

        return await DetailAsync(id, ct);
    }

    // Deliberately omit notes/free text from the audit payload.
    private static object Snapshot(AssetAssignment assignment) => new
    {
        assignment.AssetId, assignment.AssignedUserId, assignment.AssignedDepartmentId, assignment.AssignedByUserId,
        assignment.ReturnedByUserId, assignment.AssignedAtUtc, assignment.ReturnedAtUtc,
        reasonProvided = !string.IsNullOrEmpty(assignment.ReturnNote),
    };

    private static BusinessException NotFound() => new(404, "ASSIGNMENT_NOT_FOUND", "Không tìm thấy assignment.");
}
