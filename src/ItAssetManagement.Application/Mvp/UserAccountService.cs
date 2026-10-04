using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public interface IAccountPersistence
{
    void RemoveMembership(UserRole membership);
    Task<string[]> AllocationWarningsAsync(CancellationToken ct);
}
public sealed class ReplaceUserRolesRequest
{
    [JsonRequired] public long[] RoleIds { get; set; } = [];
    [Required] public string RowVersion { get; set; } = "";
}
// The four profile endpoints keep their existing DTO. These two privileged operations
// additionally report the resulting lock/membership state and allocation-review warning.
public sealed record AccountChangeResult(UserDto User, bool IsAdminLocked, long[] RoleIds, string[] Warnings);

public static class UserAccountValidation
{
    public static byte[] Status(StatusRequest request)
    {
        if (request.Status is not ("Active" or "Inactive" or "Locked" or "Unlocked"))
            throw Validation.Invalid("status", "Active, Inactive, Locked hoặc Unlocked.");
        Validation.Required(request.Reason, 1000, "reason");
        return Validation.Version(request.RowVersion);
    }
    public static byte[] Roles(ReplaceUserRolesRequest request)
    {
        if (request.RoleIds == null || request.RoleIds.Length > Permissions.Roles.Length ||
            request.RoleIds.Any(x => x <= 0) || request.RoleIds.Distinct().Count() != request.RoleIds.Length)
            throw Validation.Invalid("roleIds", "Danh sách tối đa 3 ID dương, không trùng; [] để bỏ mọi role.");
        request.RoleIds = request.RoleIds.Order().ToArray();
        return Validation.Version(request.RowVersion);
    }
    public static void IncrementVersion(User user)
    {
        if (user.TokenVersion == int.MaxValue)
            throw new BusinessException(409, "USER_TOKEN_VERSION_EXHAUSTED", "Không thể thay đổi trạng thái token; cần kiểm tra tài khoản.");
        user.TokenVersion++;
    }
}

