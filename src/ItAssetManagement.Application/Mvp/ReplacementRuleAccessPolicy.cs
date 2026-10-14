namespace ItAssetManagement.Application.Mvp;

// Prepared rule configuration boundary, not an enabled endpoint. Caller supplies
// server-refreshed CurrentUser and rechecks under the coordinator lock on writes.
public sealed class ReplacementRuleAccessPolicy
{
    private readonly bool admin;
    private readonly HashSet<string> grants;
    public ReplacementRuleAccessPolicy(CurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0) throw Forbidden();
        admin = user.Roles.Contains("ADMIN_IT", StringComparer.Ordinal);
        grants = new(user.Permissions, StringComparer.Ordinal);
    }
    public bool CanRead => grants.Contains(Permissions.ReplacementRuleRead);
    public bool CanCreate => admin && CanRead && grants.Contains(Permissions.ReplacementRuleCreate);
    public bool CanUpdate => admin && CanRead && grants.Contains(Permissions.ReplacementRuleUpdate);
    public void RequireRead() { if (!CanRead) throw Forbidden(); }
    public void RequireCreate() { if (!CanCreate) throw Forbidden(); }
    // Update includes publishing/deactivation, never bypassing version/history rules.
    public void RequireUpdate() { if (!CanUpdate) throw Forbidden(); }
    private static BusinessException Forbidden() => new(403, "FORBIDDEN", "Không có quyền quản lý quy tắc thay thế.");
}
