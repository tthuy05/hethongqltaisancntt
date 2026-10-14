using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.UnitTests;

public sealed class ReplacementRuleAccessPolicyTests
{
    private static readonly string[] Grants = [Permissions.ReplacementRuleRead, Permissions.ReplacementRuleCreate, Permissions.ReplacementRuleUpdate];
    private static CurrentUser User(string role, string[] grants) => new(7, "Fixture", "fixture@example.test", null, [role], grants);
    private static void Denied(Action work) => Assert.Equal(403, Assert.Throws<BusinessException>(work).Status);

    [Fact]
    public void Admin_with_explicit_grants_can_configure_but_role_alone_cannot()
    {
        var allowed = new ReplacementRuleAccessPolicy(User("ADMIN_IT", Grants));
        allowed.RequireRead(); allowed.RequireCreate(); allowed.RequireUpdate();
        var denied = new ReplacementRuleAccessPolicy(User("ADMIN_IT", []));
        Assert.False(denied.CanRead); Denied(denied.RequireCreate); Denied(denied.RequireUpdate);
    }
    [Theory]
    [InlineData("SYSTEM_MANAGER")][InlineData("TECHNICAL_SUPPORT")][InlineData("CUSTOM_GROUP")]
    public void Non_admin_cannot_configure_even_if_mutation_grants_are_misassigned(string role)
    {
        var policy = new ReplacementRuleAccessPolicy(User(role, Grants));
        policy.RequireRead(); Assert.False(policy.CanCreate); Assert.False(policy.CanUpdate);
        Denied(policy.RequireCreate); Denied(policy.RequireUpdate);
    }
    [Fact]
    public void Read_dependency_and_distinct_create_update_grants_are_enforced()
    {
        var missingRead = new ReplacementRuleAccessPolicy(User("ADMIN_IT", [Permissions.ReplacementRuleCreate, Permissions.ReplacementRuleUpdate]));
        Denied(missingRead.RequireRead); Denied(missingRead.RequireCreate); Denied(missingRead.RequireUpdate);
        var createOnly = new ReplacementRuleAccessPolicy(User("ADMIN_IT", [Permissions.ReplacementRuleRead, Permissions.ReplacementRuleCreate]));
        createOnly.RequireCreate(); Denied(createOnly.RequireUpdate);
        var updateOnly = new ReplacementRuleAccessPolicy(User("ADMIN_IT", [Permissions.ReplacementRuleRead, Permissions.ReplacementRuleUpdate]));
        updateOnly.RequireUpdate(); Denied(updateOnly.RequireCreate);
    }
    [Fact]
    public void Invalid_identity_and_case_variant_role_fail_closed()
    {
        Denied(() => new ReplacementRuleAccessPolicy(new(0, "Fixture", "fixture@example.test", null, ["ADMIN_IT"], Grants)));
        var wrongCase = new ReplacementRuleAccessPolicy(User("admin_it", Grants));
        Denied(wrongCase.RequireCreate); Denied(wrongCase.RequireUpdate);
    }
    [Fact]
    public void Prepared_permissions_are_not_registered_in_active_catalog()
    {
        foreach (var grant in Grants) Assert.DoesNotContain(grant, Permissions.All);
    }
}
