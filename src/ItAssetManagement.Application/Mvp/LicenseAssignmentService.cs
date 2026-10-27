using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class LicenseAssignmentDto
{
    public long Id { get; set; }
    public long SoftwareLicenseId { get; set; }
    public string LicenseCode { get; set; } = "";
    public string SoftwareName { get; set; } = "";
    public long? AssignedUserId { get; set; }
    public string? AssignedUserFullName { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AssignedUserEmail { get; set; }
    public long? AssignedAssetId { get; set; }
    public string? AssignedAssetCode { get; set; }
    public string? AssignedAssetName { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public long AssignedByUserId { get; set; }
    public string? AssignedByUserName { get; set; }
    public long? RevokedByUserId { get; set; }
    public string? RevokedByUserName { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string RowVersion { get; set; } = "";
}

public sealed class AssignLicenseRequest
{
    public long SoftwareLicenseId { get; set; }
    public long? UserId { get; set; }
    public long? AssetId { get; set; }
    public DateTime? AssignedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class RevokeLicenseAssignmentRequest
{
    public DateTime? RevokedAt { get; set; }
    public string? Reason { get; set; }
    public string RowVersion { get; set; } = "";
}

public sealed class TransferLicenseAssignmentRequest
{
    public long? UserId { get; set; }
    public long? AssetId { get; set; }
    public DateTime? TransferredAt { get; set; }
    public string? Reason { get; set; }
    public string RowVersion { get; set; } = "";
}

public sealed class TransferResultDto
{
    public LicenseAssignmentDto Previous { get; set; } = null!;
    public LicenseAssignmentDto Current { get; set; } = null!;
}

public sealed class LicenseAssignmentQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? LicenseId { get; set; }
    public long? UserId { get; set; }
    public long? AssetId { get; set; }
    public string? Status { get; set; } // "Active", "Revoked", "All"
    public string? TargetType { get; set; } // "user", "asset"
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public sealed class LicenseAssignmentService(
    IRepository repo,
    ILicensePersistence persistence,
    IUnitOfWork uow,
    IActor actor,
    IAuditWriter audit,
    TimeProvider clock)
{
    private void Require(string grant)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.LicenseRead) || !actor.Has(LicensePermissions.AssignmentRead) || !actor.Has(grant))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền quản lý cấp phát bản quyền.");
    }

    private static BusinessException Missing() => new(404, "ASSIGNMENT_NOT_FOUND", "Không tìm thấy thông tin cấp phát.");

    private sealed class AssignmentRow
    {
        public LicenseAssignment Assignment { get; set; } = null!;
        public string LicenseCode { get; set; } = "";
        public string SoftwareName { get; set; } = "";
        public string? UserFullName { get; set; }
        public string? UserEmail { get; set; }
        public string? AssetCode { get; set; }
        public string? AssetName { get; set; }
        public string? AssignedByUserName { get; set; }
        public string? RevokedByUserName { get; set; }
    }

    private IQueryable<AssignmentRow> Rows(IQueryable<LicenseAssignment> query) =>
        from a in query
        join l in repo.Query<SoftwareLicense>() on a.SoftwareLicenseId equals l.Id
        join s in repo.Query<Software>() on l.SoftwareId equals s.Id
        join u in repo.Query<User>() on a.AssignedUserId equals (long?)u.Id into users
        from u in users.DefaultIfEmpty()
        join ast in repo.Query<Asset>() on a.AssignedAssetId equals (long?)ast.Id into assets
        from ast in assets.DefaultIfEmpty()
        join assigner in repo.Query<User>() on a.AssignedByUserId equals assigner.Id into assigners
        from assigner in assigners.DefaultIfEmpty()
        join revoker in repo.Query<User>() on a.RevokedByUserId equals (long?)revoker.Id into revokers
        from revoker in revokers.DefaultIfEmpty()
        select new AssignmentRow
        {
            Assignment = a,
            LicenseCode = l.LicenseCode,
            SoftwareName = s.Name,
            UserFullName = u != null ? u.FullName : null,
            UserEmail = u != null ? u.Email : null,
            AssetCode = ast != null ? ast.AssetCode : null,
            AssetName = ast != null ? ast.Name : null,
            AssignedByUserName = assigner != null ? assigner.FullName : null,
            RevokedByUserName = revoker != null ? revoker.FullName : null
        };

    private LicenseAssignmentDto Map(AssignmentRow row)
    {
        var a = row.Assignment;
        return new LicenseAssignmentDto
        {
            Id = a.Id,
            SoftwareLicenseId = a.SoftwareLicenseId,
            LicenseCode = row.LicenseCode,
            SoftwareName = row.SoftwareName,
            AssignedUserId = a.AssignedUserId,
            AssignedUserFullName = row.UserFullName,
            AssignedUserEmail = actor.Has(Permissions.UserLookup) || actor.Has(Permissions.UserRead) ? row.UserEmail : null,
            AssignedAssetId = a.AssignedAssetId,
            AssignedAssetCode = row.AssetCode,
            AssignedAssetName = row.AssetName,
            AssignedAtUtc = a.AssignedAtUtc,
            RevokedAtUtc = a.RevokedAtUtc,
            AssignedByUserId = a.AssignedByUserId,
            AssignedByUserName = row.AssignedByUserName,
            RevokedByUserId = a.RevokedByUserId,
            RevokedByUserName = row.RevokedByUserName,
            Notes = a.Notes,
            IsArchived = a.IsArchived,
            CreatedAtUtc = a.CreatedAtUtc,
            RowVersion = Convert.ToBase64String(a.RowVersion)
        };
    }

    public async Task<LicenseAssignmentDto> GetAsync(long id, CancellationToken ct)
    {
        Require(LicensePermissions.AssignmentRead);
        var row = await repo.FirstAsync(Rows(repo.Query<LicenseAssignment>().Where(x => x.Id == id)), ct)
            ?? throw Missing();
        return Map(row);
    }

    public async Task<PagedResponse<LicenseAssignmentDto>> ListAsync(LicenseAssignmentQuery filter, CancellationToken ct)
    {
        Require(LicensePermissions.AssignmentRead);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "Phân trang không hợp lệ.");
        if (filter.LicenseId <= 0 || filter.UserId <= 0 || filter.AssetId <= 0) throw Validation.Invalid("filter", "ID phải lớn hơn 0.");
        if (filter.Status is not (null or "All" or "Active" or "Revoked")) throw Validation.Invalid("status", "Trạng thái không hợp lệ.");
        if (filter.TargetType is not (null or "user" or "asset")) throw Validation.Invalid("targetType", "Loại đối tượng không hợp lệ.");
        LicenseValidation.Utc(filter.From, "from"); LicenseValidation.Utc(filter.To, "to");
        if (filter.From > filter.To) throw Validation.Invalid("to", "Khoảng thời gian không hợp lệ.");
        if (filter.SortBy is not (null or "assignedAt" or "revokedAt" or "id") || filter.SortDirection is not (null or "asc" or "desc")) throw Validation.Invalid("sortBy", "Sắp xếp không hợp lệ.");
        if (filter.LicenseId.HasValue && !await repo.AnyAsync(repo.Query<SoftwareLicense>().Where(x => x.Id == filter.LicenseId), ct))
            throw new BusinessException(404, "LICENSE_NOT_FOUND", "Không tìm thấy bản quyền.");

        var query = repo.Query<LicenseAssignment>();

        if (filter.LicenseId.HasValue && filter.LicenseId > 0)
            query = query.Where(x => x.SoftwareLicenseId == filter.LicenseId.Value);

        if (filter.UserId.HasValue && filter.UserId > 0)
            query = query.Where(x => x.AssignedUserId == filter.UserId.Value);

        if (filter.AssetId.HasValue && filter.AssetId > 0)
            query = query.Where(x => x.AssignedAssetId == filter.AssetId.Value);

        if (string.Equals(filter.TargetType, "user", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.AssignedUserId != null);
        else if (string.Equals(filter.TargetType, "asset", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.AssignedAssetId != null);

        var status = filter.Status?.Trim();
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.RevokedAtUtc == null && !x.IsArchived);
        else if (string.Equals(status, "Revoked", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.RevokedAtUtc != null || x.IsArchived);

        if (filter.From.HasValue)
            query = query.Where(x => x.AssignedAtUtc >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(x => x.AssignedAtUtc <= filter.To.Value);

        var total = await repo.CountAsync(query, ct);
        var asc = filter.SortDirection == "asc";
        var ordered = (filter.SortBy, asc) switch {
            ("id", true) => query.OrderBy(x => x.Id), ("id", false) => query.OrderByDescending(x => x.Id),
            ("revokedAt", true) => query.OrderBy(x => x.RevokedAtUtc), ("revokedAt", false) => query.OrderByDescending(x => x.RevokedAtUtc),
            (_, true) => query.OrderBy(x => x.AssignedAtUtc), _ => query.OrderByDescending(x => x.AssignedAtUtc)
        };
        query = asc ? ordered.ThenBy(x => x.Id) : ordered.ThenByDescending(x => x.Id);

        var rows = await repo.ListAsync(Rows(query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)), ct);
        return new PagedResponse<LicenseAssignmentDto>(
            rows.Select(Map).ToList(),
            filter.Page,
            filter.PageSize,
            total,
            (int)Math.Ceiling(total / (double)filter.PageSize));
    }

    private void CheckVersion(LicenseAssignment item, byte[] version)
    {
        if (!item.RowVersion.SequenceEqual(version))
            throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu cấp phát đã bị thay đổi bởi người khác.");
        repo.ExpectVersion(item, version);
    }

    // Lock order: target asset -> software parent -> license -> assignment -> users-identity.
    // All capacity writers share the license row lock used by metadata quantity/archive.
    private async Task LockAsync(long licenseId, long? assetId, long? assignmentId, CancellationToken ct)
    {
        var parent = await repo.FirstAsync(repo.Query<SoftwareLicense>().Where(x => x.Id == licenseId), ct)
            ?? throw new BusinessException(404, "LICENSE_NOT_FOUND", "Không tìm thấy bản quyền.");
        if (assetId.HasValue) await repo.LockAssetAsync(assetId.Value, ct);
        await repo.LockAsync("software:" + parent.SoftwareId, ct);
        await persistence.LockAsync(licenseId, ct);
        if (assignmentId.HasValue) await persistence.LockAssignmentAsync(assignmentId.Value, ct);
    }
    private Task AuthorizeAsync(string grant, CancellationToken ct) => WorkflowAuthorization.RequireAsync(repo, actor, ct,
        Permissions.LicenseRead, LicensePermissions.AssignmentRead, grant);
    private static string? Notes(string? existing, string? reason, string prefix) => string.IsNullOrWhiteSpace(reason) ? existing :
        Validation.Optional((string.IsNullOrWhiteSpace(existing) ? "" : existing + " | ") + prefix + reason, 1000, "reason");
    private void Time(DateTime at, DateTime? lower, string field)
    {
        LicenseValidation.Utc(at, field);
        if (at > clock.GetUtcNow().UtcDateTime || lower.HasValue && at < lower) throw Validation.Invalid(field, "Không được ở tương lai hoặc trước ngày cấp.");
    }
    private async Task<SoftwareLicense> LicenseAsync(long id, CancellationToken ct) =>
        await repo.FirstAsync(repo.Query<SoftwareLicense>().Where(x => x.Id == id), ct) ?? throw new BusinessException(404, "LICENSE_NOT_FOUND", "Không tìm thấy bản quyền.");
    private async Task TargetAsync(SoftwareLicense license, long? userId, long? assetId, CancellationToken ct)
    {
        if (license.LicenseType == "PER_USER" && !userId.HasValue || license.LicenseType == "PER_DEVICE" && !assetId.HasValue)
            throw Validation.Invalid("target", "Loại bản quyền không phù hợp với đối tượng nhận.");
        if (!await repo.AnyAsync(repo.Query<Software>().Where(x => x.Id == license.SoftwareId && x.IsActive), ct))
            throw new BusinessException(409, "SOFTWARE_INACTIVE", "Phần mềm không còn hoạt động.");
        if (userId.HasValue && !await repo.AnyAsync(repo.Query<User>().Where(x => x.Id == userId && x.IsActive && x.IsEmailConfirmed && !x.IsAdminLocked), ct))
            throw new BusinessException(409, "USER_NOT_ELIGIBLE", "Người dùng không còn đủ điều kiện.");
        if (assetId.HasValue && !await repo.AnyAsync(repo.Query<Asset>().Where(x => x.Id == assetId && !x.IsArchived && x.CurrentStatus != "RETIRED"), ct))
            throw new BusinessException(409, "ASSET_NOT_ELIGIBLE", "Tài sản không còn đủ điều kiện.");
        if (await repo.AnyAsync(repo.Query<LicenseAssignment>().Where(x => x.SoftwareLicenseId == license.Id && x.RevokedAtUtc == null && !x.IsArchived &&
            (userId.HasValue && x.AssignedUserId == userId || assetId.HasValue && x.AssignedAssetId == assetId)), ct))
            throw new BusinessException(409, "DUPLICATE_ASSIGNMENT", "Đối tượng đã được cấp bản quyền này.");
    }
    private Task<int> CountAsync(long id, CancellationToken ct) => repo.CountAsync(repo.Query<LicenseAssignment>().Where(x => x.SoftwareLicenseId == id && x.RevokedAtUtc == null && !x.IsArchived), ct);
    private async Task<LicenseAssignmentDto> ReadAsync(long id, CancellationToken ct) =>
        Map(await repo.FirstAsync(Rows(repo.Query<LicenseAssignment>().Where(x => x.Id == id)), ct) ?? throw Missing());

    public async Task<LicenseAssignmentDto> AssignAsync(AssignLicenseRequest request, CancellationToken ct)
    {
        Require(LicensePermissions.AssignmentAssign);
        if (request.SoftwareLicenseId <= 0) throw Validation.Invalid("softwareLicenseId", "Chọn bản quyền hợp lệ.");
        LicenseCapacityPolicy.ValidateTarget(request.UserId, request.AssetId);
        request.Notes = Validation.Optional(request.Notes, 1000, "notes");
        if (request.AssignedAt.HasValue) Time(request.AssignedAt.Value, null, "assignedAt");
        return await uow.RunAsync(async () => {
            await LockAsync(request.SoftwareLicenseId, request.AssetId, null, ct);
            await AuthorizeAsync(LicensePermissions.AssignmentAssign, ct);
            var license = await LicenseAsync(request.SoftwareLicenseId, ct); var now = clock.GetUtcNow().UtcDateTime;
            var at = request.AssignedAt ?? now; Time(at, license.StartsAtUtc, "assignedAt");
            LicenseCapacityPolicy.CheckCanAssign(license, await CountAsync(license.Id, ct), now);
            if (license.ExpiresAtUtc < at) throw Validation.Invalid("assignedAt", "Ngày cấp phải trong hiệu lực bản quyền.");
            await TargetAsync(license, request.UserId, request.AssetId, ct);
            var item = new LicenseAssignment { SoftwareLicenseId = license.Id, AssignedUserId = request.UserId, AssignedAssetId = request.AssetId,
                AssignedAtUtc = at, AssignedByUserId = actor.UserId!.Value, Notes = request.Notes, CreatedAtUtc = now };
            repo.Add(item); await repo.SaveAsync(ct); audit.Record("license-assignments.assign", item, actor.UserId, "USER");
            await repo.SaveAsync(ct); return await ReadAsync(item.Id, ct);
        }, ct);
    }
    public async Task<LicenseAssignmentDto> RevokeAsync(long id, RevokeLicenseAssignmentRequest request, CancellationToken ct)
    {
        Require(LicensePermissions.AssignmentRevoke); var expected = Validation.Version(request.RowVersion);
        var reason = Validation.Optional(request.Reason, 1000, "reason");
        var identity = await repo.FirstAsync(repo.Query<LicenseAssignment>().Where(x => x.Id == id), ct) ?? throw Missing();
        return await uow.RunAsync(async () => {
            await LockAsync(identity.SoftwareLicenseId, null, id, ct); await AuthorizeAsync(LicensePermissions.AssignmentRevoke, ct);
            var item = await repo.FirstAsync(repo.Query<LicenseAssignment>(true).Where(x => x.Id == id), ct) ?? throw Missing();
            CheckVersion(item, expected);
            if (item.RevokedAtUtc != null || item.IsArchived) throw new BusinessException(409, "ALREADY_REVOKED", "Bản quyền đã được thu hồi.");
            var at = request.RevokedAt ?? clock.GetUtcNow().UtcDateTime; Time(at, item.AssignedAtUtc, "revokedAt");
            item.Notes = Notes(item.Notes, reason, "Lý do thu hồi: "); item.RevokedAtUtc = at; item.RevokedByUserId = actor.UserId!.Value;
            await repo.SaveAsync(ct); audit.Record("license-assignments.revoke", item, actor.UserId, "USER", after: new { reasonProvided = reason != null });
            await repo.SaveAsync(ct); return await ReadAsync(id, ct);
        }, ct);
    }
    public async Task<TransferResultDto> TransferAsync(long id, TransferLicenseAssignmentRequest request, CancellationToken ct)
    {
        Require(LicensePermissions.AssignmentTransfer); var expected = Validation.Version(request.RowVersion);
        LicenseCapacityPolicy.ValidateTarget(request.UserId, request.AssetId); var reason = Validation.Optional(request.Reason, 1000, "reason");
        var identity = await repo.FirstAsync(repo.Query<LicenseAssignment>().Where(x => x.Id == id), ct) ?? throw Missing();
        return await uow.RunAsync(async () => {
            await LockAsync(identity.SoftwareLicenseId, request.AssetId, id, ct); await AuthorizeAsync(LicensePermissions.AssignmentTransfer, ct);
            var old = await repo.FirstAsync(repo.Query<LicenseAssignment>(true).Where(x => x.Id == id), ct) ?? throw Missing();
            CheckVersion(old, expected); var license = await LicenseAsync(old.SoftwareLicenseId, ct);
            var now = clock.GetUtcNow().UtcDateTime; var at = request.TransferredAt ?? now; Time(at, old.AssignedAtUtc, "transferredAt");
            LicenseCapacityPolicy.CheckCanTransfer(license, old, request.UserId, request.AssetId, now);
            await TargetAsync(license, request.UserId, request.AssetId, ct);
            // Persist the close before insert so PostgreSQL partial UNIQUE sees the released seat.
            old.Notes = Notes(old.Notes, reason, "Điều chuyển: "); old.RevokedAtUtc = at; old.RevokedByUserId = actor.UserId!.Value;
            await repo.SaveAsync(ct); audit.Record("license-assignments.revoke", old, actor.UserId, "USER", after: new { reasonProvided = reason != null });
            LicenseCapacityPolicy.CheckCanAssign(license, await CountAsync(license.Id, ct), now);
            var item = new LicenseAssignment { SoftwareLicenseId = license.Id, AssignedUserId = request.UserId, AssignedAssetId = request.AssetId,
                AssignedAtUtc = at, AssignedByUserId = actor.UserId!.Value, Notes = reason, CreatedAtUtc = now };
            repo.Add(item); await repo.SaveAsync(ct); audit.Record("license-assignments.assign", item, actor.UserId, "USER"); await repo.SaveAsync(ct);
            return new TransferResultDto { Previous = await ReadAsync(old.Id, ct), Current = await ReadAsync(item.Id, ct) };
        }, ct);
    }
}
