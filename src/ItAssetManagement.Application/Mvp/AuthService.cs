using System.Net.Mail;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class AuthService(IRepository repo, IUnitOfWork uow, IPasswordService passwords, IAuditWriter audit, ITokenIssuer issuer)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Validation.Required(request.Email, 320, "email");
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email) throw Validation.Invalid("email", "Email không hợp lệ.");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 256) throw Validation.Invalid("password", "Mật khẩu bắt buộc, tối đa 256 ký tự.");
        var normalized = email.ToUpperInvariant();
        // Serialize attempts for this account; never include submitted credentials in logs/audit.
        var result = await uow.RunAsync(async () =>
        {
            await repo.LockAsync("login:" + normalized, ct);
            var user = await repo.FirstAsync(repo.Query<User>(true).Where(x => x.NormalizedEmail == normalized), ct);
            var now = DateTime.UtcNow;
            if (user == null)
            {
                passwords.DummyVerify(request.Password);
                audit.Record("auth.login", null, null, "ANONYMOUS", "FAILURE", failureCode: "INVALID_CREDENTIALS");
                await repo.SaveAsync(ct);
                return (Current: (CurrentUser?)null, Version: 0);
            }
            var validPassword = passwords.Verify(user, request.Password);
            var current = await CurrentAsync(user, ct);
            if (!validPassword || !user.IsActive || user.IsAdminLocked || user.LockoutEndUtc > now || current.Roles.Length == 0)
            {
                if (user.IsActive && !user.IsAdminLocked && !(user.LockoutEndUtc > now) && !validPassword)
                {
                    if (user.LockoutEndUtc != null) user.FailedLoginCount = 0;
                    user.FailedLoginCount++;
                    user.LockoutEndUtc = user.FailedLoginCount >= 5 ? now.AddMinutes(15) : null;
                }
                audit.Record("auth.login", user, null, "ANONYMOUS", "FAILURE", failureCode: "INVALID_CREDENTIALS");
                await repo.SaveAsync(ct);
                return (Current: (CurrentUser?)null, Version: 0);
            }
            user.FailedLoginCount = 0; user.LockoutEndUtc = null; user.LastLoginAtUtc = now;
            audit.Record("auth.login", user, user.Id, "USER");
            await repo.SaveAsync(ct);
            return (Current: (CurrentUser?)current, Version: user.TokenVersion);
        }, ct);
        if (result.Current == null) throw new BusinessException(401, "INVALID_CREDENTIALS", "Email hoặc mật khẩu không hợp lệ.");
        return issuer.Issue(result.Current, result.Version);
    }
    public async Task<CurrentUser?> ValidateAsync(long id, int version, CancellationToken ct)
    {
        var user = await repo.FirstAsync(repo.Query<User>().Where(x => x.Id == id), ct);
        if (user == null || !user.IsActive || user.IsAdminLocked || user.LockoutEndUtc > DateTime.UtcNow || user.TokenVersion != version) return null;
        var current = await CurrentAsync(user, ct);
        return current.Roles.Length == 0 ? null : current;
    }
    private async Task<CurrentUser> CurrentAsync(User user, CancellationToken ct)
    {
        var roleQuery = from ur in repo.Query<UserRole>() join role in repo.Query<Role>() on ur.RoleId equals role.Id
                        where ur.UserId == user.Id && role.IsActive && Permissions.Roles.Contains(role.Code) select role;
        var roles = await repo.ListAsync(roleQuery.Select(x => x.Code).Distinct().OrderBy(x => x), ct);
        var permissionQuery = from role in roleQuery join rp in repo.Query<RolePermission>() on role.Id equals rp.RoleId
                              join p in repo.Query<Permission>() on rp.PermissionId equals p.Id
                              where p.IsActive && Permissions.All.Contains(p.Code) select p.Code;
        var permissions = await repo.ListAsync(permissionQuery.Distinct().OrderBy(x => x), ct);
        return new(user.Id, user.FullName, user.Email, user.DepartmentId, roles.ToArray(), permissions.ToArray());
    }
}
