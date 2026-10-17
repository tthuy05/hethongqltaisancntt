using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseAlertsTests
{
    private sealed class Actor(long? id, params string[] grants) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "GET";
        public string? Path => "/api/v1/licenses/alerts";
        public bool Has(string permission) => grants.Contains(permission);
    }

    private sealed class Repository(params Array[] data) : IRepository
    {
        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            var match = data.OfType<T[]>().FirstOrDefault();
            return (match ?? Array.Empty<T>()).AsQueryable();
        }
        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public void Add<T>(T entity) where T : class => throw new NotSupportedException();
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task LockAsync(string resource, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Reader_enforces_authentication_and_license_read_grant()
    {
        var repo = new Repository();
        var unauthenticated = new LicenseAlertsReader(repo, new Actor(null, Permissions.LicenseRead));
        var missingGrant = new LicenseAlertsReader(repo, new Actor(7L));

        await Assert.ThrowsAsync<BusinessException>(() => unauthenticated.GetExpiringAsync(DateTime.UtcNow));
        await Assert.ThrowsAsync<BusinessException>(() => missingGrant.GetExpiringAsync(DateTime.UtcNow));
        await Assert.ThrowsAsync<BusinessException>(() => missingGrant.GetCapacityAlertsAsync());
        await Assert.ThrowsAsync<BusinessException>(() => missingGrant.GetSummaryAsync(DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task WindowDays_must_be_positive(int window)
    {
        var reader = new LicenseAlertsReader(new Repository(), new Actor(7L, Permissions.LicenseRead));
        await Assert.ThrowsAsync<BusinessException>(() => reader.GetExpiringAsync(DateTime.UtcNow, window));
        await Assert.ThrowsAsync<BusinessException>(() => reader.GetSummaryAsync(DateTime.UtcNow, window));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task UtilizationThreshold_must_be_within_zero_to_one(decimal threshold)
    {
        var reader = new LicenseAlertsReader(new Repository(), new Actor(7L, Permissions.LicenseRead));
        await Assert.ThrowsAsync<BusinessException>(() => reader.GetCapacityAlertsAsync(threshold));
    }

    [Fact]
    public async Task Expiring_query_filters_by_window_and_sorts_by_expiry()
    {
        var asOf = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var softwares = new[]
        {
            new Software { Id = 1, Code = "MS365", Name = "Microsoft 365" },
            new Software { Id = 2, Code = "JETB", Name = "JetBrains All Products" }
        };
        var licenses = new[]
        {
            new SoftwareLicense { Id = 10, SoftwareId = 1, LicenseCode = "LIC-01", LicenseType = "SUBSCRIPTION", TotalQuantity = 10, ExpiresAtUtc = asOf.AddDays(10), IsActive = true, PurchaseCost = 5000m },
            new SoftwareLicense { Id = 20, SoftwareId = 2, LicenseCode = "LIC-02", LicenseType = "VOLUME", TotalQuantity = 5, ExpiresAtUtc = asOf.AddDays(-2), IsActive = true, PurchaseCost = 3000m },
            new SoftwareLicense { Id = 30, SoftwareId = 1, LicenseCode = "LIC-03", LicenseType = "SUBSCRIPTION", TotalQuantity = 20, ExpiresAtUtc = asOf.AddDays(45), IsActive = true, PurchaseCost = 8000m },
            new SoftwareLicense { Id = 40, SoftwareId = 2, LicenseCode = "LIC-04", LicenseType = "VOLUME", TotalQuantity = 5, ExpiresAtUtc = asOf.AddDays(5), IsActive = false, PurchaseCost = 2000m } // Inactive
        };
        var assignments = new[]
        {
            new LicenseAssignment { Id = 100, SoftwareLicenseId = 10, AssignedUserId = 1, RevokedAtUtc = null },
            new LicenseAssignment { Id = 101, SoftwareLicenseId = 10, AssignedUserId = 2, RevokedAtUtc = null },
            new LicenseAssignment { Id = 102, SoftwareLicenseId = 10, AssignedUserId = 3, RevokedAtUtc = asOf.AddDays(-1) } // Revoked
        };

        var repo = new Repository(softwares, licenses, assignments);
        var readerWithCost = new LicenseAlertsReader(repo, new Actor(7L, Permissions.LicenseRead, Permissions.AssetCost));
        var readerNoCost = new LicenseAlertsReader(repo, new Actor(7L, Permissions.LicenseRead));

        var alerts = await readerWithCost.GetExpiringAsync(asOf, windowDays: 30);
        Assert.Equal(2, alerts.Count);

        // Sorted by expiry: LIC-02 (expired) first, then LIC-01
        var expired = alerts[0];
        Assert.Equal(20, expired.LicenseId);
        Assert.Equal("LIC-02", expired.LicenseCode);
        Assert.True(expired.IsExpired);
        Assert.Equal(-2, expired.DaysRemaining);
        Assert.Equal(3000m, expired.PurchaseCost);

        var expiring = alerts[1];
        Assert.Equal(10, expiring.LicenseId);
        Assert.Equal("LIC-01", expiring.LicenseCode);
        Assert.False(expiring.IsExpired);
        Assert.Equal(10, expiring.DaysRemaining);
        Assert.Equal(2, expiring.ActiveSeats); // 2 active, 1 revoked excluded

        // Least privilege: caller without AssetCost gets null purchase cost
        var noCostAlerts = await readerNoCost.GetExpiringAsync(asOf, windowDays: 30);
        Assert.Null(noCostAlerts[0].PurchaseCost);
        Assert.Null(noCostAlerts[1].PurchaseCost);
    }

    [Fact]
    public async Task Capacity_alerts_respect_utilization_threshold()
    {
        var softwares = new[] { new Software { Id = 1, Code = "WIN", Name = "Windows 11 Enterprise" } };
        var licenses = new[]
        {
            new SoftwareLicense { Id = 1, SoftwareId = 1, LicenseCode = "LIC-FULL", TotalQuantity = 2, IsActive = true },
            new SoftwareLicense { Id = 2, SoftwareId = 1, LicenseCode = "LIC-HIGH", TotalQuantity = 10, IsActive = true },
            new SoftwareLicense { Id = 3, SoftwareId = 1, LicenseCode = "LIC-LOW", TotalQuantity = 10, IsActive = true }
        };
        var assignments = new[]
        {
            new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 1 },
            new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 2 }, // 2/2 = 100%
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 3 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 4 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 5 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 6 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 7 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 8 }, // 6/10 = 60%
            new LicenseAssignment { SoftwareLicenseId = 3, AssignedUserId = 9 }  // 1/10 = 10%
        };

        var repo = new Repository(softwares, licenses, assignments);
        var reader = new LicenseAlertsReader(repo, new Actor(7L, Permissions.LicenseRead));

        var fullOnly = await reader.GetCapacityAlertsAsync(utilizationThreshold: 1.0m);
        Assert.Single(fullOnly);
        Assert.Equal(1, fullOnly[0].LicenseId);
        Assert.True(fullOnly[0].IsExhausted);
        Assert.Equal(0, fullOnly[0].AvailableSeats);
        Assert.Equal(1.0m, fullOnly[0].UtilizationRatio);

        var overHalf = await reader.GetCapacityAlertsAsync(utilizationThreshold: 0.5m);
        Assert.Equal(2, overHalf.Count);
        Assert.Equal(2, overHalf[0].LicenseId); // 6 active seats sorted first
        Assert.Equal(0.6m, overHalf[0].UtilizationRatio);
        Assert.False(overHalf[0].IsExhausted);
        Assert.Equal(4, overHalf[0].AvailableSeats);
    }

    [Fact]
    public async Task Summary_counts_real_metrics_without_fake_zeroes()
    {
        var asOf = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var softwares = new[] { new Software { Id = 1, Code = "APP", Name = "App" } };
        var licenses = new[]
        {
            new SoftwareLicense { Id = 1, SoftwareId = 1, LicenseCode = "L1", TotalQuantity = 5, ExpiresAtUtc = asOf.AddDays(5), IsActive = true }, // expiring
            new SoftwareLicense { Id = 2, SoftwareId = 1, LicenseCode = "L2", TotalQuantity = 2, ExpiresAtUtc = asOf.AddDays(-1), IsActive = true }, // expired & at capacity
            new SoftwareLicense { Id = 3, SoftwareId = 1, LicenseCode = "L3", TotalQuantity = 10, ExpiresAtUtc = asOf.AddDays(90), IsActive = true } // safe
        };
        var assignments = new[]
        {
            new LicenseAssignment { SoftwareLicenseId = 1, AssignedUserId = 1 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 2 },
            new LicenseAssignment { SoftwareLicenseId = 2, AssignedUserId = 3 }
        };

        var repo = new Repository(softwares, licenses, assignments);
        var reader = new LicenseAlertsReader(repo, new Actor(7L, Permissions.LicenseRead));

        var summary = await reader.GetSummaryAsync(asOf, windowDays: 30);
        Assert.Equal(3, summary.TotalActiveLicenses);
        Assert.Equal(1, summary.ExpiringSoonCount);
        Assert.Equal(1, summary.ExpiredCount);
        Assert.Equal(1, summary.AtCapacityCount);
        Assert.Equal(3, summary.TotalAllocatedSeats);
        Assert.Equal(17, summary.TotalSeats);
    }
}
