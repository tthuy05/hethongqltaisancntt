using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class AssignmentDto
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public long? AssignedUserId { get; set; }
    public long? AssignedDepartmentId { get; set; }
    public string? AssetCode { get; set; }
    public string? AssetName { get; set; }
    public string? AssignedUserName { get; set; }
    public string? AssignedDepartmentName { get; set; }
    public long AssignedByUserId { get; set; }
    public long? ReturnedByUserId { get; set; }
    public string Status { get; set; } = "Active";
    [JsonPropertyName("expectedReturnAt")] public DateTime? ExpectedReturnAtUtc { get; set; }
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
    [JsonPropertyName("expectedReturnAt")] public DateTime? ExpectedReturnAtUtc { get; set; }
    public string? Note { get; set; }
}

public sealed class ReturnAssetRequest
{
    public string? Condition { get; set; }
    public string? Note { get; set; }
    public string? RowVersion { get; set; }
    [JsonPropertyName("returnedAt")] public DateTime? ReturnedAtUtc { get; set; }
}

public sealed class TransferAssetRequest
{
    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }
    [JsonPropertyName("transferredAt")] public DateTime? TransferredAtUtc { get; set; }
    [JsonPropertyName("expectedReturnAt")] public DateTime? ExpectedReturnAtUtc { get; set; }
    public string? ReturnCondition { get; set; }
    public string? Note { get; set; }
    public string? RowVersion { get; set; }
}
public sealed record AssignmentTransferDto(AssignmentDto Previous, AssignmentDto Current);
public sealed class AssignmentListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? AssetId { get; set; }
    public long? UserId { get; set; }
    public long? DepartmentId { get; set; }
    public bool? Active { get; set; }
    public string? Status { get; set; }
    [JsonPropertyName("from")] public DateTime? From { get; set; }
    [JsonPropertyName("to")] public DateTime? To { get; set; }
    public string SortBy { get; set; } = "assignedAt";
    public string SortDirection { get; set; } = "desc";
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

    private IQueryable<AssignmentDto> Query() =>
        from x in repo.Query<AssetAssignment>()
        join asset in repo.Query<Asset>() on x.AssetId equals asset.Id
        join user in repo.Query<User>() on x.AssignedUserId equals (long?)user.Id into users
        from user in users.DefaultIfEmpty()
        join department in repo.Query<Department>() on x.AssignedDepartmentId equals (long?)department.Id into departments
        from department in departments.DefaultIfEmpty()
        where !x.IsArchived
        select new AssignmentDto
    {
        Id = x.Id, AssetId = x.AssetId, AssignedUserId = x.AssignedUserId, AssignedDepartmentId = x.AssignedDepartmentId,
        AssetCode = asset.AssetCode, AssetName = asset.Name, AssignedUserName = user == null ? null : user.FullName,
        AssignedDepartmentName = department == null ? null : department.Name,
        AssignedByUserId = x.AssignedByUserId, ReturnedByUserId = x.ReturnedByUserId,
        ExpectedReturnAtUtc = x.ExpectedReturnAtUtc, Status = x.ReturnedAtUtc == null ? "Active" : "Returned",
        AssignedAtUtc = x.AssignedAtUtc, ReturnedAtUtc = x.ReturnedAtUtc, AssignmentNote = x.AssignmentNote,
        ReturnNote = x.ReturnNote, RowVersion = Convert.ToBase64String(x.RowVersion),
    };

    public Task<PagedResponse<AssignmentDto>> ListAsync(long? assetId, long? userId, long? departmentId,
        bool activeOnly, int page, int pageSize, CancellationToken ct) => ListAsync(new()
        { AssetId = assetId, UserId = userId, DepartmentId = departmentId, Active = activeOnly, Page = page, PageSize = pageSize }, ct);

    public async Task<PagedResponse<AssignmentDto>> ListAsync(AssignmentListQuery filter, CancellationToken ct)
    {
        Require(Permissions.AssignmentRead);
        var (page, pageSize, assetId, userId, departmentId) = (filter.Page, filter.PageSize, filter.AssetId, filter.UserId, filter.DepartmentId);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        if (assetId <= 0 || userId <= 0 || departmentId <= 0)
            throw Validation.Invalid("filter", "ID phải > 0 hoặc null.");

        Utc(filter.From, "from"); Utc(filter.To, "to");
        if (filter.From > filter.To) throw Validation.Invalid("to", "Thời điểm kết thúc phải sau thời điểm bắt đầu.");
        if (filter.Status is not (null or "Active" or "Returned") || filter.Active == true && filter.Status == "Returned")
            throw Validation.Invalid("status", "Status là Active/Returned; không xung đột active=true.");
        if (filter.SortBy is not ("assignedAt" or "returnedAt" or "id") || filter.SortDirection is not ("asc" or "desc"))
            throw Validation.Invalid("sortBy", "Sort theo assignedAt/returnedAt/id, direction asc/desc.");
        var query = Query();
        if (assetId is { } a) query = query.Where(x => x.AssetId == a);
        if (userId is { } u) query = query.Where(x => x.AssignedUserId == u);
        if (departmentId is { } d) query = query.Where(x => x.AssignedDepartmentId == d);
        if (filter.Active == true || filter.Status == "Active") query = query.Where(x => x.ReturnedAtUtc == null);
        if (filter.Status == "Returned") query = query.Where(x => x.ReturnedAtUtc != null);
        if (filter.From is {} from) query = query.Where(x => x.AssignedAtUtc >= from);
        if (filter.To is {} to) query = query.Where(x => x.AssignedAtUtc <= to);

        var total = await repo.CountAsync(query, ct);
        var ascending = filter.SortDirection == "asc";
        var order = filter.SortBy switch {
            "returnedAt" => ascending ? query.OrderBy(x => x.ReturnedAtUtc).ThenBy(x => x.Id) : query.OrderByDescending(x => x.ReturnedAtUtc).ThenByDescending(x => x.Id),
            "id" => ascending ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id),
            _ => ascending ? query.OrderBy(x => x.AssignedAtUtc).ThenBy(x => x.Id) : query.OrderByDescending(x => x.AssignedAtUtc).ThenByDescending(x => x.Id)
        };
        var items = await repo.ListAsync(order.Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<PagedResponse<AssignmentDto>> HistoryAsync(long assetId, AssignmentListQuery filter, CancellationToken ct)
    {
        Require(Permissions.AssignmentRead); PositiveId(assetId, "assetId");
        if (!await repo.AnyAsync(repo.Query<Asset>().Where(x => x.Id == assetId), ct))
            throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");
        filter.AssetId = assetId;
        return await ListAsync(filter, ct);
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
        var note = Validation.Optional(x.Note, 1000, "note"); Utc(x.AssignedAtUtc, "assignedAt"); Utc(x.ExpectedReturnAtUtc, "expectedReturnAt");

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
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.AssignmentRead, Permissions.AssignmentAssign);
            if (x.UserId is { } userId && !await repo.AnyAsync(repo.Query<User>().Where(u => u.Id == userId && u.IsActive), ct))
                throw Validation.Invalid("userId", "Người nhận không tồn tại/không hoạt động.");
            if (x.DepartmentId is { } departmentId && !await repo.AnyAsync(repo.Query<Department>().Where(d => d.Id == departmentId && d.IsActive), ct))
                throw Validation.Invalid("departmentId", "Phòng ban nhận không tồn tại/không hoạt động.");

            var assignedAt = x.AssignedAtUtc ?? DateTime.UtcNow;
            if (x.ExpectedReturnAtUtc < assignedAt) throw Validation.Invalid("expectedReturnAt", "Hạn trả không được trước thời điểm cấp phát.");
            var assetBefore = new { asset.CurrentStatus, asset.IsArchived };

            var assignment = new AssetAssignment
            {
                AssetId = x.AssetId, AssignedUserId = x.UserId, AssignedDepartmentId = x.DepartmentId,
                AssignedAtUtc = assignedAt, AssignedByUserId = actor.UserId!.Value, AssignmentNote = note,
                ExpectedReturnAtUtc = x.ExpectedReturnAtUtc,
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
        var note = ReturnNote(x.Note, x.Condition); Utc(x.ReturnedAtUtc, "returnedAt");
        await uow.RunAsync(async () =>
        {
            var observed = await repo.FirstAsync(repo.Query<AssetAssignment>().Where(a => a.Id == id), ct) ?? throw NotFound();
            await repo.LockAssetAsync(observed.AssetId, ct);

            var assignment = await repo.FirstAsync(repo.Query<AssetAssignment>(true).Where(a => a.Id == id && a.AssetId == observed.AssetId), ct)
                ?? throw NotFound();
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.AssignmentRead, Permissions.AssignmentReturn);
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

    public async Task<AssignmentTransferDto> TransferAsync(long id, TransferAssetRequest request, CancellationToken ct)
    {
        Require(Permissions.AssignmentTransfer); PositiveId(id, "assignmentId");
        var version = Validation.Version(request.RowVersion);
        if ((request.UserId is null) == (request.DepartmentId is null) || request.UserId <= 0 || request.DepartmentId <= 0)
            throw Validation.Invalid("target", "Chọn đúng một User hoặc Department hợp lệ.");
        Utc(request.TransferredAtUtc, "transferredAt"); Utc(request.ExpectedReturnAtUtc, "expectedReturnAt");
        var note = ReturnNote(request.Note, request.ReturnCondition);
        return await uow.RunAsync(async () =>
        {
            var observed = await repo.FirstAsync(repo.Query<AssetAssignment>().Where(x => x.Id == id), ct) ?? throw NotFound();
            await repo.LockAssetAsync(observed.AssetId, ct);
            var previous = await repo.FirstAsync(repo.Query<AssetAssignment>(true).Where(x => x.Id == id), ct) ?? throw NotFound();
            if (!previous.RowVersion.SequenceEqual(version)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi; hãy tải lại.");
            if (previous.IsArchived || previous.ReturnedAtUtc != null) throw new BusinessException(409, "ASSIGNMENT_ALREADY_RETURNED", "Cấp phát đã đóng.");
            var asset = await repo.FirstAsync(repo.Query<Asset>().Where(x => x.Id == previous.AssetId), ct)
                ?? throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Tài sản không còn khả dụng.");
            if (asset.IsArchived || asset.CurrentStatus != "IN_USE" || await repo.AnyAsync(repo.Query<MaintenanceTicket>().Where(x =>
                    x.AssetId == asset.Id && x.Status == "IN_PROGRESS" && !x.IsArchived), ct))
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Chỉ điều chuyển tài sản IN_USE, không đang bảo trì.");
            var active = await repo.CountAsync(repo.Query<AssetAssignment>().Where(x => x.AssetId == asset.Id && x.ReturnedAtUtc == null && !x.IsArchived), ct);
            if (active != 1) throw new BusinessException(409, "ASSIGNMENT_STATE_CONFLICT", "Tài sản phải có đúng một cấp phát hiệu lực.");
            if (request.UserId == previous.AssignedUserId && request.DepartmentId == previous.AssignedDepartmentId)
                throw new BusinessException(409, "ASSIGNMENT_SAME_TARGET", "Người/phòng ban nhận phải khác hiện tại.");
            await repo.LockAsync("master-data", ct);
            await repo.LockAsync("users-identity", ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.AssignmentRead, Permissions.AssignmentTransfer);
            if (request.UserId is {} userId && !await repo.AnyAsync(repo.Query<User>().Where(x => x.Id == userId && x.IsActive), ct) ||
                request.DepartmentId is {} departmentId && !await repo.AnyAsync(repo.Query<Department>().Where(x => x.Id == departmentId && x.IsActive), ct))
                throw Validation.Invalid("target", "Người/phòng ban nhận không tồn tại hoặc đã ngừng hoạt động.");
            var transferredAt = request.TransferredAtUtc ?? DateTime.UtcNow;
            if (transferredAt < previous.AssignedAtUtc) throw Validation.Invalid("transferredAt", "Không được trước thời điểm cấp phát cũ.");
            if (request.ExpectedReturnAtUtc < transferredAt) throw Validation.Invalid("expectedReturnAt", "Hạn trả không được trước thời điểm điều chuyển.");
            var before = Snapshot(previous);
            repo.ExpectVersion(previous, version);
            previous.ReturnedAtUtc = transferredAt; previous.ReturnedByUserId = actor.UserId; previous.ReturnNote = note;
            // Release the partial unique index before INSERT; both saves share the same audited transaction.
            await repo.SaveAsync(ct);
            var current = new AssetAssignment { AssetId = asset.Id, AssignedUserId = request.UserId,
                AssignedDepartmentId = request.DepartmentId, AssignedAtUtc = transferredAt,
                AssignedByUserId = actor.UserId!.Value, AssignmentNote = Validation.Required(request.Note, 1000, "note"),
                ExpectedReturnAtUtc = request.ExpectedReturnAtUtc };
            repo.Add(current);
            await repo.SaveAsync(ct);
            audit.Record("assignments.transfer", previous, actor.UserId, "USER", before: before, after: Snapshot(previous));
            audit.Record("assignments.transfer", current, actor.UserId, "USER", after: new {
                current.AssetId, current.AssignedUserId, current.AssignedDepartmentId,
                previousAssignmentId = previous.Id, currentAssignmentId = current.Id });
            await repo.SaveAsync(ct);
            return new AssignmentTransferDto(await DetailAsync(previous.Id, ct), await DetailAsync(current.Id, ct));
        }, ct);
    }

    private static string ReturnNote(string? note, string? condition)
    {
        var reason = Validation.Required(note, 1000, "note");
        var state = Validation.Optional(condition, 200, "condition");
        return Validation.Required(state == null ? reason : $"Tình trạng: {state}\nLý do: {reason}", 1000, "note");
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
