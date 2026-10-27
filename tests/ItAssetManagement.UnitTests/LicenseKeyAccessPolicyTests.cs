using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseKeyAccessPolicyTests
{
    private static CurrentUser User(string role, params string[] grants) => new(7, "Fixture", "fixture@example.test", null, [role], grants);
    private static readonly string[] All = [Permissions.LicenseRead, Permissions.LicenseKeyManage, Permissions.LicenseKeyReveal];
    private static SoftwareLicense Encrypted() => new()
    { Id = 1, LicenseKeyCiphertext = [1, 2, 3], LicenseKeyLast4 = "ABCD", KeyVersion = "v1" };
    private static void Denied(Action work) => Assert.Equal(403, Assert.Throws<BusinessException>(work).Status);

    [Theory]
    [InlineData("SYSTEM_MANAGER")][InlineData("TECHNICAL_SUPPORT")][InlineData("CUSTOM_TEAM")]
    public void Misgranted_permissions_do_not_replace_required_admin_membership(string role)
    {
        var policy = new LicenseKeyAccessPolicy(User(role, All)); Assert.False(policy.CanManage); Assert.False(policy.CanReveal);
        using var input = JsonDocument.Parse("{\"licenseKey\":null}"); Denied(() => policy.RequireKeyInput(input.RootElement));
        Denied(() => policy.RequireReveal("fixture reason")); Assert.Equal("****-ABCD", policy.MaskedKey(Encrypted()));
    }

    [Theory]
    [InlineData("{\"licenseKey\":null}")][InlineData("{\"licenseKey\":\"\"}")]
    [InlineData("{\"LicenseKey\":\"synthetic\"}")][InlineData("{\"key\":null}")]
    public void Key_presence_is_checked_before_nullable_dto_binding(string json)
    {
        using var body = JsonDocument.Parse(json);
        Denied(() => new LicenseKeyAccessPolicy(User("SYSTEM_MANAGER", Permissions.LicenseRead)).RequireKeyInput(body.RootElement));
        new LicenseKeyAccessPolicy(User("ADMIN_IT", All)).RequireKeyInput(body.RootElement);
    }

    [Theory]
    [InlineData("licenseKeyCiphertext")][InlineData("licenseKeyLast4")][InlineData("keyVersion")]
    public void Even_admin_cannot_supply_internal_persistence_fields(string name)
    {
        using var input = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?> { [name] = null }));
        Assert.Equal(400, Assert.Throws<BusinessException>(() => new LicenseKeyAccessPolicy(User("ADMIN_IT", All)).RequireKeyInput(input.RootElement)).Status);
    }

    [Fact]
    public void Read_manage_and_reveal_are_independent_grants_and_revocation_takes_effect()
    {
        var reader = new LicenseKeyAccessPolicy(User("ADMIN_IT", Permissions.LicenseRead));
        Assert.False(reader.CanManage); Assert.False(reader.CanReveal);
        var manager = new LicenseKeyAccessPolicy(User("ADMIN_IT", Permissions.LicenseRead, Permissions.LicenseKeyManage));
        Assert.True(manager.CanManage); Denied(() => manager.RequireReveal("fixture reason"));
        var reveal = new LicenseKeyAccessPolicy(User("ADMIN_IT", Permissions.LicenseRead, Permissions.LicenseKeyReveal));
        Assert.False(reveal.CanManage); Assert.Equal("fixture reason", reveal.RequireReveal("  fixture reason  "));
        Denied(() => new LicenseKeyAccessPolicy(User("ADMIN_IT", Permissions.LicenseKeyReveal)).RequireReveal("fixture"));
        Assert.Contains(Permissions.LicenseRead, Permissions.All);
        Assert.Contains(Permissions.LicenseKeyReveal, Permissions.Admin);
        Assert.DoesNotContain(Permissions.LicenseKeyManage, Permissions.Operate);
    }

    [Fact]
    public void Metadata_without_key_is_allowed_and_masking_never_calls_decryption()
    {
        var policy = new LicenseKeyAccessPolicy(User("SYSTEM_MANAGER", Permissions.LicenseRead));
        using var body = JsonDocument.Parse("{\"vendor\":\"fixture\"}"); policy.RequireKeyInput(body.RootElement);
        Assert.Null(policy.MaskedKey(new SoftwareLicense())); Assert.Equal("****-ABCD", policy.MaskedKey(Encrypted()));
        var malformed = Encrypted(); malformed.KeyVersion = null;
        Assert.Equal(500, Assert.Throws<BusinessException>(() => policy.MaskedKey(malformed)).Status);
        Denied(() => new LicenseKeyAccessPolicy(User("TECHNICAL_SUPPORT")).MaskedKey(Encrypted()));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public void Reveal_requires_reason_after_authorization(string? reason) =>
        Assert.Equal(400, Assert.Throws<BusinessException>(() => new LicenseKeyAccessPolicy(User("ADMIN_IT", All)).RequireReveal(reason)).Status);
}
