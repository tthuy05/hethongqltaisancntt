namespace ItAssetManagement.Application.Mvp;

public sealed class MaintenanceService(IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit)
{
    public Task<PagedResponse<MaintenanceDto>> ListAsync(long? assetId, int page, int pageSize, CancellationToken ct)
    {
        // TODO: Triển khai logic lấy danh sách ticket bảo trì (phân trang, lọc theo asset)
        return Task.FromResult(new PagedResponse<MaintenanceDto>([], page, pageSize, 0, 0));
    }

    public Task<MaintenanceDto> GetAsync(long id, CancellationToken ct)
    {
        // TODO: Query lấy chi tiết ticket bảo trì
        return Task.FromResult(new MaintenanceDto());
    }

    public async Task<MaintenanceDto> CreateAsync(CreateMaintenanceRequest request, CancellationToken ct)
    {
        return await uow.RunAsync(() =>
        {
            // TODO: Validate, lock tài sản (LockAssetAsync), tạo ticket mới, ghi audit
            return Task.FromResult(new MaintenanceDto());
        }, ct);
    }

    public async Task<MaintenanceDto> CompleteAsync(long id, CompleteMaintenanceRequest request, CancellationToken ct)
    {
        return await uow.RunAsync(() =>
        {
            // TODO: Lấy ticket, validate RowVersion (ExpectVersion), cập nhật chi phí, đóng ticket, ghi audit
            return Task.FromResult(new MaintenanceDto());
        }, ct);
    }
}