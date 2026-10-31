using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.UnitTests;

public sealed class AssetLifecycleMetricsTests
{
    private sealed class Actor(params string[] grants) : IActor
    {
        public long? UserId => 7; public Guid CorrelationId => Guid.NewGuid();
        public string? Method => "POST"; public string? Path => "/fixture";
        public bool Has(string permission) => grants.Contains(permission);
    }
    [Theory]
    [InlineData(2026, 1, 31, 2026, 2, 27, 0)]
    [InlineData(2026, 1, 31, 2026, 2, 28, 1)]
    [InlineData(2024, 2, 29, 2025, 2, 28, 12)]
    [InlineData(2024, 10, 14, 2026, 10, 13, 23)]
    [InlineData(2024, 10, 14, 2026, 10, 14, 24)]
    public void Age_uses_whole_calendar_months_with_end_of_month_anniversaries(int py, int pm, int pd, int ay, int am, int ad, int expected)
    {
        var result = AssetLifecycleMetricsReader.Calculate(new() { PurchaseDate = new(py, pm, pd) }, new(ay, am, ad));
        Assert.Equal(expected, result.AgeMonths);
    }
    [Fact]
    public void Warranty_includes_its_end_date_and_missing_cost_is_not_fake_zero()
    {
        var date = new DateOnly(2026, 10, 14);
        var facts = new AssetLifecycleFacts { PurchaseDate = date, WarrantyEndDate = date, PurchaseCost = 1000m,
            KnownMaintenanceCost = 123.45m, MissingCostTickets = 1 };
        var result = AssetLifecycleMetricsReader.Calculate(facts, date);
        Assert.False(result.WarrantyExpired); Assert.Null(result.MaintenanceCostTotal); Assert.Null(result.MaintenanceCostRatio);
        Assert.Contains("IncompleteMaintenanceCost", result.InsufficientData);
        facts = facts with { MissingCostTickets = 0 };
        result = AssetLifecycleMetricsReader.Calculate(facts, date.AddDays(1));
        Assert.True(result.WarrantyExpired); Assert.Equal(123.45m, result.MaintenanceCostTotal); Assert.Equal(0.12345m, result.MaintenanceCostRatio);
    }
    [Fact]
    public void Missing_future_dates_and_zero_price_are_explicitly_insufficient()
    {
        var date = new DateOnly(2026, 10, 14);
        var result = AssetLifecycleMetricsReader.Calculate(new() { PurchaseDate = date.AddDays(1), PurchaseCost = 0m }, date);
        Assert.Null(result.AgeMonths); Assert.Null(result.WarrantyExpired); Assert.Null(result.MaintenanceCostRatio);
        Assert.Equal(new[] { "FuturePurchaseDate", "MissingWarrantyEndDate", "NonPositivePurchaseCost" }, result.InsufficientData);
        result = AssetLifecycleMetricsReader.Calculate(new(), date);
        Assert.Contains("MissingPurchaseDate", result.InsufficientData); Assert.Contains("MissingPurchaseCost", result.InsufficientData);
        Assert.Equal(0m, result.MaintenanceCostTotal); // No ticket and no missing cost are known facts.
    }
    [Theory]
    [InlineData(Permissions.AssetCost)] [InlineData(Permissions.ReplacementEvaluate)] [InlineData("")]
    public void Permission_checks_happen_before_any_repository_access(string grant)
    {
        var reader = new AssetLifecycleMetricsReader(null!, new Actor(grant));
        Assert.Equal(403, Assert.Throws<BusinessException>(() => reader.Query(1)).Status);
    }
    [Fact]
    public void Query_translates_to_one_statement_using_ticket_cost_not_history_snapshots()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=offline_metrics").Options);
        var reader = new AssetLifecycleMetricsReader(new EfRepository(db), new Actor(Permissions.ReplacementEvaluate, Permissions.AssetCost));
        var sql = reader.Query(1).ToQueryString(); Assert.Contains("maintenance_tickets", sql); Assert.Contains("actual_cost", sql);
        Assert.DoesNotContain("maintenance_histories", sql); Assert.DoesNotContain("license_key", sql);
        Assert.Contains(Permissions.ReplacementEvaluate, Permissions.All);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QueryCandidates_rejects_non_positive_asset_type_id(long assetTypeId)
    {
        var reader = new AssetLifecycleMetricsReader(null!, new Actor(Permissions.ReplacementEvaluate, Permissions.AssetCost));
        Assert.Equal(400, Assert.Throws<BusinessException>(() => reader.QueryCandidates(assetTypeId)).Status);
    }
    [Fact]
    public void QueryCandidates_translates_single_statement_with_optional_type_filter()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=offline_metrics").Options);
        var reader = new AssetLifecycleMetricsReader(new EfRepository(db), new Actor(Permissions.ReplacementEvaluate, Permissions.AssetCost));
        var sqlAll = reader.QueryCandidates(null).ToQueryString();
        Assert.Contains("assets", sqlAll);
        Assert.Contains("maintenance_tickets", sqlAll);
        Assert.DoesNotContain("asset_type_id =", sqlAll);

        var sqlType = reader.QueryCandidates(5).ToQueryString();
        Assert.Contains("asset_type_id", sqlType);
    }
}
