using System.Text.Json;
using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.UnitTests;

public sealed class AssetCostRequestTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData("{}", false)]
    [InlineData("{\"purchasePrice\":null}", true)]
    [InlineData("{\"PurchasePrice\":12.25}", true)]
    [InlineData("{\"hasPurchasePrice\":true}", false)]
    [InlineData("{\"purchasePrice\":null,\"hasPurchasePrice\":false}", true)]
    public void Presence_tracks_the_price_property_not_an_untrusted_flag(string body, bool expected)
    {
        var request = JsonSerializer.Deserialize<AssetRequest>(body, Json)!;
        Assert.Equal(expected, request.HasPurchasePrice);
    }
    [Fact]
    public void Direct_commands_track_explicit_null_and_internal_state_is_not_serialized()
    {
        var request = new AssetRequest(); Assert.False(request.HasPurchasePrice);
        request.PurchasePrice = null; Assert.True(request.HasPurchasePrice);
        request.PurchasePrice = 12.25m;
        var body = JsonSerializer.Serialize(request, Json);
        Assert.DoesNotContain("hasPurchasePrice", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12.25m, JsonDocument.Parse(body).RootElement.GetProperty("purchasePrice").GetDecimal());
    }
    [Fact]
    public void Validation_does_not_turn_an_omitted_price_into_a_clear_command()
    {
        var request = new AssetRequest { Name = "Fixture", AssetTypeId = 1, OwningDepartmentId = 1 };
        Validation.Asset(request, false); Assert.False(request.HasPurchasePrice); Assert.Null(request.PurchasePrice);
    }
}
