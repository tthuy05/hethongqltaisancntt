using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class AssetService(IRepository repo, IUnitOfWork uow, IAuditWriter audit, IActor actor)
{
    private IQueryable<AssetDto> Project(IQueryable<Asset> source)
    {
        var canRead = actor.Has(Permissions.AssetCost);
        return from a in source join t in repo.Query<AssetType>() on a.AssetTypeId equals t.Id
               join d in repo.Query<Department>() on a.OwningDepartmentId equals d.Id
               select new AssetDto
               {
                   Id = a.Id, AssetCode = a.AssetCode, Name = a.Name, SerialNumber = a.SerialNumber,
                   AssetTypeId = a.AssetTypeId, OwningDepartmentId = a.OwningDepartmentId,
                   AssetType = new(t.Id, t.Code, t.Name, t.IsActive), OwningDepartment = new(d.Id, d.Code, d.Name, d.IsActive),
                   Status = a.CurrentStatus, Location = a.Location, CanReadCost = canRead,
                   PurchasePrice = canRead ? a.PurchaseCost : null,
                   Brand = a.Manufacturer, Model = a.Model, Specification = a.Specification,
                   OperatingSystem = a.OperatingSystem, Note = a.Notes, PurchaseDate = a.PurchaseDate,
                   WarrantyExpirationDate = a.WarrantyEndDate, CreatedAtUtc = a.CreatedAtUtc, UpdatedAtUtc = a.UpdatedAtUtc,
                   RowVersion = Convert.ToBase64String(a.RowVersion), IsArchived = a.IsArchived
               };
    }
    public async Task<PagedResponse<AssetDto>> ListAsync(ListQuery q, CancellationToken ct)
    {
        Validation.List(q, true);
        var query = repo.Query<Asset>().Where(x => !x.IsArchived);
        if (q.Keyword != null) query = repo.SearchAssets(query, q.Keyword);
        if (q.AssetTypeId != null) query = query.Where(x => x.AssetTypeId == q.AssetTypeId);
        if (q.DepartmentId != null) query = query.Where(x => x.OwningDepartmentId == q.DepartmentId);
        if (q.Status != null) { var status = StatusMap.Api[q.Status]; query = query.Where(x => x.CurrentStatus == status); }
        var total = await repo.CountAsync(query, ct);
        var asc = q.SortDirection == "asc";
        IOrderedQueryable<Asset> ordered = (q.SortBy, asc) switch
        {
            ("name", true) => query.OrderBy(x => x.Name), ("name", false) => query.OrderByDescending(x => x.Name),
            ("status", true) => query.OrderBy(x => x.CurrentStatus), ("status", false) => query.OrderByDescending(x => x.CurrentStatus),
            ("createdAt", true) => query.OrderBy(x => x.CreatedAtUtc), ("createdAt", false) => query.OrderByDescending(x => x.CreatedAtUtc),
            ("updatedAt", true) => query.OrderBy(x => x.UpdatedAtUtc), ("updatedAt", false) => query.OrderByDescending(x => x.UpdatedAtUtc),
            (_, true) => query.OrderBy(x => x.AssetCode), _ => query.OrderByDescending(x => x.AssetCode)
        };
        ordered = asc ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);
        var items = await repo.ListAsync(Project(ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)), ct);
        foreach (var item in items) item.Status = StatusMap.ToApi(item.Status);
        return new(items, q.Page, q.PageSize, total, (int)Math.Ceiling(total / (double)q.PageSize));
    }
    public async Task<AssetDto> GetAsync(long id, CancellationToken ct)
    {
        var result = await repo.FirstAsync(Project(repo.Query<Asset>().Where(x => x.Id == id && !x.IsArchived)), ct)
                     ?? throw NotFound();
        result.Status = StatusMap.ToApi(result.Status); return result;
    }
    public async Task<AssetDto> CreateAsync(AssetRequest x, CancellationToken ct)
    {
        Validation.Asset(x, false);
        var id = await uow.RunAsync(async () =>
        {
            await ReferencesAsync(x, null, ct); await UniqueAsync(x, 0, ct);
            var asset = new Asset { CreatedByUserId = actor.UserId };
            Apply(asset, x); repo.Add(asset); await repo.SaveAsync(ct);
            repo.Add(new AssetStatusHistory { AssetId = asset.Id, ToStatus = "IN_STOCK", Source = "SYSTEM",
                Reason = "Tạo tài sản", ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId, ChangedAtUtc = DateTime.UtcNow });
            Record("assets.create", asset, null); await repo.SaveAsync(ct); return asset.Id;
        }, ct);
        return await GetAsync(id, ct);
    }
    public async Task<AssetDto> UpdateAsync(long id, AssetRequest x, CancellationToken ct)
    {
        Validation.Asset(x, true);
        await uow.RunAsync(async () =>
        {
            await repo.LockAssetAsync(id, ct);
            var asset = await TrackedAsync(id, ct); var before = Snapshot(asset);
            repo.ExpectVersion(asset, Validation.Version(x.RowVersion));
            if (!actor.Has(Permissions.AssetCost) && x.PurchasePrice != asset.PurchaseCost)
                throw new BusinessException(403, "FORBIDDEN", "Không có quyền sửa chi phí.");
            await ReferencesAsync(x, asset, ct); await UniqueAsync(x, id, ct); Apply(asset, x);
            asset.UpdatedByUserId = actor.UserId; Record("assets.update", asset, before); await repo.SaveAsync(ct); return true;
        }, ct);
        return await GetAsync(id, ct);
    }
    public async Task ArchiveAsync(long id, byte[] version, CancellationToken ct)
    {
        await uow.RunAsync(async () =>
        {
            await repo.LockAssetAsync(id, ct); var asset = await TrackedAsync(id, ct); var before = Snapshot(asset);
            repo.ExpectVersion(asset, version);
            if (await repo.HasActiveWorkflowAsync(id, ct)) throw Workflow();
            asset.IsArchived = true; asset.ArchivedAtUtc = DateTime.UtcNow; asset.UpdatedByUserId = actor.UserId;
            Record("assets.archive", asset, before); await repo.SaveAsync(ct); return true;
        }, ct);
    }
    public async Task<AssetDto> StatusAsync(long id, StatusRequest x, CancellationToken ct)
    {
        var reason = Validation.Required(x.Reason, 1000, "reason"); var version = Validation.Version(x.RowVersion);
        if (x.Status != "Retired") throw Validation.Invalid("status", "M1 chỉ cho phép InStock/Broken → Retired; các workflow khác PLANNED.");
        await uow.RunAsync(async () =>
        {
            await repo.LockAssetAsync(id, ct); var asset = await TrackedAsync(id, ct); var before = Snapshot(asset);
            repo.ExpectVersion(asset, version);
            if (asset.CurrentStatus is not ("IN_STOCK" or "BROKEN")) throw new BusinessException(409, "ASSET_STATUS_CONFLICT", "Chuyển trạng thái không hợp lệ.");
            if (await repo.HasActiveWorkflowAsync(id, ct)) throw Workflow();
            repo.Add(new AssetStatusHistory { AssetId = id, FromStatus = asset.CurrentStatus, ToStatus = "RETIRED", Reason = reason,
                Source = "ADMIN", ChangedAtUtc = DateTime.UtcNow, ChangedByUserId = actor.UserId, CorrelationId = actor.CorrelationId });
            asset.CurrentStatus = "RETIRED"; asset.UpdatedByUserId = actor.UserId;
            Record("assets.status.change", asset, before); await repo.SaveAsync(ct); return true;
        }, ct);
        return await GetAsync(id, ct);
    }
    public async Task<PagedResponse<HistoryDto>> HistoryAsync(long id, ListQuery q, CancellationToken ct)
    {
        await GetAsync(id, ct); Validation.List(q, true);
        var query = repo.Query<AssetStatusHistory>().Where(x => x.AssetId == id); var total = await repo.CountAsync(query, ct);
        var items = await repo.ListAsync(query.OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new HistoryDto(x.Id, x.FromStatus, x.ToStatus, x.Reason, x.Source, x.ChangedAtUtc, x.ChangedByUserId, x.CorrelationId)), ct);
        return new(items.Select(x => x with { FromStatus = x.FromStatus == null ? null : StatusMap.ToApi(x.FromStatus), ToStatus = StatusMap.ToApi(x.ToStatus) }).ToList(),
            q.Page, q.PageSize, total, (int)Math.Ceiling(total / (double)q.PageSize));
    }
    private Task<Asset?> FindTrackedAsync(long id, CancellationToken ct) => repo.FirstAsync(repo.Query<Asset>(true).Where(x => x.Id == id && !x.IsArchived), ct);
    private async Task<Asset> TrackedAsync(long id, CancellationToken ct) => await FindTrackedAsync(id, ct) ?? throw NotFound();
    private async Task ReferencesAsync(AssetRequest x, Asset? old, CancellationToken ct)
    {
        await repo.LockAsync("master-data", ct);
        if (!await repo.AnyAsync(repo.Query<AssetType>().Where(t => t.Id == x.AssetTypeId && (t.IsActive || old != null && t.Id == old.AssetTypeId)), ct))
            throw Validation.Invalid("assetTypeId", "Loại tài sản không tồn tại/không hoạt động.");
        if (!await repo.AnyAsync(repo.Query<Department>().Where(d => d.Id == x.OwningDepartmentId && (d.IsActive || old != null && d.Id == old.OwningDepartmentId)), ct))
            throw Validation.Invalid("owningDepartmentId", "Phòng ban không tồn tại/không hoạt động.");
    }
    private async Task UniqueAsync(AssetRequest x, long id, CancellationToken ct)
    {
        var code = x.AssetCode.ToLowerInvariant(); var serial = x.SerialNumber?.ToLowerInvariant();
        if (await repo.AnyAsync(repo.Query<Asset>().Where(a => a.Id != id && a.AssetCode.ToLower() == code), ct))
            throw new BusinessException(409, "ASSET_CODE_CONFLICT", "Mã tài sản đã tồn tại.");
        if (serial != null && await repo.AnyAsync(repo.Query<Asset>().Where(a => a.Id != id && a.SerialNumber != null && a.SerialNumber.ToLower() == serial), ct))
            throw new BusinessException(409, "ASSET_SERIAL_CONFLICT", "Serial đã tồn tại.");
    }
    private static void Apply(Asset a, AssetRequest x)
    {
        a.AssetCode = x.AssetCode; a.Name = x.Name; a.AssetTypeId = x.AssetTypeId; a.OwningDepartmentId = x.OwningDepartmentId;
        a.SerialNumber = x.SerialNumber; a.Manufacturer = x.Brand; a.Model = x.Model; a.Specification = x.Specification;
        a.OperatingSystem = x.OperatingSystem; a.Location = x.Location; a.PurchaseCost = x.PurchasePrice;
        a.PurchaseDate = x.PurchaseDate; a.WarrantyEndDate = x.WarrantyExpirationDate; a.Notes = x.Note;
    }
    // Explicit safe allowlist: no credential, token or arbitrary entity serialization.
    private static object Snapshot(Asset a) => new { a.AssetCode, a.Name, a.AssetTypeId, a.OwningDepartmentId, a.CurrentStatus, a.IsArchived, a.SerialNumber, a.PurchaseCost };
    private void Record(string action, Asset a, object? before) => audit.Record(action, a, actor.UserId, "USER", before: before, after: Snapshot(a));
    private static BusinessException NotFound() => new(404, "ASSET_NOT_FOUND", "Không tìm thấy tài sản.");
    private static BusinessException Workflow() => new(409, "ASSET_ACTIVE_WORKFLOW", "Tài sản có workflow đang hoạt động.");
}
