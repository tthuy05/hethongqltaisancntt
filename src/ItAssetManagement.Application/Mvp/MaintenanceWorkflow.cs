using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed partial class MaintenanceService
{
    private static void Utc(DateTime? value, string field)
    { if (value is { Kind: not DateTimeKind.Utc }) throw Validation.Invalid(field, "Dùng ISO UTC kết thúc bằng Z."); }
    private static void Priority(string value)
    { if (value is not ("LOW" or "MEDIUM" or "HIGH" or "CRITICAL")) throw Validation.Invalid("priority", "Mức ưu tiên không hợp lệ."); }
    private static bool Terminal(MaintenanceTicket ticket) => ticket.Status is "RESOLVED" or "FAILED" or "CANCELLED";
    private static void Page(int page, int size)
    { if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue) throw Validation.Invalid("page", "Phân trang không hợp lệ."); }

    public async Task<PagedResponse<MaintenanceDto>> ListAsync(MaintenanceListQuery filter, CancellationToken ct)
    {
        Require(Permissions.MaintenanceRead); Page(filter.Page, filter.PageSize);
        foreach (var id in new[] { filter.AssetId, filter.ReporterId, filter.TechnicianId }) if (id <= 0) throw Validation.Invalid("filter", "ID phải > 0.");
        if (filter.Priority != null) Priority(filter.Priority);
        if (filter.Status is not (null or "PENDING" or "IN_PROGRESS" or "RESOLVED" or "FAILED" or "CANCELLED")) throw Validation.Invalid("status", "Trạng thái không hợp lệ.");
        Utc(filter.From, "from"); Utc(filter.To, "to");
        if (filter.From > filter.To) throw Validation.Invalid("to", "Khoảng thời gian không hợp lệ.");
        var keyword = Validation.Optional(filter.Keyword, 200, "keyword");
        if (filter.SortBy is not ("openedAt" or "dueAt" or "priority" or "id") || filter.SortDirection is not ("asc" or "desc")) throw Validation.Invalid("sortBy", "Cột/chiều sắp xếp không hợp lệ.");
        var query = Query();
        if (filter.AssetId is {} asset) query = query.Where(x => x.AssetId == asset);
        if (filter.ReporterId is {} reporter) query = query.Where(x => x.RequestedByUserId == reporter);
        if (filter.TechnicianId is {} technician) query = query.Where(x => x.AssignedToUserId == technician);
        if (filter.Status != null) query = query.Where(x => x.Status == filter.Status);
        if (filter.Priority != null) query = query.Where(x => x.Priority == filter.Priority);
        if (filter.From is {} from) query = query.Where(x => x.OpenedAtUtc >= from);
        if (filter.To is {} to) query = query.Where(x => x.OpenedAtUtc <= to);
        if (keyword != null) query = query.Where(x => x.Title.Contains(keyword) || x.Description.Contains(keyword) || x.TicketCode.Contains(keyword));
        var ascending = filter.SortDirection == "asc";
        var ordered = filter.SortBy switch {
            "dueAt" => ascending ? query.OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id) : query.OrderByDescending(x => x.DueAtUtc).ThenByDescending(x => x.Id),
            "priority" => ascending ? query.OrderBy(x => x.Priority == "CRITICAL" ? 4 : x.Priority == "HIGH" ? 3 : x.Priority == "MEDIUM" ? 2 : 1).ThenBy(x => x.Id)
                : query.OrderByDescending(x => x.Priority == "CRITICAL" ? 4 : x.Priority == "HIGH" ? 3 : x.Priority == "MEDIUM" ? 2 : 1).ThenByDescending(x => x.Id),
            "id" => ascending ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id),
            _ => ascending ? query.OrderBy(x => x.OpenedAtUtc).ThenBy(x => x.Id) : query.OrderByDescending(x => x.OpenedAtUtc).ThenByDescending(x => x.Id)
        };
        var count = await repo.CountAsync(query, ct);
        return new(await repo.ListAsync(ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize), ct), filter.Page, filter.PageSize, count, (int)Math.Ceiling(count / (double)filter.PageSize));
    }

    private async Task<MaintenanceTicket> LockedAsync(long id, byte[] version, MaintenanceTicketAction action, CancellationToken ct)
    {
        var observed = await repo.FirstAsync(access.ScopeRead(repo.Query<MaintenanceTicket>()).Where(x => x.Id == id && !x.IsArchived), ct) ?? throw NotFound();
        await repo.LockAssetAsync(observed.AssetId, ct);
        await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.MaintenanceRead, action switch {
            MaintenanceTicketAction.Read => Permissions.MaintenanceAssign,
            MaintenanceTicketAction.Update => Permissions.MaintenanceUpdate,
            MaintenanceTicketAction.Work => Permissions.MaintenanceWork,
            MaintenanceTicketAction.Resolve => Permissions.MaintenanceResolve,
            MaintenanceTicketAction.Cancel => Permissions.MaintenanceCancel,
            _ => throw new InvalidOperationException("Unknown maintenance command.")
        });
        var ticket = await repo.FirstAsync(repo.Query<MaintenanceTicket>(true).Where(x => x.Id == id && !x.IsArchived), ct) ?? throw NotFound();
        access.Require(ticket, action);
        if (!ticket.RowVersion.SequenceEqual(version)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Phiếu đã thay đổi, hãy tải lại.");
        repo.ExpectVersion(ticket, version);
        return ticket;
    }
    private async Task TechnicianAsync(long id, CancellationToken ct)
    {
        var eligible = from user in repo.Query<User>() join membership in repo.Query<UserRole>() on user.Id equals membership.UserId
                       join role in repo.Query<Role>() on membership.RoleId equals role.Id
                       where user.Id == id && user.IsActive && user.IsEmailConfirmed && !user.IsAdminLocked && role.IsActive &&
                         (role.Code == "ADMIN_IT" || role.Code == "SYSTEM_MANAGER" || role.Code == "TECHNICAL_SUPPORT") select user.Id;
        if (!await repo.AnyAsync(eligible, ct)) throw Validation.Invalid("technicianId", "Kỹ thuật viên phải là tài khoản đã kích hoạt, có vai trò phù hợp và đang hoạt động.");
    }
    public async Task<PagedResponse<UserLookupDto>> TechniciansAsync(string? keyword, int page, int size, CancellationToken ct)
    {
        Require(Permissions.MaintenanceAssign); access.RequireAssign(new MaintenanceTicket(), 1); Page(page, size);
        var term = Validation.Optional(keyword, 200, "keyword")?.ToLowerInvariant();
        var users = repo.Query<User>().Where(user => user.IsActive && user.IsEmailConfirmed && !user.IsAdminLocked &&
            (from membership in repo.Query<UserRole>() join role in repo.Query<Role>() on membership.RoleId equals role.Id
             where membership.UserId == user.Id && role.IsActive &&
               (role.Code == "ADMIN_IT" || role.Code == "SYSTEM_MANAGER" || role.Code == "TECHNICAL_SUPPORT") select role.Id).Any());
        if (term != null) users = users.Where(x => x.FullName.ToLower().Contains(term));
        var count = await repo.CountAsync(users, ct);
        var rows = users.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new UserLookupDto { Id = x.Id, DisplayName = x.FullName, DepartmentId = x.DepartmentId });
        return new(await repo.ListAsync(rows, ct), page, size, count, (int)Math.Ceiling(count / (double)size));
    }
    private async Task SaveEventAsync(MaintenanceTicket ticket, MaintenanceHistory history, MaintenanceAuditState before, Asset? asset, CancellationToken ct)
    {
        repo.Add(history); await repo.SaveAsync(ct);
        MaintenanceAudit.Record(audit, actor, ticket, history, before);
        if (asset != null) audit.Record("maintenance.asset.status", asset, actor.UserId, "USER", after: new { asset.CurrentStatus });
        await repo.SaveAsync(ct);
    }

    public async Task<MaintenanceDto> AssignAsync(long id, AssignTechnicianRequest request, CancellationToken ct)
    {
        Require(Permissions.MaintenanceAssign); PositiveId(id, "maintenanceId"); PositiveId(request.TechnicianId, "technicianId");
        var version = Validation.Version(request.RowVersion); var note = Validation.Required(request.Note, 1000, "note");
        return await uow.RunAsync(async () => {
            var ticket = await LockedAsync(id, version, MaintenanceTicketAction.Read, ct);
            access.RequireAssign(ticket, request.TechnicianId);
            if (Terminal(ticket)) throw new BusinessException(409, "TICKET_ALREADY_COMPLETED", "Phiếu đã kết thúc.");
            if (ticket.AssignedToUserId == request.TechnicianId) throw new BusinessException(409, "TECHNICIAN_UNCHANGED", "Kỹ thuật viên không thay đổi.");
            await repo.LockAsync("users-identity", ct); await TechnicianAsync(request.TechnicianId, ct);
            var before = MaintenanceAuditState.Capture(ticket); var old = ticket.AssignedToUserId;
            ticket.AssignedToUserId = request.TechnicianId; ticket.UpdatedAtUtc = DateTime.UtcNow;
            var history = History(ticket, "ASSIGNED", ticket.Status, ticket.UpdatedAtUtc.Value);
            history.FromAssignedToUserId = old; history.ToAssignedToUserId = request.TechnicianId; history.Comment = note;
            await SaveEventAsync(ticket, history, before, null, ct);
            return await DetailAsync(id, ct);
        }, ct);
    }

    public async Task<MaintenanceDto> StartAsync(long id, StartMaintenanceRequest request, CancellationToken ct)
    {
        Require(Permissions.MaintenanceWork); PositiveId(id, "maintenanceId"); var version = Validation.Version(request.RowVersion);
        var note = Validation.Required(request.Note, 1000, "note"); Utc(request.StartedAtUtc, "startedAt");
        return await uow.RunAsync(async () => {
            var ticket = await LockedAsync(id, version, MaintenanceTicketAction.Work, ct);
            if (ticket.Status != "PENDING") throw new BusinessException(409, "MAINTENANCE_INVALID_TRANSITION", "Chỉ bắt đầu phiếu PENDING.");
            if (ticket.AssignedToUserId is not {} technician) throw Validation.Invalid("technicianId", "Gán kỹ thuật viên trước khi bắt đầu.");
            await repo.LockAsync("users-identity", ct); await TechnicianAsync(technician, ct);
            var started = request.StartedAtUtc ?? DateTime.UtcNow;
            if (started < ticket.OpenedAtUtc || started > DateTime.UtcNow) throw Validation.Invalid("startedAt", "Thời điểm bắt đầu phải từ lúc mở phiếu đến hiện tại.");
            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(x => x.Id == ticket.AssetId), ct) ?? throw NotFound("Không tìm thấy tài sản.");
            if (asset.IsArchived || asset.CurrentStatus is not ("IN_STOCK" or "IN_USE" or "BROKEN") ||
                await repo.AnyAsync(repo.Query<MaintenanceTicket>().Where(x => x.AssetId == asset.Id && x.Status == "IN_PROGRESS" && !x.IsArchived), ct))
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Tài sản không thể bắt đầu bảo trì hoặc đã có phiếu đang xử lý.");
            var assigned = await repo.AnyAsync(repo.Query<AssetAssignment>().Where(x => x.AssetId == asset.Id && x.ReturnedAtUtc == null && !x.IsArchived), ct);
            if (asset.CurrentStatus == "IN_USE" && !assigned || asset.CurrentStatus == "IN_STOCK" && assigned)
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Trạng thái tài sản và cấp phát không nhất quán.");
            var before = MaintenanceAuditState.Capture(ticket); ticket.Status = "IN_PROGRESS"; ticket.StartedAtUtc = started; ticket.UpdatedAtUtc = DateTime.UtcNow;
            repo.Add(AssetStatusTransitions.Apply(asset, "MAINTENANCE", AssetStatusChangeSource.Maintenance, actor, started, note));
            var history = History(ticket, "STATUS_CHANGED", "PENDING", started); history.Comment = note;
            await SaveEventAsync(ticket, history, before, asset, ct);
            return await DetailAsync(id, ct);
        }, ct);
    }

    public Task<MaintenanceDto> FailAsync(long id, FailMaintenanceRequest request, CancellationToken ct)
    {
        var complete = new CompleteMaintenanceRequest { RowVersion = request.RowVersion,
            Resolution = request.Reason ?? "", CompletedAtUtc = request.CompletedAtUtc };
        if (request.HasCost) complete.ActualCost = request.Cost;
        return FinishAsync(id, complete, false, ct);
    }

    public async Task<MaintenanceDto> CancelAsync(long id, CancelMaintenanceRequest request, CancellationToken ct)
    {
        Require(Permissions.MaintenanceCancel); PositiveId(id, "maintenanceId"); var version = Validation.Version(request.RowVersion);
        var reason = Validation.Required(request.Reason, 1000, "reason");
        return await uow.RunAsync(async () => {
            var ticket = await LockedAsync(id, version, MaintenanceTicketAction.Cancel, ct);
            if (Terminal(ticket)) throw new BusinessException(409, "TICKET_ALREADY_COMPLETED", "Phiếu đã kết thúc.");
            var now = DateTime.UtcNow; var before = MaintenanceAuditState.Capture(ticket); Asset? asset = null;
            if (ticket.Status == "IN_PROGRESS") {
                if (ticket.StartedAtUtc > now) throw new BusinessException(409, "MAINTENANCE_TIME_CONFLICT", "Thời điểm xử lý không hợp lệ.");
                asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(x => x.Id == ticket.AssetId), ct) ?? throw NotFound();
                if (asset.IsArchived || asset.CurrentStatus != "MAINTENANCE") throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Tài sản không khớp phiếu.");
                var start = await repo.FirstAsync(repo.Query<AssetStatusHistory>().Where(x => x.AssetId == ticket.AssetId &&
                    x.Source == "MAINTENANCE" && x.ToStatus == "MAINTENANCE" && x.ChangedAtUtc == ticket.StartedAtUtc).OrderByDescending(x => x.Id), ct);
                if (start?.FromStatus is not ("IN_STOCK" or "IN_USE" or "BROKEN")) throw new BusinessException(409, "MAINTENANCE_HISTORY_MISSING", "Chưa đủ lịch sử để khôi phục trạng thái an toàn.");
                var assigned = await repo.AnyAsync(repo.Query<AssetAssignment>().Where(x => x.AssetId == asset.Id && x.ReturnedAtUtc == null && !x.IsArchived), ct);
                var restored = start.FromStatus == "BROKEN" ? "BROKEN" : AssetStatusTransitions.AfterMaintenance(true, assigned);
                repo.Add(AssetStatusTransitions.Apply(asset, restored, AssetStatusChangeSource.Maintenance, actor, now, reason));
            }
            ticket.Status = "CANCELLED"; ticket.Resolution = reason; ticket.ResolvedAtUtc = now; ticket.UpdatedAtUtc = now;
            var history = History(ticket, "CANCELLED", before.Status, now); history.Comment = reason;
            await SaveEventAsync(ticket, history, before, asset, ct);
            return await DetailAsync(id, ct);
        }, ct);
    }

    public async Task<MaintenanceDto> UpdateAsync(long id, UpdateMaintenanceRequest request, CancellationToken ct)
    {
        Require(Permissions.MaintenanceUpdate); access.RequireCostWrite(request.HasEstimatedCost || request.HasActualCost);
        PositiveId(id, "maintenanceId"); var version = Validation.Version(request.RowVersion);
        var note = Validation.Required(request.Note, 1000, "note"); Money(request.EstimatedCost, "estimatedCost"); Money(request.ActualCost, "actualCost"); Utc(request.DueAtUtc, "dueAt");
        var title = request.Title == null ? null : Validation.Required(request.Title, 200, "title");
        var description = request.Description == null ? null : Validation.Required(request.Description, 2000, "description");
        if (request.Priority != null) Priority(request.Priority);
        if (title == null && description == null && request.Priority == null && !request.HasDueAt && !request.HasEstimatedCost && !request.HasActualCost) throw Validation.Invalid("body", "Chọn thông tin cần cập nhật.");
        return await uow.RunAsync(async () => {
            var ticket = await LockedAsync(id, version, MaintenanceTicketAction.Update, ct);
            if (Terminal(ticket) && (title != null || description != null || request.Priority != null || request.HasDueAt || request.HasEstimatedCost || !request.HasActualCost))
                throw new BusinessException(409, "TICKET_ALREADY_COMPLETED", "Phiếu kết thúc chỉ cho phép hiệu chỉnh chi phí thực tế kèm lý do.");
            if (request.HasDueAt && request.DueAtUtc < ticket.OpenedAtUtc) throw Validation.Invalid("dueAt", "Hạn xử lý không được trước lúc mở phiếu.");
            var before = MaintenanceAuditState.Capture(ticket);
            if (title != null) ticket.Title = title;
            if (description != null) ticket.Description = description;
            if (request.Priority != null) ticket.Priority = request.Priority;
            if (request.HasDueAt) ticket.DueAtUtc = request.DueAtUtc;
            if (request.HasEstimatedCost) ticket.EstimatedCost = request.EstimatedCost;
            if (request.HasActualCost) ticket.ActualCost = request.ActualCost;
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            // Each financial correction and metadata update remains a new append-only event.
            var history = History(ticket, request.HasActualCost ? "COST_UPDATED" : "COMMENTED", before.Status, ticket.UpdatedAtUtc.Value);
            history.Comment = note; if (request.HasActualCost) history.Cost = ticket.ActualCost;
            await SaveEventAsync(ticket, history, before, null, ct);
            return await DetailAsync(id, ct);
        }, ct);
    }

    public async Task<PagedResponse<MaintenanceHistoryDto>> HistoryAsync(long id, int page, int size, DateTime? from, DateTime? to, string? eventType, CancellationToken ct)
    {
        Require(Permissions.MaintenanceRead); PositiveId(id, "maintenanceId"); Page(page, size); Utc(from, "from"); Utc(to, "to");
        if (from > to) throw Validation.Invalid("to", "Khoảng thời gian không hợp lệ.");
        if (eventType is not (null or "CREATED" or "ASSIGNED" or "STATUS_CHANGED" or "COMMENTED" or "COST_UPDATED" or "RESOLVED" or "FAILED" or "CANCELLED")) throw Validation.Invalid("eventType", "Loại sự kiện không hợp lệ.");
        await DetailAsync(id, ct); // Same object scope as ticket detail; no history-based scope bypass.
        var query = repo.Query<MaintenanceHistory>().Where(x => x.MaintenanceTicketId == id);
        if (from.HasValue) query = query.Where(x => x.PerformedAtUtc >= from.Value);
        if (to.HasValue) query = query.Where(x => x.PerformedAtUtc <= to.Value);
        if (eventType != null) query = query.Where(x => x.EventType == eventType);
        var count = await repo.CountAsync(query, ct); var cost = access.CanReadCost;
        var rows = query.OrderByDescending(x => x.PerformedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new MaintenanceHistoryDto(x.Id, x.EventType, x.FromStatus, x.ToStatus, x.FromAssignedToUserId,
                x.ToAssignedToUserId, x.Comment, cost ? x.Cost : null, x.PerformedByUserId, x.PerformedAtUtc, x.CorrelationId));
        return new(await repo.ListAsync(rows, ct), page, size, count, (int)Math.Ceiling(count / (double)size));
    }
}
