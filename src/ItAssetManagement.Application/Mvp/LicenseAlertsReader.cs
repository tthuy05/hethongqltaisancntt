using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed record LicenseExpiryAlert(
    long LicenseId,
    string LicenseCode,
    long SoftwareId,
    string SoftwareName,
    string LicenseType,
    int TotalQuantity,
    int ActiveSeats,
    DateTime? ExpiresAtUtc,
    int? DaysRemaining,
    bool IsExpired,
    decimal? PurchaseCost
);

public sealed record LicenseCapacityAlert(
    long LicenseId,
    string LicenseCode,
    long SoftwareId,
    string SoftwareName,
    string LicenseType,
    int TotalQuantity,
    int ActiveSeats,
    int AvailableSeats,
    decimal UtilizationRatio,
    bool IsExhausted,
    decimal? PurchaseCost
);

public sealed record LicenseAlertsSummary(
    int TotalActiveLicenses,
    int ExpiringSoonCount,
    int ExpiredCount,
    int AtCapacityCount,
    int TotalAllocatedSeats,
    int TotalSeats
);

public sealed class LicenseAlertsReader(IRepository repo, IActor actor)
{
    private void RequireAuthorization()
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.LicenseRead))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền xem cảnh báo license.");
    }

    private sealed record LicenseAlertQueryRow
    {
        public long LicenseId { get; init; }
        public string LicenseCode { get; init; } = "";
        public long SoftwareId { get; init; }
        public string SoftwareName { get; init; } = "";
        public string LicenseType { get; init; } = "";
        public int TotalQuantity { get; init; }
        public int ActiveSeats { get; init; }
        public DateTime? ExpiresAtUtc { get; init; }
        public decimal? PurchaseCost { get; init; }
    }

    private IQueryable<LicenseAlertQueryRow> BaseQuery()
    {
        RequireAuthorization();
        var assignments = repo.Query<LicenseAssignment>();
        var softwares = repo.Query<Software>();
        var hasCost = actor.Has(Permissions.AssetCost);

        return from l in repo.Query<SoftwareLicense>()
               join s in softwares on l.SoftwareId equals s.Id
               where l.IsActive
               select new LicenseAlertQueryRow
               {
                   LicenseId = l.Id,
                   LicenseCode = l.LicenseCode,
                   SoftwareId = s.Id,
                   SoftwareName = s.Name,
                   LicenseType = l.LicenseType,
                   TotalQuantity = l.TotalQuantity,
                   ActiveSeats = assignments.Count(a => a.SoftwareLicenseId == l.Id && a.RevokedAtUtc == null && !a.IsArchived),
                   ExpiresAtUtc = l.ExpiresAtUtc,
                   PurchaseCost = hasCost ? l.PurchaseCost : null
               };
    }

    public async Task<IReadOnlyList<LicenseExpiryAlert>> GetExpiringAsync(
        DateTime asOfUtc, int windowDays = 30, CancellationToken ct = default)
    {
        if (windowDays <= 0)
            throw Validation.Invalid("windowDays", "Số ngày cửa sổ cảnh báo phải > 0.");

        var threshold = asOfUtc.AddDays(windowDays);
        var rows = await repo.ListAsync(
            BaseQuery().Where(r => r.ExpiresAtUtc != null && r.ExpiresAtUtc <= threshold)
                       .OrderBy(r => r.ExpiresAtUtc), ct);

        return rows.Select(r =>
        {
            int? daysRemaining = r.ExpiresAtUtc.HasValue
                ? (int)Math.Floor((r.ExpiresAtUtc.Value - asOfUtc).TotalDays)
                : null;
            bool isExpired = r.ExpiresAtUtc.HasValue && r.ExpiresAtUtc.Value < asOfUtc;
            return new LicenseExpiryAlert(
                r.LicenseId,
                r.LicenseCode,
                r.SoftwareId,
                r.SoftwareName,
                r.LicenseType,
                r.TotalQuantity,
                r.ActiveSeats,
                r.ExpiresAtUtc,
                daysRemaining,
                isExpired,
                r.PurchaseCost
            );
        }).ToList();
    }

    public async Task<IReadOnlyList<LicenseCapacityAlert>> GetCapacityAlertsAsync(
        decimal utilizationThreshold = 1.0m, CancellationToken ct = default)
    {
        if (utilizationThreshold <= 0 || utilizationThreshold > 1.0m)
            throw Validation.Invalid("utilizationThreshold", "Ngưỡng sử dụng phải nằm trong khoảng (0, 1].");

        var rows = await repo.ListAsync(BaseQuery(), ct);

        return rows
            .Where(r => r.TotalQuantity > 0 &&
                        ((decimal)r.ActiveSeats / r.TotalQuantity) >= utilizationThreshold)
            .OrderByDescending(r => r.ActiveSeats)
            .Select(r =>
            {
                var ratio = r.TotalQuantity > 0 ? Math.Round((decimal)r.ActiveSeats / r.TotalQuantity, 4) : 0m;
                var available = Math.Max(0, r.TotalQuantity - r.ActiveSeats);
                var isExhausted = r.ActiveSeats >= r.TotalQuantity;
                return new LicenseCapacityAlert(
                    r.LicenseId,
                    r.LicenseCode,
                    r.SoftwareId,
                    r.SoftwareName,
                    r.LicenseType,
                    r.TotalQuantity,
                    r.ActiveSeats,
                    available,
                    ratio,
                    isExhausted,
                    r.PurchaseCost
                );
            }).ToList();
    }

    public async Task<LicenseAlertsSummary> GetSummaryAsync(
        DateTime asOfUtc, int windowDays = 30, CancellationToken ct = default)
    {
        if (windowDays <= 0)
            throw Validation.Invalid("windowDays", "Số ngày cửa sổ cảnh báo phải > 0.");

        var rows = await repo.ListAsync(BaseQuery(), ct);
        var threshold = asOfUtc.AddDays(windowDays);

        int totalActive = rows.Count;
        int expiringSoon = rows.Count(r => r.ExpiresAtUtc.HasValue && r.ExpiresAtUtc.Value >= asOfUtc && r.ExpiresAtUtc.Value <= threshold);
        int expired = rows.Count(r => r.ExpiresAtUtc.HasValue && r.ExpiresAtUtc.Value < asOfUtc);
        int atCapacity = rows.Count(r => r.TotalQuantity > 0 && r.ActiveSeats >= r.TotalQuantity);
        int totalAllocated = rows.Sum(r => r.ActiveSeats);
        int totalSeats = rows.Sum(r => r.TotalQuantity);

        return new LicenseAlertsSummary(
            totalActive,
            expiringSoon,
            expired,
            atCapacity,
            totalAllocated,
            totalSeats
        );
    }
}
