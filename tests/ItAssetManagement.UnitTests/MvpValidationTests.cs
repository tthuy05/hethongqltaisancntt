using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Mvp;

namespace ItAssetManagement.UnitTests;

public sealed class MvpValidationTests
{
    private static AssetRequest Valid() => new() { AssetCode = " A-01 ", Name = " Laptop ", AssetTypeId = 1, OwningDepartmentId = 1 };
    [Fact] public void Asset_normalization_preserves_null_cost_and_empty_serial_becomes_null()
    { var x = Valid(); x.SerialNumber = "  "; Validation.Asset(x, false); Assert.Equal("A-01", x.AssetCode); Assert.Null(x.SerialNumber); Assert.Null(x.PurchasePrice); }
    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void Invalid_cost_is_rejected(string price)
    { var x = Valid(); x.PurchasePrice = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture); Assert.Throws<BusinessException>(() => Validation.Asset(x, false)); }
    [Fact] public void Warranty_cannot_precede_purchase()
    { var x = Valid(); x.PurchaseDate = new(2026, 10, 2); x.WarrantyExpirationDate = new(2026, 10, 1); Assert.Throws<BusinessException>(() => Validation.Asset(x, false)); }
    [Theory]
    [InlineData(0, 20)] [InlineData(1, 101)] [InlineData(1, 0)]
    public void Invalid_pagination_is_rejected_not_clamped(int page, int size)
    { Assert.Throws<BusinessException>(() => Validation.List(new() { Page = page, PageSize = size }, true)); }
    [Theory]
    [InlineData(null)] [InlineData("*")] [InlineData("W/\"YQ==\"")] [InlineData("\"YQ==\"")]
    public void Missing_or_invalid_archive_precondition_is_rejected(string? header)
    { Assert.Throws<BusinessException>(() => Validation.IfMatch(header)); }
    [Fact] public void Sixteen_byte_version_is_roundtripped_and_status_mapping_is_exact()
    { var v = Convert.ToBase64String(new byte[16]); Assert.Equal(new byte[16], Validation.IfMatch('"' + v + '"')); Assert.Equal("InStock", StatusMap.ToApi("IN_STOCK")); }
    [Fact] public void Password_hasher_uses_salted_adaptive_hashes()
    {
        var passwords = new PasswordService(); var user = new User();
        var first = passwords.Hash(user, "Fixture-password-2026!"); var second = passwords.Hash(user, "Fixture-password-2026!");
        Assert.NotEqual(first, second); user.PasswordHash = first;
        Assert.True(passwords.Verify(user, "Fixture-password-2026!")); Assert.False(passwords.Verify(user, "wrong"));
        Assert.DoesNotContain("Fixture-password", first);
    }
}