public sealed class UserAccountService(IRepository repo, IAccountPersistence accounts, IUnitOfWork uow, IAuditWriter audit, IActor actor)
{
    private void Require(string permission, long id)
    {
        if (actor.UserId is not > 0 || !actor.Has(permission)) throw new BusinessException(403, "FORBIDDEN", "Không có quyền quản lý tài khoản.");
        if (id <= 0) throw Validation.Invalid("userId", "ID phải > 0.");
    }
    private async Task<User> BeginAsync(long id, string permission, byte[] version, CancellationToken ct)
    {
        // Same shared identity lock as profile writes; one global lock serializes last-Admin checks.
        await repo.LockAsync("users-identity", ct);
        var caller = await repo.FirstAsync(repo.Query<User>().Where(x => x.Id == actor.UserId), ct);
        if (caller == null || !caller.IsActive || caller.IsAdminLocked || caller.LockoutEndUtc > DateTime.UtcNow ||
            actor.TokenVersion == null || caller.TokenVersion != actor.TokenVersion)
            throw new BusinessException(401, "UNAUTHORIZED", "Yêu cầu đăng nhập hợp lệ.");
        var allowed = from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
                      join grant in repo.Query<RolePermission>() on role.Id equals grant.RoleId
                      join definition in repo.Query<Permission>() on grant.PermissionId equals definition.Id
                      where link.UserId == caller.Id && role.IsActive && Permissions.Roles.Contains(role.Code) &&
                          definition.IsActive && definition.Code == permission select definition.Id;
        if (!await repo.AnyAsync(allowed, ct)) throw new BusinessException(403, "FORBIDDEN", "Quyền đã thay đổi; tải lại phiên làm việc.");
        var user = await repo.FirstAsync(repo.Query<User>(true).Where(x => x.Id == id), ct)
            ?? throw new BusinessException(404, "USER_NOT_FOUND", "Không tìm thấy người dùng.");
        // Reject stale state before checking business rules or touching role links.
        if (!user.RowVersion.SequenceEqual(version)) throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi; tải lại trước khi lưu.");
        repo.ExpectVersion(user, version);
        return user;
    }
    private IQueryable<long> AdminUsers() =>
        from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
        where role.IsActive && role.Code == "ADMIN_IT" select link.UserId;
    private async Task ProtectLastAdminAsync(User user, bool becomesUnavailable, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!becomesUnavailable || !user.IsActive || user.IsAdminLocked || user.LockoutEndUtc > now ||
            !await repo.AnyAsync(AdminUsers().Where(x => x == user.Id), ct)) return;
        var admins = AdminUsers();
        var other = repo.Query<User>().Where(x => x.Id != user.Id && x.IsActive && !x.IsAdminLocked &&
            (x.LockoutEndUtc == null || x.LockoutEndUtc <= now) && admins.Contains(x.Id));
        if (!await repo.AnyAsync(other, ct))
            throw new BusinessException(409, "LAST_ADMIN_PROTECTED", "Không được làm mất Admin đang có thể đăng nhập cuối cùng.");
    }
    public Task<AccountChangeResult> StatusAsync(long id, StatusRequest request, CancellationToken ct)
    {
        Require(Permissions.UserStatus, id); var version = UserAccountValidation.Status(request);
        return uow.RunAsync(async () =>
        {
            var user = await BeginAsync(id, Permissions.UserStatus, version, ct);
            await ProtectLastAdminAsync(user, request.Status is "Inactive" or "Locked", ct);
            var before = new { user.IsActive, user.IsAdminLocked };
            switch (request.Status)
            {
                case "Active": user.IsActive = true; break;
                case "Inactive": user.IsActive = false; break;
                case "Locked":
                    if (!user.IsAdminLocked) user.AdminLockedAtUtc = DateTime.UtcNow;
                    user.IsAdminLocked = true; break;
                case "Unlocked": user.IsAdminLocked = false; user.AdminLockedAtUtc = null; break;
            }
            UserAccountValidation.IncrementVersion(user); user.UpdatedByUserId = actor.UserId;
            var warnings = request.Status == "Inactive" ? await accounts.AllocationWarningsAsync(ct) : [];
            audit.Record("users.status.change", user, actor.UserId, "USER", before: before,
                after: new { user.IsActive, user.IsAdminLocked, reasonProvided = true });
            await repo.SaveAsync(ct);
            return await ResultAsync(user, warnings, ct);
        }, ct);
    }
    public Task<AccountChangeResult> RolesAsync(long id, ReplaceUserRolesRequest request, CancellationToken ct)
    {
        Require(Permissions.RoleAssign, id); var version = UserAccountValidation.Roles(request);
        return uow.RunAsync(async () =>
        {
            var user = await BeginAsync(id, Permissions.RoleAssign, version, ct);
            var selected = await repo.ListAsync(repo.Query<Role>().Where(x => request.RoleIds.Contains(x.Id) &&
                x.IsActive && Permissions.Roles.Contains(x.Code)), ct);
            if (selected.Count != request.RoleIds.Length) throw Validation.Invalid("roleIds", "Role phải tồn tại, active và thuộc bộ ba cố định.");
            await ProtectLastAdminAsync(user, !selected.Any(x => x.Code == "ADMIN_IT"), ct);
            var current = await repo.ListAsync(repo.Query<UserRole>(true).Where(x => x.UserId == id), ct);
            var existingIds = current.Select(x => x.RoleId).ToHashSet();
            foreach (var link in current.Where(x => !request.RoleIds.Contains(x.RoleId)))
            {
                audit.Record("users.roles.remove", link, actor.UserId, "USER", before: new { link.UserId, link.RoleId });
                accounts.RemoveMembership(link);
            }
            foreach (var roleId in request.RoleIds.Where(x => !existingIds.Contains(x)))
            {
                var link = new UserRole { UserId = id, RoleId = roleId, AssignedByUserId = actor.UserId, AssignedAtUtc = DateTime.UtcNow };
                repo.Add(link); await repo.SaveAsync(ct);
                audit.Record("users.roles.add", link, actor.UserId, "USER", after: new { link.UserId, link.RoleId });
            }
            UserAccountValidation.IncrementVersion(user); user.UpdatedByUserId = actor.UserId;
            audit.Record("users.roles.replace", user, actor.UserId, "USER", before: new { roleCount = current.Count },
                after: new { roleCount = request.RoleIds.Length, adminRoleAssigned = selected.Any(x => x.Code == "ADMIN_IT") });
            await repo.SaveAsync(ct);
            return await ResultAsync(user, [], ct);
        }, ct);
    }
    private async Task<AccountChangeResult> ResultAsync(User user, string[] warnings, CancellationToken ct)
    {
        var roleIds = await repo.ListAsync(repo.Query<UserRole>().Where(x => x.UserId == user.Id).Select(x => x.RoleId).OrderBy(x => x), ct);
        return new(new UserDto
        {
            Id = user.Id, Username = user.Username, Email = user.Email, DisplayName = user.FullName, EmployeeCode = user.EmployeeCode,
            DepartmentId = user.DepartmentId, Phone = user.Phone, IsActive = user.IsActive, CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc, RowVersion = Convert.ToBase64String(user.RowVersion)
        }, user.IsAdminLocked, roleIds.ToArray(), warnings);
    }
}
