using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Run inside the business transaction, after the asset/master locks. HTTP actors
// always carry a validated token version. Tokenless actors are trusted in-process
// test/system callers; they still pass each service's explicit permission guards.
internal static class WorkflowAuthorization
{
    public static async Task RequireAsync(IRepository repo, IActor actor, CancellationToken ct, params string[] permissions)
    {
        if (actor.TokenVersion is not {} version) return;
        await repo.LockAsync("users-identity", ct);
        var user = await repo.FirstAsync(repo.Query<User>().Where(x => x.Id == actor.UserId), ct);
        if (user == null || !user.IsActive || !user.IsEmailConfirmed || user.IsAdminLocked ||
            user.LockoutEndUtc > DateTime.UtcNow || user.TokenVersion != version)
            throw new BusinessException(401, "UNAUTHORIZED", "Phiên đã thay đổi. Vui lòng đăng nhập lại.");
        var effective = await repo.ListAsync(EffectivePermissionQuery.ForUser(repo, user.Id), ct);
        // A response may contain costs based on the request's scope snapshot. If
        // that financial grant disappeared while waiting, deny rather than leak it.
        var checks = permissions.AsEnumerable();
        if (permissions.Any(x => x.StartsWith("maintenance.", StringComparison.Ordinal)) && actor.Has(Permissions.MaintenanceCostWrite))
            checks = checks.Append(Permissions.MaintenanceCostWrite);
        if (checks.Any(code => !effective.Contains(code)))
            throw new BusinessException(403, "FORBIDDEN", "Quyền đã thay đổi. Vui lòng tải lại phiên làm việc.");
    }
}
