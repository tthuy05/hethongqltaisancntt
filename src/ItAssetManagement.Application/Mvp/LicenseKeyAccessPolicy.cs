using System.Text;
using System.Text.Json;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Construct from server-refreshed CurrentUser within one request. This is not a
// metadata CRUD permission or an enabled reveal endpoint.
public sealed class LicenseKeyAccessPolicy
{
    private readonly HashSet<string> permissions;
    private readonly bool admin;
    public LicenseKeyAccessPolicy(CurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0) throw Forbidden();
        permissions = new(user.Permissions, StringComparer.Ordinal);
        admin = user.Roles.Contains("ADMIN_IT", StringComparer.Ordinal);
    }
    public bool CanManage => admin && permissions.Contains(Permissions.LicenseRead) && permissions.Contains(Permissions.LicenseKeyManage);
    public bool CanReveal => admin && permissions.Contains(Permissions.LicenseRead) && permissions.Contains(Permissions.LicenseKeyReveal);

    public void RequireKeyInput(JsonElement body)
    {
        RequireRead();
        if (body.ValueKind != JsonValueKind.Object) throw Validation.Invalid("body", "Request phải là JSON object.");
        var names = body.EnumerateObject().Select(field => field.Name).ToArray();
        if (names.Any(name => name.Equals("licenseKeyCiphertext", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("licenseKeyLast4", StringComparison.OrdinalIgnoreCase) || name.Equals("keyVersion", StringComparison.OrdinalIgnoreCase)))
            throw Validation.Invalid("licenseKey", "Encrypted fields chỉ do server quản lý.");
        if (names.Any(name => name.Equals("licenseKey", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("key", StringComparison.OrdinalIgnoreCase)) && !CanManage) throw Forbidden();
        // Presence includes explicit null/empty; the service separately decides clear
        // semantics and validates the plaintext before invoking the protector.
    }

    public string RequireReveal(string? reason)
    {
        if (!CanReveal) throw Forbidden();
        return Validation.Required(reason, 1000, "reason");
    }

    public string? MaskedKey(SoftwareLicense license)
    {
        RequireRead(); ArgumentNullException.ThrowIfNull(license);
        if (license.LicenseKeyCiphertext is null && license.LicenseKeyLast4 is null && license.KeyVersion is null) return null;
        if (license.LicenseKeyCiphertext is not { Length: > 0 } || string.IsNullOrEmpty(license.KeyVersion) ||
            license.LicenseKeyLast4 is null || license.LicenseKeyLast4.EnumerateRunes().Count() != 4 ||
            license.LicenseKeyLast4.EnumerateRunes().Any(Rune.IsControl))
            throw new BusinessException(500, "DATA_INCONSISTENT", "Dữ liệu license key không nhất quán.");
        return "****-" + license.LicenseKeyLast4;
    }
    private void RequireRead() { if (!permissions.Contains(Permissions.LicenseRead)) throw Forbidden(); }
    private static BusinessException Forbidden() => new(403, "FORBIDDEN", "Không có quyền truy cập license key.");
}
