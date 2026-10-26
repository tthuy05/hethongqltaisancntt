using System.Text.Json.Serialization;
using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public static class LicensePermissions
{
    public const string Create = "licenses.create", Update = "licenses.update", Archive = "licenses.archive";
    public static readonly string[] Metadata = [Permissions.LicenseRead, Create, Update, Archive];
    public static readonly string[] All = [.. Metadata, Permissions.LicenseKeyManage];
}
public interface ILicensePersistence { Task LockAsync(long id, CancellationToken ct); }
public sealed class LicenseRequest
{
    private string? key; private decimal? cost;
    public long SoftwareId { get; set; }
    public string LicenseType { get; set; } = "";
    public int TotalQuantity { get; set; }
    public string? Vendor { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }
    public string? RowVersion { get; set; }
    public decimal? PurchaseCost { get => cost; set { cost = value; HasCost = true; } }
    public string? LicenseKey { get => key; set { key = value; HasKey = true; } }
    [JsonIgnore] public bool HasKey { get; private set; }
    [JsonIgnore] public bool HasCost { get; private set; }
    public override string ToString() => "[license metadata request]";
}
public sealed class LicenseQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? SoftwareId { get; set; }
    public string? Keyword { get; set; }
    public string? Vendor { get; set; }
    public string? Status { get; set; }
    public DateTime? ExpirationFrom { get; set; }
    public DateTime? ExpirationTo { get; set; }
    public string SortBy { get; set; } = "licenseCode";
    public string SortDirection { get; set; } = "asc";
}
public sealed record LicenseSoftwareDto(long Id, string Name);
public sealed record LicenseDto(long Id, string LicenseCode, LicenseSoftwareDto Software, string LicenseType,
    int TotalQuantity, int UsedQuantity, int AvailableQuantity, bool IsActive, string Status, string? Vendor,
    DateTime? PurchasedAt, DateTime? StartsAt, DateTime? ExpiresAt, string? Notes, string? MaskedKey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? PurchaseCost,
    string RowVersion);

public static class LicenseValidation
{
    public static readonly string[] Types = ["PER_USER", "PER_DEVICE", "VOLUME", "SUBSCRIPTION", "OTHER"];
    public static void Utc(DateTime? value, string field) { if (value.HasValue && value.Value.Kind != DateTimeKind.Utc) throw Validation.Invalid(field, "Thời điểm phải là UTC."); }
    public static void Request(LicenseRequest value, bool update)
    {
        if (value.SoftwareId <= 0) throw Validation.Invalid("softwareId", "Chọn phần mềm hợp lệ.");
        if (!Types.Contains(value.LicenseType)) throw Validation.Invalid("licenseType", "Loại bản quyền không hợp lệ.");
        if (value.TotalQuantity <= 0) throw Validation.Invalid("totalQuantity", "Số lượng phải lớn hơn 0.");
        value.Vendor = Validation.Optional(value.Vendor, 250, "vendor"); value.Notes = Validation.Optional(value.Notes, 2000, "notes");
        Utc(value.PurchasedAt, "purchasedAt"); Utc(value.StartsAt, "startsAt"); Utc(value.ExpiresAt, "expiresAt");
        if (value.ExpiresAt.HasValue && value.StartsAt.HasValue && value.ExpiresAt <= value.StartsAt) throw Validation.Invalid("expiresAt", "Hết hạn phải sau thời điểm hiệu lực.");
        if (value.PurchaseCost is < 0 or > 9999999999999999.99m || value.PurchaseCost.HasValue && decimal.Round(value.PurchaseCost.Value, 2) != value.PurchaseCost)
            throw Validation.Invalid("purchaseCost", "Chi phí numeric(18,2) không âm, tối đa hai chữ số thập phân.");
        if (update) Validation.Version(value.RowVersion); else if (value.RowVersion != null) throw Validation.Invalid("rowVersion", "Không gửi phiên bản khi tạo mới.");
    }
}

