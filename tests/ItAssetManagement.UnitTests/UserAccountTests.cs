using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class UserAccountTests
{
    private static string Version => Convert.ToBase64String(new byte[16]);
    [Theory]
    [InlineData("Active")] [InlineData("Inactive")] [InlineData("Locked")] [InlineData("Unlocked")]
    public void Four_explicit_status_commands_are_supported(string status) =>
        Assert.Equal(16, UserAccountValidation.Status(new(status, "Administrative test reason", Version)).Length);
    [Theory]
    [InlineData("active")] [InlineData("Disable")] [InlineData("LockedOut")] [InlineData("")]
    public void Unknown_or_automatic_lockout_status_is_rejected(string status) =>
        Assert.Equal(400, Assert.Throws<BusinessException>(() => UserAccountValidation.Status(new(status, "reason", Version))).Status);
    [Fact]
    public void Reason_and_version_are_required_but_request_is_not_logged()
    {
        foreach (var reason in new[] { "", "   ", new string('x', 1001) })
            Assert.Throws<BusinessException>(() => UserAccountValidation.Status(new("Locked", reason, Version)));
        Assert.Throws<BusinessException>(() => UserAccountValidation.Status(new("Locked", "reason", "invalid")));
    }
    [Fact]
    public void Roles_require_unique_positive_bounded_ids_and_allow_explicit_empty_replacement()
    {
        foreach (var ids in new long[][] { null!, [0], [-1], [1, 1], [1, 2, 3, 4] })
            Assert.Throws<BusinessException>(() => UserAccountValidation.Roles(new() { RoleIds = ids, RowVersion = Version }));
        var request = new ReplaceUserRolesRequest { RoleIds = [3, 1], RowVersion = Version };
        UserAccountValidation.Roles(request); Assert.Equal([1L, 3L], request.RoleIds);
        Assert.Equal(16, UserAccountValidation.Roles(new() { RoleIds = [], RowVersion = Version }).Length);
        Assert.Throws<BusinessException>(() => UserAccountValidation.Roles(new() { RoleIds = [], RowVersion = "bad" }));
    }
    [Fact]
    public void Token_version_overflow_is_safe_and_admin_permissions_are_not_read_or_operate_grants()
    {
        var user = new User { TokenVersion = int.MaxValue };
        var error = Assert.Throws<BusinessException>(() => UserAccountValidation.IncrementVersion(user));
        Assert.Equal(409, error.Status); Assert.Equal("USER_TOKEN_VERSION_EXHAUSTED", error.Code); Assert.Equal(int.MaxValue, user.TokenVersion);
        user.TokenVersion = 0; UserAccountValidation.IncrementVersion(user); Assert.Equal(1, user.TokenVersion);
        foreach (var permission in new[] { Permissions.UserStatus, Permissions.RoleAssign })
        { Assert.Contains(permission, Permissions.Admin); Assert.DoesNotContain(permission, Permissions.Read); Assert.DoesNotContain(permission, Permissions.Operate); }
    }
    [Fact]
    public async Task Service_checks_permission_before_any_persistence()
    {
        var service = new UserAccountService(null!, null!, null!, null!, new DeniedActor());
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.StatusAsync(1, new("Locked", "reason", Version), default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.RolesAsync(1, new(), default))).Status);
    }
    private sealed class DeniedActor : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId => Guid.Empty;
        public string? Method => null;
        public string? Path => null;
        public bool Has(string permission) => false;
    }
}
