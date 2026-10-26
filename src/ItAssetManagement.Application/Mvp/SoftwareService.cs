using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public static class SoftwarePermissions
{
    public const string Read = "software.read", Create = "software.create", Update = "software.update", Archive = "software.archive";
    public static readonly string[] All = [Read, Create, Update, Archive];
}
public sealed class SoftwareRequest
{
    public string Name { get; set; } = "";
    public string? Publisher { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? RowVersion { get; set; }
}
public sealed class SoftwareListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? Vendor { get; set; }
    public string SortBy { get; set; } = "name";
    public string SortDirection { get; set; } = "asc";
}
public sealed record SoftwareDto(long Id, string Code, string Name, string? Publisher, string? Version,
    string? Description, bool IsActive, [property: JsonPropertyName("createdAt")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAtUtc, string RowVersion);

public sealed class SoftwareService(IRepository repo, IUnitOfWork uow, IActor actor, IAuditWriter audit, ISoftwareCodeGenerator codes)
{
    private void Require(string permission) {
        if (actor.UserId is not > 0 || !actor.Has(SoftwarePermissions.Read) || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền quản lý phần mềm.");
    }
    private static void Positive(long id) { if (id <= 0) throw Validation.Invalid("softwareId", "ID phải là số nguyên dương."); }
    private static BusinessException Missing() => new(404, "SOFTWARE_NOT_FOUND", "Không tìm thấy phần mềm.");
    private static SoftwareDto Map(Software x) => new(x.Id, x.Code, x.Name, x.Publisher, x.Version,
        x.Description, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc, Convert.ToBase64String(x.RowVersion));
    public async Task<SoftwareDto> GetAsync(long id, CancellationToken ct) {
        Require(SoftwarePermissions.Read); Positive(id);
        return Map(await repo.FirstAsync(repo.Query<Software>().Where(x => x.Id == id), ct) ?? throw Missing());
    }
    public async Task<PagedResponse<SoftwareDto>> ListAsync(SoftwareListQuery filter, CancellationToken ct) {
        Require(SoftwarePermissions.Read);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "Phân trang không hợp lệ.");
        if (filter.Status is not (null or "Active" or "Inactive")) throw Validation.Invalid("status", "Trạng thái không hợp lệ.");
        if (filter.SortBy is not ("name" or "code" or "publisher" or "createdAt") || filter.SortDirection is not ("asc" or "desc"))
            throw Validation.Invalid("sortBy", "Cột/chiều sắp xếp không hợp lệ.");
        var term = Validation.Optional(filter.Keyword, 200, "keyword")?.ToLowerInvariant();
        var vendor = Validation.Optional(filter.Vendor, 250, "vendor")?.ToLowerInvariant();
        var query = repo.Query<Software>();
        if (term != null) query = query.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        if (vendor != null) query = query.Where(x => x.Publisher != null && x.Publisher.ToLower().Contains(vendor));
        if (filter.Status != null) { var active = filter.Status == "Active"; query = query.Where(x => x.IsActive == active); }
        var total = await repo.CountAsync(query, ct); var asc = filter.SortDirection == "asc";
        var ordered = (filter.SortBy, asc) switch {
            ("code", true) => query.OrderBy(x => x.Code), ("code", false) => query.OrderByDescending(x => x.Code),
            ("publisher", true) => query.OrderBy(x => x.Publisher), ("publisher", false) => query.OrderByDescending(x => x.Publisher),
            ("createdAt", true) => query.OrderBy(x => x.CreatedAtUtc), ("createdAt", false) => query.OrderByDescending(x => x.CreatedAtUtc),
            (_, true) => query.OrderBy(x => x.Name), _ => query.OrderByDescending(x => x.Name)
        };
        ordered = asc ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);
        var items = await repo.ListAsync(ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize), ct);
        return new(items.Select(Map).ToList(), filter.Page, filter.PageSize, total, (int)Math.Ceiling(total / (double)filter.PageSize));
    }
    public async Task<SoftwareDto> WriteAsync(long? id, SoftwareRequest request, CancellationToken ct) {
        var permission = id.HasValue ? SoftwarePermissions.Update : SoftwarePermissions.Create; Require(permission);
        if (id.HasValue) Positive(id.Value);
        var name = Validation.Required(request.Name, 250, "name"); var publisher = Validation.Optional(request.Publisher, 250, "publisher");
        var version = Validation.Optional(request.Version, 100, "version"); var description = Validation.Optional(request.Description, 2000, "description");
        var expected = id.HasValue ? Validation.Version(request.RowVersion) : null;
        if (!id.HasValue && request.RowVersion != null) throw Validation.Invalid("rowVersion", "Không gửi phiên bản khi tạo mới.");
        return await uow.RunAsync(async () => {
            // License writers must acquire this parent coordinator before their license lock.
            await repo.LockAsync(id.HasValue ? "software:" + id.Value : "software-catalog", ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, SoftwarePermissions.Read, permission);
            Software item;
            if (id.HasValue) {
                item = await repo.FirstAsync(repo.Query<Software>(true).Where(x => x.Id == id.Value), ct) ?? throw Missing();
                CheckVersion(item, expected!); if (!item.IsActive) throw Archived();
            } else {
                item = new Software { Code = await codes.NextAsync(ct), CreatedAtUtc = DateTime.UtcNow }; repo.Add(item);
            }
            var before = id.HasValue ? new { item.IsActive } : null;
            item.Name = name; item.Publisher = publisher; item.Version = version; item.Description = description;
            if (id.HasValue) item.UpdatedAtUtc = DateTime.UtcNow;
            await repo.SaveAsync(ct);
            audit.Record(id.HasValue ? "software.update" : "software.create", item, actor.UserId, "USER", before: before,
                after: new { item.Code, item.IsActive });
            await repo.SaveAsync(ct);
            return Map(item);
        }, ct);
    }
    private static BusinessException Archived() => new(409, "SOFTWARE_INACTIVE", "Phần mềm đã ngừng hoạt động.");
    private void CheckVersion(Software item, byte[] version) {
        if (!item.RowVersion.SequenceEqual(version)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi. Hãy tải lại.");
        repo.ExpectVersion(item, version);
    }
    public async Task ArchiveAsync(long id, byte[] expected, CancellationToken ct) {
        Require(SoftwarePermissions.Archive); Positive(id);
        if (expected.Length != 16) throw Validation.Invalid("rowVersion", "Phiên bản bản ghi không hợp lệ.");
        await uow.RunAsync(async () => {
            await repo.LockAsync("software:" + id, ct);
            await WorkflowAuthorization.RequireAsync(repo, actor, ct, SoftwarePermissions.Read, SoftwarePermissions.Archive);
            var item = await repo.FirstAsync(repo.Query<Software>(true).Where(x => x.Id == id), ct) ?? throw Missing();
            CheckVersion(item, expected); if (!item.IsActive) throw Archived();
            if (await repo.AnyAsync(repo.Query<SoftwareLicense>().Where(x => x.SoftwareId == id && x.IsActive), ct))
                throw new BusinessException(409, "SOFTWARE_ACTIVE_LICENSES", "Phần mềm còn bản quyền hoạt động.");
            item.IsActive = false; item.UpdatedAtUtc = DateTime.UtcNow;
            audit.Record("software.archive", item, actor.UserId, "USER", before: new { isActive = true }, after: new { item.IsActive });
            await repo.SaveAsync(ct); return true;
        }, ct);
    }
}
