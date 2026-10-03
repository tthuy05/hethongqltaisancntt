using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class MasterService(IRepository repo, IUnitOfWork uow, IAuditWriter audit, IActor actor)
{
    private IQueryable<MasterDto> Query(bool department)
    {
        if (department) return repo.Query<Department>().Select(x => new MasterDto { Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description,
            IsActive = x.IsActive, RowVersion = Convert.ToBase64String(x.RowVersion), ParentDepartmentId = x.ParentDepartmentId });
        return repo.Query<AssetType>().Select(x => new MasterDto { Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description,
            IsActive = x.IsActive, RowVersion = Convert.ToBase64String(x.RowVersion), DefaultUsefulLifeMonths = x.DefaultUsefulLifeMonths });
    }
    public async Task<PagedResponse<MasterDto>> ListAsync(bool department, ListQuery q, CancellationToken ct)
    {
        Validation.List(q, false); var query = Query(department);
        if (q.Keyword != null) { var key = q.Keyword.ToLowerInvariant(); query = query.Where(x => x.Name.ToLower().Contains(key) || x.Code.ToLower().Contains(key)); }
        if (q.Status != null) { var active = q.Status == "Active"; query = query.Where(x => x.IsActive == active); }
        var total = await repo.CountAsync(query, ct); var asc = q.SortDirection == "asc";
        IOrderedQueryable<MasterDto> order = (q.SortBy, asc) switch
        {
            ("code", true) => query.OrderBy(x => x.Code), ("code", false) => query.OrderByDescending(x => x.Code),
            (_, true) => query.OrderBy(x => x.Name), _ => query.OrderByDescending(x => x.Name)
        };
        order = asc ? order.ThenBy(x => x.Id) : order.ThenByDescending(x => x.Id);
        var items = await repo.ListAsync(order.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize), ct);
        return new(items, q.Page, q.PageSize, total, (int)Math.Ceiling(total / (double)q.PageSize));
    }
    public async Task<MasterDto> GetAsync(bool department, long id, CancellationToken ct) =>
        await repo.FirstAsync(Query(department).Where(x => x.Id == id), ct) ?? throw NotFound(department);
    public async Task<MasterDto> WriteAsync(bool department, long? id, MasterRequest x, CancellationToken ct)
    {
        x.Code = Validation.Required(x.Code, 50, "code"); x.Name = Validation.Required(x.Name, 200, "name");
        x.Description = Validation.Optional(x.Description, 1000, "description");
        if (x.DefaultUsefulLifeMonths <= 0) throw Validation.Invalid("defaultUsefulLifeMonths", "Phải > 0.");
        if (department && x.DefaultUsefulLifeMonths != null || !department && x.ParentDepartmentId != null)
            throw Validation.Invalid("fields", "Field không phù hợp master data.");
        if (id != null) Validation.Version(x.RowVersion);
        else if (x.RowVersion != null) throw Validation.Invalid("rowVersion", "Server tạo token.");
        var resultId = await uow.RunAsync(async () =>
        {
            await repo.LockAsync("master-data", ct);
            var lower = x.Code.ToLowerInvariant();
            var duplicate = department
                ? await repo.AnyAsync(repo.Query<Department>().Where(d => d.Id != id && d.Code.ToLower() == lower), ct)
                : await repo.AnyAsync(repo.Query<AssetType>().Where(t => t.Id != id && t.Code.ToLower() == lower), ct);
            if (duplicate) throw new BusinessException(409, department ? "DEPARTMENT_CODE_CONFLICT" : "ASSET_TYPE_CODE_CONFLICT", "Mã đã tồn tại.");
            object entity; object? before = null;
            if (department)
            {
                await ParentAsync(id, x.ParentDepartmentId, ct);
                var d = id == null ? new Department { CreatedByUserId = actor.UserId } :
                    await repo.FirstAsync(repo.Query<Department>(true).Where(d => d.Id == id), ct) ?? throw NotFound(true);
                before = id == null ? null : Safe(d);
                if (id != null) { Immutable(d.Code, x.Code); repo.ExpectVersion(d, Validation.Version(x.RowVersion)); }
                else { d.Code = x.Code; repo.Add(d); }
                d.Name = x.Name; d.Description = x.Description; d.ParentDepartmentId = x.ParentDepartmentId;
                if (id != null) d.UpdatedByUserId = actor.UserId;
                entity = d;
            }
            else
            {
                var t = id == null ? new AssetType() :
                    await repo.FirstAsync(repo.Query<AssetType>(true).Where(t => t.Id == id), ct) ?? throw NotFound(false);
                before = id == null ? null : Safe(t);
                if (id != null) { repo.ExpectVersion(t, Validation.Version(x.RowVersion)); }
                else { t.Code = x.Code; repo.Add(t); }
                t.Code = x.Code; t.Name = x.Name; t.Description = x.Description; t.DefaultUsefulLifeMonths = x.DefaultUsefulLifeMonths;
                entity = t;
            }
            await repo.SaveAsync(ct);
            audit.Record((department ? "departments." : "asset-types.") + (id == null ? "create" : "update"), entity,
                actor.UserId, "USER", before: before, after: Safe(entity));
            await repo.SaveAsync(ct); return Id(entity);
        }, ct);
        return await GetAsync(department, resultId, ct);
    }
    public async Task<MasterDto> StatusAsync(bool department, long id, StatusRequest x, CancellationToken ct)
    {
        Validation.Required(x.Reason, 1000, "reason"); var version = Validation.Version(x.RowVersion);
        if (x.Status is not ("Active" or "Inactive")) throw Validation.Invalid("status", "Active hoặc Inactive.");
        await uow.RunAsync(async () =>
        {
            await repo.LockAsync("master-data", ct);
            object entity;
            if (department) entity = await repo.FirstAsync(repo.Query<Department>(true).Where(d => d.Id == id), ct) ?? throw NotFound(true);
            else entity = await repo.FirstAsync(repo.Query<AssetType>(true).Where(t => t.Id == id), ct) ?? throw NotFound(false);
            var before = Safe(entity);
            if (entity is Department d) { repo.ExpectVersion(d, version); d.IsActive = x.Status == "Active"; d.UpdatedByUserId = actor.UserId; }
            if (entity is AssetType t) { repo.ExpectVersion(t, version); t.IsActive = x.Status == "Active"; }
            audit.Record((department ? "departments." : "asset-types.") + "status.change", entity, actor.UserId, "USER", before: before, after: Safe(entity));
            await repo.SaveAsync(ct); return true;
        }, ct);
        return await GetAsync(department, id, ct);
    }
    private async Task ParentAsync(long? id, long? parentId, CancellationToken ct)
    {
        if (parentId == null) return;
        if (parentId <= 0) throw Validation.Invalid("parentDepartmentId", "ID phải > 0.");
        var seen = new HashSet<long>(); long? cursor = parentId;
        while (cursor != null)
        {
            if (cursor == id || !seen.Add(cursor.Value)) throw Validation.Invalid("parentDepartmentId", "Không được tạo vòng lặp phòng ban.");
            var parent = await repo.FirstAsync(repo.Query<Department>().Where(d => d.Id == cursor), ct)
                ?? throw Validation.Invalid("parentDepartmentId", "Không tìm thấy phòng ban cha.");
            if (cursor == parentId && !parent.IsActive) throw Validation.Invalid("parentDepartmentId", "Phòng ban cha không hoạt động.");
            cursor = parent.ParentDepartmentId;
        }
    }
    private static void Immutable(string old, string current)
    { if (old != current) throw Validation.Invalid("code", "Code không được thay đổi."); }
    private static long Id(object entity) => entity is Department d ? d.Id : ((AssetType)entity).Id;
    private static object Safe(object entity) => entity is Department d
        ? new { d.Code, d.Name, d.IsActive, d.ParentDepartmentId }
        : (object)new { ((AssetType)entity).Code, ((AssetType)entity).Name, ((AssetType)entity).IsActive, ((AssetType)entity).DefaultUsefulLifeMonths };
    private static BusinessException NotFound(bool d) => new(404, d ? "DEPARTMENT_NOT_FOUND" : "ASSET_TYPE_NOT_FOUND", "Không tìm thấy master data.");
}