public sealed class LicenseService(IRepository repo, ILicensePersistence persistence, IUnitOfWork uow, IActor actor,
    IAuditWriter audit, ILicenseCodeGenerator codes, LicenseKeyAccessPolicy keys, ILicenseKeyProtector protector, TimeProvider clock)
{
    private bool Cost => actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
    private void Require(string grant) { if (actor.UserId is not > 0 || !actor.Has(Permissions.LicenseRead) || !actor.Has(grant)) throw Denied(); }
    private static BusinessException Denied() => new(403, "FORBIDDEN", "Không có quyền quản lý bản quyền.");
    private static BusinessException Missing() => new(404, "LICENSE_NOT_FOUND", "Không tìm thấy bản quyền.");
    private IQueryable<LicenseAssignment> ActiveSeats(long id) => repo.Query<LicenseAssignment>().Where(x => x.SoftwareLicenseId == id && x.RevokedAtUtc == null && !x.IsArchived);
    private sealed class Row { public SoftwareLicense License { get; set; } = null!; public string Name { get; set; } = ""; public int Used { get; set; } }
    private IQueryable<Row> Rows(IQueryable<SoftwareLicense> query) => from license in query join software in repo.Query<Software>() on license.SoftwareId equals software.Id
        select new Row { License = license, Name = software.Name, Used = repo.Query<LicenseAssignment>().Count(x => x.SoftwareLicenseId == license.Id && x.RevokedAtUtc == null && !x.IsArchived) };
    private string Status(SoftwareLicense item, int used) {
        var now = clock.GetUtcNow().UtcDateTime;
        return !item.IsActive ? "INACTIVE" : item.ExpiresAtUtc < now ? "EXPIRED" : item.StartsAtUtc > now ? "NOT_EFFECTIVE" :
            item.ExpiresAtUtc <= now.AddDays(30) ? "EXPIRING_SOON" : used >= item.TotalQuantity ? "EXHAUSTED" : "ACTIVE";
    }
    private LicenseDto Map(Row row) {
        var x = row.License;
        if (row.Used > x.TotalQuantity) throw new BusinessException(500, "DATA_INCONSISTENT", "Số lượng bản quyền không nhất quán.");
        return new(x.Id, x.LicenseCode, new(x.SoftwareId, row.Name), x.LicenseType, x.TotalQuantity, row.Used, x.TotalQuantity - row.Used,
            x.IsActive, Status(x, row.Used), x.Vendor, x.PurchasedAtUtc, x.StartsAtUtc, x.ExpiresAtUtc, x.Notes, keys.MaskedKey(x), Cost ? x.PurchaseCost : null, Convert.ToBase64String(x.RowVersion));
    }
    public async Task<LicenseDto> GetAsync(long id, CancellationToken ct) { Require(Permissions.LicenseRead); return Map(await repo.FirstAsync(Rows(repo.Query<SoftwareLicense>().Where(x => x.Id == id)), ct) ?? throw Missing()); }
    public async Task<PagedResponse<LicenseDto>> ListAsync(LicenseQuery filter, CancellationToken ct)
    {
        Require(Permissions.LicenseRead);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue || filter.SoftwareId <= 0) throw Validation.Invalid("page", "Phân trang/bộ lọc không hợp lệ.");
        if (filter.SortBy is not ("licenseCode" or "expiresAt" or "createdAt" or "totalQuantity") || filter.SortDirection is not ("asc" or "desc")) throw Validation.Invalid("sortBy", "Sắp xếp không hợp lệ.");
        if (filter.Status is not (null or "ACTIVE" or "INACTIVE" or "EXPIRED" or "EXPIRING_SOON" or "EXHAUSTED" or "NOT_EFFECTIVE")) throw Validation.Invalid("status", "Trạng thái không hợp lệ.");
        LicenseValidation.Utc(filter.ExpirationFrom, "expirationFrom"); LicenseValidation.Utc(filter.ExpirationTo, "expirationTo");
        if (filter.ExpirationFrom > filter.ExpirationTo) throw Validation.Invalid("expirationTo", "Khoảng thời gian không hợp lệ.");
        var term = Validation.Optional(filter.Keyword, 200, "keyword")?.ToLowerInvariant(); var vendor = Validation.Optional(filter.Vendor, 250, "vendor")?.ToLowerInvariant();
        var query = repo.Query<SoftwareLicense>(); var now = clock.GetUtcNow().UtcDateTime; var soon = now.AddDays(30);
        if (filter.SoftwareId.HasValue) query = query.Where(x => x.SoftwareId == filter.SoftwareId);
        if (term != null) query = query.Where(x => x.LicenseCode.ToLower().Contains(term) || repo.Query<Software>().Any(s => s.Id == x.SoftwareId && s.Name.ToLower().Contains(term)));
        if (vendor != null) query = query.Where(x => x.Vendor != null && x.Vendor.ToLower().Contains(vendor));
        if (filter.ExpirationFrom.HasValue) query = query.Where(x => x.ExpiresAtUtc >= filter.ExpirationFrom);
        if (filter.ExpirationTo.HasValue) query = query.Where(x => x.ExpiresAtUtc <= filter.ExpirationTo);
        if (filter.Status != null) query = filter.Status switch {
            "INACTIVE" => query.Where(x => !x.IsActive), "EXPIRED" => query.Where(x => x.IsActive && x.ExpiresAtUtc < now),
            "NOT_EFFECTIVE" => query.Where(x => x.IsActive && (x.ExpiresAtUtc == null || x.ExpiresAtUtc >= now) && x.StartsAtUtc > now),
            "EXPIRING_SOON" => query.Where(x => x.IsActive && (x.StartsAtUtc == null || x.StartsAtUtc <= now) && x.ExpiresAtUtc >= now && x.ExpiresAtUtc <= soon),
            "EXHAUSTED" => query.Where(x => x.IsActive && (x.StartsAtUtc == null || x.StartsAtUtc <= now) && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > soon) && repo.Query<LicenseAssignment>().Count(a => a.SoftwareLicenseId == x.Id && a.RevokedAtUtc == null && !a.IsArchived) >= x.TotalQuantity),
            _ => query.Where(x => x.IsActive && (x.StartsAtUtc == null || x.StartsAtUtc <= now) && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > soon) && repo.Query<LicenseAssignment>().Count(a => a.SoftwareLicenseId == x.Id && a.RevokedAtUtc == null && !a.IsArchived) < x.TotalQuantity)
        };
        var total = await repo.CountAsync(query, ct); var asc = filter.SortDirection == "asc";
        var ordered = (filter.SortBy, asc) switch { ("expiresAt", true) => query.OrderBy(x => x.ExpiresAtUtc), ("expiresAt", false) => query.OrderByDescending(x => x.ExpiresAtUtc),
            ("createdAt", true) => query.OrderBy(x => x.CreatedAtUtc), ("createdAt", false) => query.OrderByDescending(x => x.CreatedAtUtc), ("totalQuantity", true) => query.OrderBy(x => x.TotalQuantity), ("totalQuantity", false) => query.OrderByDescending(x => x.TotalQuantity),
            (_, true) => query.OrderBy(x => x.LicenseCode), _ => query.OrderByDescending(x => x.LicenseCode) };
        ordered = asc ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);
        return new((await repo.ListAsync(Rows(ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)), ct)).Select(Map).ToArray(), filter.Page, filter.PageSize, total, (int)Math.Ceiling(total / (double)filter.PageSize));
    }
    public async Task<PagedResponse<LicenseSoftwareDto>> SoftwareOptionsAsync(int page, string? keyword, CancellationToken ct) {
        Require(Permissions.LicenseRead); if (page < 1 || page > int.MaxValue / 50) throw Validation.Invalid("page", "Trang không hợp lệ.");
        var term = Validation.Optional(keyword, 200, "keyword")?.ToLowerInvariant(); var query = repo.Query<Software>().Where(x => x.IsActive);
        if (term != null) query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
        var total = await repo.CountAsync(query, ct); var items = await repo.ListAsync(query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50).Select(x => new LicenseSoftwareDto(x.Id, x.Name)), ct);
        return new(items, page, 50, total, (int)Math.Ceiling(total / 50d));
    }
    private async Task AuthorizeAsync(string permission, bool key, CancellationToken ct) {
        var grants = new List<string> { Permissions.LicenseRead, permission };
        if (Cost) grants.Add(actor.Has(Permissions.AssetCost) ? Permissions.AssetCost : Permissions.ReportCost);
        if (key) grants.Add(Permissions.LicenseKeyManage);
        await WorkflowAuthorization.RequireAsync(repo, actor, ct, grants.ToArray());
        if (key && !await repo.AnyAsync(from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id where link.UserId == actor.UserId && role.Code == "ADMIN_IT" && role.IsActive select link.Id, ct)) throw Denied();
    }
    public async Task<LicenseDto> WriteAsync(long? id, LicenseRequest request, CancellationToken ct)
    {
        var permission = id.HasValue ? LicensePermissions.Update : LicensePermissions.Create; Require(permission);
        if (request.HasCost && !Cost || request.HasKey && !keys.CanManage) throw Denied(); LicenseValidation.Request(request, id.HasValue);
        return await uow.RunAsync(async () => {
            await repo.LockAsync("software:" + request.SoftwareId, ct);
            if (id.HasValue) await persistence.LockAsync(id.Value, ct);
            await AuthorizeAsync(permission, request.HasKey, ct);
            var software = await repo.FirstAsync(repo.Query<Software>().Where(x => x.Id == request.SoftwareId && x.IsActive), ct) ?? throw new BusinessException(409, "SOFTWARE_INACTIVE", "Phần mềm không còn hoạt động.");
            SoftwareLicense item;
            if (id.HasValue) {
                item = await repo.FirstAsync(repo.Query<SoftwareLicense>(true).Where(x => x.Id == id), ct) ?? throw Missing();
                if (item.SoftwareId != request.SoftwareId) throw Validation.Invalid("softwareId", "Không thể đổi phần mềm của bản quyền đã cấp mã.");
                CheckVersion(item, Validation.Version(request.RowVersion)); if (!item.IsActive) throw new BusinessException(409, "LICENSE_INACTIVE", "Bản quyền đã ngừng hoạt động.");
            } else { item = new SoftwareLicense { SoftwareId = request.SoftwareId, LicenseCode = await codes.NextAsync(ct), CreatedAtUtc = clock.GetUtcNow().UtcDateTime }; repo.Add(item); }
            var used = id.HasValue ? await repo.CountAsync(ActiveSeats(item.Id), ct) : 0;
            LicenseCapacityPolicy.CheckCanUpdateQuantity(item, request.TotalQuantity, used);
            if (used > 0 && item.LicenseType != request.LicenseType) throw new BusinessException(409, "LICENSE_TYPE_IN_USE", "Không đổi loại khi còn bản quyền đang cấp phát.");
            item.LicenseType = request.LicenseType; item.TotalQuantity = request.TotalQuantity; item.Vendor = request.Vendor;
            item.PurchasedAtUtc = request.PurchasedAt; item.StartsAtUtc = request.StartsAt; item.ExpiresAtUtc = request.ExpiresAt; item.Notes = request.Notes;
            if (request.HasCost) item.PurchaseCost = request.PurchaseCost;
            if (id.HasValue) item.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repo.SaveAsync(ct);
            if (request.HasKey) {
                if (request.LicenseKey == null) { item.LicenseKeyCiphertext = null; item.LicenseKeyLast4 = null; item.KeyVersion = null; }
                else {
                    LicenseKeyEnvelope envelope; try { envelope = protector.Protect(item.Id, request.LicenseKey); } catch (ArgumentException) { throw Validation.Invalid("licenseKey", "Key phải có ít nhất 5 ký tự, không ký tự điều khiển, tối đa 4096 UTF-8 bytes."); }
                    item.LicenseKeyCiphertext = envelope.Ciphertext; item.LicenseKeyLast4 = envelope.Last4; item.KeyVersion = envelope.KeyVersion;
                }
                await repo.SaveAsync(ct);
            }
            audit.Record(id.HasValue ? "licenses.update" : "licenses.create", item, actor.UserId, "USER", after: new { item.SoftwareId, item.TotalQuantity, item.LicenseType, item.IsActive });
            await repo.SaveAsync(ct); return Map(new() { License = item, Name = software.Name, Used = used });
        }, ct);
    }
    private void CheckVersion(SoftwareLicense item, byte[] version) { if (!item.RowVersion.SequenceEqual(version)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi. Hãy tải lại."); repo.ExpectVersion(item, version); }
    public async Task ArchiveAsync(long id, byte[] expected, CancellationToken ct) {
        Require(LicensePermissions.Archive);
        var parent = await repo.FirstAsync(repo.Query<SoftwareLicense>().Where(x => x.Id == id), ct) ?? throw Missing();
        await uow.RunAsync(async () => {
            await repo.LockAsync("software:" + parent.SoftwareId, ct); await persistence.LockAsync(id, ct); await AuthorizeAsync(LicensePermissions.Archive, false, ct);
            var item = await repo.FirstAsync(repo.Query<SoftwareLicense>(true).Where(x => x.Id == id), ct) ?? throw Missing(); CheckVersion(item, expected);
            if (!item.IsActive) throw new BusinessException(409, "LICENSE_INACTIVE", "Bản quyền đã ngừng hoạt động.");
            LicenseCapacityPolicy.CheckCanArchive(item, await repo.CountAsync(ActiveSeats(id), ct)); item.IsActive = false; item.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            audit.Record("licenses.archive", item, actor.UserId, "USER", after: new { item.IsActive }); await repo.SaveAsync(ct); return true;
        }, ct);
    }
}
