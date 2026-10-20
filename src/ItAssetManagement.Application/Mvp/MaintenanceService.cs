using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class MaintenanceService(IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit)
{
    private void Require(string permission)
    {
        if (actor.UserId is not > 0 || !actor.Has(permission))
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
        return repo.Query<MaintenanceTicket>().Where(x => !x.IsArchived).Select(x => new MaintenanceDto
        {
            Id = x.Id,
            AssetId = x.AssetId,
            Description = x.Description,
            StartedAtUtc = x.StartedAtUtc ?? x.OpenedAtUtc,
            CompletedAtUtc = x.ResolvedAtUtc,
            Resolution = x.Resolution,
            EstimatedCost = x.EstimatedCost,
            ActualCost = x.ActualCost,
            Status = x.Status,
            RowVersion = Convert.ToBase64String(x.RowVersion)
        });
    }

    public async Task<PagedResponse<MaintenanceDto>> ListAsync(long? assetId, int page, int pageSize, CancellationToken ct)
    {
        Require(Permissions.MaintenanceRead);
        if (page < 1 || pageSize is < 1 or > 100)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        if (assetId <= 0)
            throw Validation.Invalid("assetId", "ID phải > 0 hoặc null.");

        var query = Query();
        if (assetId is { } a) query = query.Where(x => x.AssetId == a);

        var total = await repo.CountAsync(query, ct);
        var order = query.OrderByDescending(x => x.StartedAtUtc).ThenByDescending(x => x.Id);
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
        PositiveId(request.AssetId, "assetId");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw Validation.Invalid("description", "Mô tả sự cố không được để trống.");
        if (request.Description.Length > 2000)
            throw Validation.Invalid("description", "Mô tả không được vượt quá 2000 ký tự.");
        if (request.EstimatedCost is < 0)
            throw Validation.Invalid("estimatedCost", "Chi phí dự kiến không được âm.");

        var createdId = await uow.RunAsync(async () =>
        {
            // 1. Khóa tài sản tránh tranh chấp đồng thời
            await repo.LockAssetAsync(request.AssetId, ct);

            // 2. Kiểm tra tài sản & trạng thái hợp lệ
            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == request.AssetId), ct)
                ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");

            if (asset.IsArchived)
                throw new BusinessException(409, "ASSET_ARCHIVED", "Không thể tạo phiếu bảo trì cho tài sản đã lưu trữ.");

            if (asset.CurrentStatus == "MAINTENANCE")
                throw new BusinessException(409, "ASSET_ALREADY_IN_MAINTENANCE", "Tài sản đang trong quá trình bảo trì.");

            if (asset.CurrentStatus == "IN_USE")
                throw new BusinessException(409, "ASSET_IN_USE", "Tài sản đang được cấp phát sử dụng. Cần thu hồi tài sản trước khi đưa vào bảo trì.");

            if (asset.CurrentStatus != "IN_STOCK")
                throw new BusinessException(409, "ASSET_NOT_IN_STOCK", "Chỉ tài sản sẵn sàng trong kho (IN_STOCK) mới có thể đưa vào bảo trì.");

            var now = DateTime.UtcNow;
            var ticketCode = $"MT-{request.AssetId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var title = request.Description.Length > 50 ? request.Description[..50] : request.Description;

            // 3. Khởi tạo phiếu bảo trì mới
            var ticket = new MaintenanceTicket
            {
                TicketCode = ticketCode,
                AssetId = request.AssetId,
                Title = title,
                Description = request.Description.Trim(),
                Priority = "MEDIUM",
                Status = "IN_PROGRESS",
                RequestedByUserId = actor.UserId ?? 1,
                OpenedAtUtc = now,
                StartedAtUtc = now,
                EstimatedCost = request.EstimatedCost,
                CreatedAtUtc = now,
                IsArchived = false
            };

            repo.Add(ticket);

            // 4. Chuyển trạng thái tài sản sang MAINTENANCE
            asset.CurrentStatus = "MAINTENANCE";
            asset.UpdatedAtUtc = now;

            await repo.SaveAsync(ct);

            // 5. Ghi vết kiểm toán (Audit)
            audit.Record("MAINTENANCE_CREATE", ticket, actor.UserId, "USER", "SUCCESS", after: new { ticket.Id, ticket.AssetId, ticket.Status });

            return ticket.Id;
        }, ct);

        return await DetailAsync(createdId, ct);
    }

    public async Task<MaintenanceDto> CompleteAsync(long id, CompleteMaintenanceRequest request, CancellationToken ct)
    {
        Require(Permissions.MaintenanceComplete);
        PositiveId(id, "maintenanceId");

        if (string.IsNullOrWhiteSpace(request.Resolution))
            throw Validation.Invalid("resolution", "Nội dung xử lý/nghiệm thu không được để trống.");
        if (request.Resolution.Length > 2000)
            throw Validation.Invalid("resolution", "Nội dung xử lý không được vượt quá 2000 ký tự.");
        if (request.ActualCost is < 0)
            throw Validation.Invalid("actualCost", "Chi phí thực tế không được âm.");

        // Kiểm tra và giải mã RowVersion
        var version = Validation.Version(request.RowVersion);

        await uow.RunAsync(async () =>
        {
            var ticket = await repo.FirstAsync(repo.Query<MaintenanceTicket>(true).Where(t => t.Id == id && !t.IsArchived), ct)
                ?? throw NotFound();

            // So sánh RowVersion để tránh Concurrency Conflict
            if (!ticket.RowVersion.SequenceEqual(version))
                throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi; tải lại trước khi lưu.");

            if (ticket.ResolvedAtUtc != null || ticket.Status == "COMPLETED")
                throw new BusinessException(409, "TICKET_ALREADY_COMPLETED", "Phiếu bảo trì này đã được nghiệm thu trước đó.");

            await repo.LockAssetAsync(ticket.AssetId, ct);

            var asset = await repo.FirstAsync(repo.Query<Asset>(true).Where(a => a.Id == ticket.AssetId), ct)
                ?? throw new BusinessException(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản liên kết với phiếu bảo trì.");

            var now = DateTime.UtcNow;

            // 1. Cập nhật phiếu bảo trì
            ticket.Status = "COMPLETED";
            ticket.Resolution = request.Resolution.Trim();
            ticket.ActualCost = request.ActualCost;
            ticket.ResolvedAtUtc = now;
            ticket.UpdatedAtUtc = now;

            // 2. Đưa tài sản quay về trạng thái sẵn sàng trong kho
            asset.CurrentStatus = "IN_STOCK";
            asset.UpdatedAtUtc = now;

            await repo.SaveAsync(ct);

            // 3. Ghi audit log
            audit.Record("MAINTENANCE_COMPLETE", ticket, actor.UserId, "USER", "SUCCESS", after: new { ticket.Id, ticket.AssetId, ticket.ActualCost, ticket.Status });

            return ticket.Id;
        }, ct);

        return await DetailAsync(id, ct);
    }
}