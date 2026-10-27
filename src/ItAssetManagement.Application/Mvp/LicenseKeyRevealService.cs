using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class LicenseKeyRevealResponse(long licenseId, string licenseKey, DateTime revealedAt)
{
    public long LicenseId { get; } = licenseId;
    public string LicenseKey { get; } = licenseKey;
    public DateTime RevealedAt { get; } = revealedAt;
    public override string ToString() => "[license key reveal response]";
}

// Explicit, audited sensitive read. Prepared for the owner's reveal endpoint;
// not registered in DI and not reachable from metadata/list/export paths.
public sealed class LicenseKeyRevealService(IRepository repo, IUnitOfWork uow,
    IAuditWriter audit, IActor actor, ILicenseKeyProtector protector)
{
    public async Task<LicenseKeyRevealResponse> RevealAsync(long licenseId, string? reason, CancellationToken ct)
    {
        if (actor.UserId is not > 0 || actor.TokenVersion is not >= 0 || actor.CorrelationId == Guid.Empty ||
            !actor.Has(Permissions.LicenseRead) || !actor.Has(Permissions.LicenseKeyReveal)) throw Forbidden();
        if (licenseId <= 0) throw Validation.Invalid("licenseId", "ID phải > 0.");
        return await uow.RunAsync(async () =>
        {
            // Same coordinator lock as account/role/grant writes. Recheck DB grants
            // after acquiring it: permissions refreshed earlier in the HTTP request
            // alone cannot protect against concurrent grant revocation.
            await repo.LockAsync("users-identity", ct);
            var now = DateTime.UtcNow;
            var user = await repo.FirstAsync(repo.Query<User>().Where(u => u.Id == actor.UserId), ct);
            if (user is null || !user.IsActive || !user.IsEmailConfirmed || user.IsAdminLocked ||
                user.LockoutEndUtc > now || user.TokenVersion != actor.TokenVersion) throw Forbidden();
            var roles = from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
                        where link.UserId == user.Id && role.IsActive select role;
            if (!await repo.AnyAsync(roles.Where(role => role.Code == "ADMIN_IT"), ct)) throw Forbidden();
            var grants = EffectivePermissionQuery.ForUser(repo, user.Id).Where(code =>
                code == Permissions.LicenseRead || code == Permissions.LicenseKeyReveal);
            if (await repo.CountAsync(grants.Distinct(), ct) != 2) throw Forbidden();
            Validation.Required(reason, 1000, "reason");
            var license = await repo.FirstAsync(repo.Query<SoftwareLicense>().Where(l => l.Id == licenseId), ct) ??
                throw new BusinessException(404, "LICENSE_NOT_FOUND", "Không tìm thấy license.");
            if (license.LicenseKeyCiphertext is null && license.LicenseKeyLast4 is null && license.KeyVersion is null)
                throw new BusinessException(404, "LICENSE_KEY_NOT_FOUND", "License chưa có key.");
            if (license.LicenseKeyCiphertext is null || license.LicenseKeyLast4 is null || license.KeyVersion is null)
                throw new BusinessException(500, "DATA_INCONSISTENT", "Dữ liệu license key không nhất quán.");
            var plaintext = protector.Reveal(license.Id,
                new(license.LicenseKeyCiphertext, license.LicenseKeyLast4, license.KeyVersion));
            audit.Record("license.key.reveal", license, actor.UserId, "USER", after: new
            { licenseId = license.Id, keyVersion = license.KeyVersion, keyRevealed = true, reasonProvided = true });
            await repo.SaveAsync(ct);
            // AuditedUnitOfWork must commit successfully before the caller receives
            // this object. Audit/commit failure never returns plaintext to the API.
            return new LicenseKeyRevealResponse(license.Id, plaintext, now);
        }, ct);
    }
    private static BusinessException Forbidden() => new(403, "FORBIDDEN", "Không có quyền reveal license key.");
}
