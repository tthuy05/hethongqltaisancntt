using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed partial class MaintenanceService(IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit,
    MaintenanceAccessPolicy access, IMaintenanceTicketCodeGenerator? ticketCodes = null)
{
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || actor.UserId != access.UserId || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền thực hiện thao tác bảo trì.");
    }

    private static void PositiveId(long id, string field)
    {
        if (id <= 0) throw Validation.Invalid(field, "ID phải > 0.");
    }

    private static BusinessException NotFound(string message = "Không tìm thấy phiếu bảo trì.") =>
        new(404, "MAINTENANCE_NOT_FOUND", message);

    private IQueryable<MaintenanceDto> Query()
    {
        return Project(access.ScopeRead(repo.Query<MaintenanceTicket>()).Where(x => !x.IsArchived));
    }

    private IQueryable<MaintenanceDto> Project(IQueryable<MaintenanceTicket> tickets)
    {
        var canReadCost = access.CanReadCost;
        return tickets.Select(x => new MaintenanceDto
        {
            Id = x.Id,
            TicketCode = x.TicketCode,
            AssetId = x.AssetId,
            AssetCode = repo.Query<Asset>().Where(a => a.Id == x.AssetId).Select(a => a.AssetCode).FirstOrDefault(),
            AssetName = repo.Query<Asset>().Where(a => a.Id == x.AssetId).Select(a => a.Name).FirstOrDefault(),
            AssignedToName = repo.Query<User>().Where(u => u.Id == x.AssignedToUserId).Select(u => u.FullName).FirstOrDefault(),
            Title = x.Title, Priority = x.Priority, RequestedByUserId = x.RequestedByUserId,
            AssignedToUserId = x.AssignedToUserId, DueAtUtc = x.DueAtUtc,
            Description = x.Description,
            OpenedAtUtc = x.OpenedAtUtc,
            StartedAtUtc = x.StartedAtUtc,
            CompletedAtUtc = x.ResolvedAtUtc,
            Resolution = x.Resolution,
            EstimatedCost = canReadCost ? x.EstimatedCost : null,
            ActualCost = canReadCost ? x.ActualCost : null,
            Status = x.Status,
            RowVersion = Convert.ToBase64String(x.RowVersion)
        });
    }

    public async Task<PagedResponse<MaintenanceDto>> ListAsync(long? assetId, int page, int pageSize, CancellationToken ct)
    {
        Require(Permissions.MaintenanceRead);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        if (assetId <= 0)
            throw Validation.Invalid("assetId", "ID phải > 0 hoặc null.");

        var query = Query();
        if (assetId is { } a) query = query.Where(x => x.AssetId == a);

        var total = await repo.CountAsync(query, ct);
        var order = query.OrderByDescending(x => x.OpenedAtUtc).ThenByDescending(x => x.Id);
        var items = await repo.ListAsync(order.Skip((page - 1) * pageSize).Take(pageSize), ct);
        return new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<MaintenanceDto> GetAsync(long id, CancellationToken ct)
    {
        Require(Permissions.MaintenanceRead);
        PositiveId(id, "maintenanceId");
        return await DetailAsync(id, ct);
    }

    private async Task<MaintenanceDto> DetailAsync(long id, CancellationToken ct) =>
        await repo.FirstAsync(Query().Where(x => x.Id == id), ct) ?? throw NotFound();

    public async Task<MaintenanceDto> CreateAsync(CreateMaintenanceRequest request, CancellationToken ct)
    {
        Require(Permissions.MaintenanceCreate);
        access.RequireCreate();
        access.RequireCostWrite(request.HasEstimatedCost);
        PositiveId(request.AssetId, "assetId");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw Validation.Invalid("description", "Mô tả sự cố không được để trống.");
        if (request.Description.Length > 2000)
            throw Validation.Invalid("description", "Mô tả không được vượt quá 2000 ký tự.");
        Money(request.EstimatedCost, "estimatedCost");

        return await uow.RunAsync(async () =>
        {
            // 1. Khóa tài sản tránh tranh chấp đồng thời
            await repo.LockAssetAsync(request.AssetId, ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.MaintenanceRead, Permissions.MaintenanceCreate);

            // 2. Kiểm tra tài sản & trạng thái hợp lệ
            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == request.AssetId), ct)
                ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");

            if (asset.IsArchived)
                throw new BusinessException(409, "ASSET_ARCHIVED", "Không thể tạo phiếu bảo trì cho tài sản đã lưu trữ.");

            if (asset.CurrentStatus == "MAINTENANCE")
                throw new BusinessException(409, "ASSET_ALREADY_IN_MAINTENANCE", "Tài sản đang trong quá trình bảo trì.");

            if (asset.CurrentStatus is not ("IN_STOCK" or "IN_USE" or "BROKEN"))
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Không thể mở bảo trì cho tài sản đã ngừng sử dụng.");
            if (await repo.AnyAsync(repo.Query<MaintenanceTicket>().Where(x => x.AssetId == asset.Id && !x.IsArchived &&
                    (x.Status == "PENDING" || x.Status == "IN_PROGRESS")), ct))
                throw new BusinessException(409, "ASSET_ACTIVE_WORKFLOW", "Tài sản đã có phiếu bảo trì chưa kết thúc.");

            var now = DateTime.UtcNow;
            var ticketCode = await (ticketCodes ?? throw new InvalidOperationException("Maintenance code generator required.")).NextAsync(ct);
            var title = request.Title == null ? (request.Description.Length > 50 ? request.Description[..50] : request.Description) : Validation.Required(request.Title, 200, "title");
            Priority(request.Priority); Utc(request.DueAtUtc, "dueAt");
            if (request.DueAtUtc < now) throw Validation.Invalid("dueAt", "Hạn xử lý không được trước thời điểm mở phiếu.");

            // 3. Khởi tạo phiếu bảo trì mới
            var ticket = new MaintenanceTicket
            {
                TicketCode = ticketCode,
                AssetId = request.AssetId,
                Title = title,
                Description = request.Description.Trim(),
                Priority = request.Priority, DueAtUtc = request.DueAtUtc,
                Status = "PENDING",
                RequestedByUserId = actor.UserId!.Value,
                OpenedAtUtc = now,
                EstimatedCost = request.EstimatedCost,
                CreatedAtUtc = now,
                IsArchived = false
            };

            repo.Add(ticket);

            // Opening a pending ticket does not start work or change the asset.
            await repo.SaveAsync(ct);
            var history = History(ticket, "CREATED", null, now);
            repo.Add(history);
            await repo.SaveAsync(ct);
            MaintenanceAudit.Record(audit, actor, ticket, history, null);
            await repo.SaveAsync(ct);

            // Return only the ticket just created; later reads remain scoped.
            return await repo.FirstAsync(Project(repo.Query<MaintenanceTicket>().Where(t => t.Id == ticket.Id)), ct)
                ?? throw NotFound();
        }, ct);
    }

    public Task<MaintenanceDto> CompleteAsync(long id, CompleteMaintenanceRequest request, CancellationToken ct) => FinishAsync(id, request, true, ct);

    private async Task<MaintenanceDto> FinishAsync(long id, CompleteMaintenanceRequest request, bool usable, CancellationToken ct)
    {
        Require(Permissions.MaintenanceComplete);
        access.RequireCostWrite(request.HasActualCost);
        PositiveId(id, "maintenanceId");

        if (string.IsNullOrWhiteSpace(request.Resolution))
            throw Validation.Invalid("resolution", "Nội dung xử lý/nghiệm thu không được để trống.");
        if (request.Resolution.Length > 2000)
            throw Validation.Invalid("resolution", "Nội dung xử lý không được vượt quá 2000 ký tự.");
        Money(request.ActualCost, "actualCost");
        Utc(request.CompletedAtUtc, "completedAt");

        // Kiểm tra và giải mã RowVersion
        var version = Validation.Version(request.RowVersion);

        return await uow.RunAsync(async () =>
        {
            var snapshot = await repo.FirstAsync(access.ScopeRead(repo.Query<MaintenanceTicket>()).Where(t => t.Id == id && !t.IsArchived), ct)
                ?? throw NotFound();

            // Same lock order as Assignment/start/return: asset first, then reload
            // the tracked ticket so a waiter cannot validate a stale pre-lock row.
            await repo.LockAssetAsync(snapshot.AssetId, ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, Permissions.MaintenanceRead, Permissions.MaintenanceResolve);
            var ticket = await repo.FirstAsync(repo.Query<MaintenanceTicket>(true).Where(t => t.Id == id && !t.IsArchived), ct)
                ?? throw NotFound();
            access.Require(ticket, MaintenanceTicketAction.Resolve);

            // So sánh RowVersion để tránh Concurrency Conflict
            if (!ticket.RowVersion.SequenceEqual(version))
                throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi; tải lại trước khi lưu.");

            if (ticket.ResolvedAtUtc != null || ticket.Status is "RESOLVED" or "FAILED" or "CANCELLED")
                throw new BusinessException(409, "TICKET_ALREADY_COMPLETED", "Phiếu bảo trì này đã được nghiệm thu trước đó.");
            if (ticket.Status != "IN_PROGRESS" || ticket.AssignedToUserId is not > 0 || ticket.StartedAtUtc == null)
                throw new BusinessException(409, "MAINTENANCE_NOT_STARTED", "Phiếu phải được gán và bắt đầu trước khi nghiệm thu.");

            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == ticket.AssetId), ct)
                ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản liên kết với phiếu bảo trì.");
            if (asset.IsArchived || asset.CurrentStatus != "MAINTENANCE")
                throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Trạng thái tài sản không khớp phiếu đang bảo trì.");

            var now = request.CompletedAtUtc ?? DateTime.UtcNow;
            if (ticket.StartedAtUtc > now || now > DateTime.UtcNow)
                throw new BusinessException(409, "MAINTENANCE_TIME_CONFLICT", "Thời điểm bắt đầu nằm trong tương lai.");
            var before = MaintenanceAuditState.Capture(ticket);
            var hasAssignment = await repo.AnyAsync(repo.Query<AssetAssignment>().Where(a =>
                a.AssetId == asset.Id && a.ReturnedAtUtc == null && !a.IsArchived), ct);
            repo.ExpectVersion(ticket, version);

            // 1. Cập nhật phiếu bảo trì
            ticket.Status = usable ? "RESOLVED" : "FAILED";
            ticket.Resolution = request.Resolution.Trim();
            if (request.HasActualCost) ticket.ActualCost = request.ActualCost;
            ticket.ResolvedAtUtc = now;
            ticket.UpdatedAtUtc = now;

            // Reuse the shared return/maintenance protocol; do not erase an active assignment.
            repo.Add(AssetStatusTransitions.Apply(asset, AssetStatusTransitions.AfterMaintenance(usable, hasAssignment),
                AssetStatusChangeSource.Maintenance, actor, now, "Hoàn tất bảo trì"));
            asset.UpdatedAtUtc = now;
            var history = History(ticket, usable ? "RESOLVED" : "FAILED", "IN_PROGRESS", now);
            history.Comment = request.Resolution.Trim();
            history.Cost = ticket.ActualCost;
            repo.Add(history);
            await repo.SaveAsync(ct);
            MaintenanceAudit.Record(audit, actor, ticket, history, before);
            audit.Record("maintenance.asset.status", asset, actor.UserId, "USER", after: new { asset.CurrentStatus });
            await repo.SaveAsync(ct);

            return await DetailAsync(id, ct);
        }, ct);
    }

    private MaintenanceHistory History(MaintenanceTicket ticket, string eventType, string? fromStatus, DateTime now) => new()
    {
        MaintenanceTicketId = ticket.Id, EventType = eventType, FromStatus = fromStatus, ToStatus = ticket.Status,
        PerformedByUserId = actor.UserId, PerformedAtUtc = now, CorrelationId = actor.CorrelationId
    };

    private static void Money(decimal? value, string field)
    {
        if (value is < 0 or > 9999999999999999.99m || value.HasValue && decimal.Round(value.Value, 2) != value.Value)
            throw Validation.Invalid(field, "Chi phí numeric(18,2) không âm; tối đa 2 chữ số thập phân.");
    }
}
