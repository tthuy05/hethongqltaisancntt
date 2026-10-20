using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Reporting;

public sealed class DashboardQueryService(IRepository repo, IActor actor, TimeProvider? timeProvider = null)
{
    private DateTime NowUtc => timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UtcNow;

    private void RequireDashboardRead()
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.DashboardRead))
            throw new BusinessException(403, "FORBIDDEN", "Yêu cầu quyền xem tổng quan hệ thống (dashboard.read).");
    }

    private void RequireDashboardCostRead()
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.DashboardCost))
            throw new BusinessException(403, "FORBIDDEN", "Yêu cầu quyền xem chi phí tổng quan (dashboard.cost.read).");
    }

    private sealed record StatusCountRow
    {
        public string Status { get; init; } = "";
        public int Count { get; init; }
        public decimal TotalCost { get; init; }
    }

    private sealed record IdNameRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = "";
    }

    private sealed record GroupCountRow
    {
        public long Id { get; init; }
        public string Label { get; init; } = "";
        public int Count { get; init; }
        public decimal TotalCost { get; init; }
    }

    private sealed record RecentAssetQueryRow
    {
        public long Id { get; init; }
        public string AssetCode { get; init; } = "";
        public string Name { get; init; } = "";
        public long AssetTypeId { get; init; }
        public long OwningDepartmentId { get; init; }
        public string CurrentStatus { get; init; } = "";
        public DateTime CreatedAtUtc { get; init; }
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardSummaryQuery query, CancellationToken ct = default)
    {
        RequireDashboardRead();
        query.Validate();

        var assets = repo.Query<Asset>().Where(a => !a.IsArchived);
        if (query.DepartmentId.HasValue)
            assets = assets.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);

        var statusGroups = await repo.ListAsync(
            assets.GroupBy(a => a.CurrentStatus)
                  .Select(g => new StatusCountRow
                  {
                      Status = g.Key,
                      Count = g.Count(),
                      TotalCost = g.Sum(x => x.PurchaseCost ?? 0m)
                  }), ct);

        var totalAssets = statusGroups.Sum(x => x.Count);
        var inUse = statusGroups.FirstOrDefault(x => x.Status == "IN_USE")?.Count ?? 0;
        var maintenance = statusGroups.FirstOrDefault(x => x.Status == "MAINTENANCE")?.Count ?? 0;
        var inStock = statusGroups.FirstOrDefault(x => x.Status == "IN_STOCK")?.Count ?? 0;

        var byStatus = statusGroups.Select(g => new DashboardSeriesDto(
            g.Status,
            FormatStatusLabel(g.Status),
            g.Count,
            actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) ? g.TotalCost : null
        )).ToList();

        // Tickets
        var tickets = repo.Query<MaintenanceTicket>().Where(t => !t.IsArchived && (t.Status == "PENDING" || t.Status == "IN_PROGRESS"));
        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            tickets = tickets.Where(t => deptAssetIds.Contains(t.AssetId));
        }
        var openTickets = await repo.CountAsync(tickets, ct);

        // Licenses (Global, not scoped to department)
        int? activeLicenses = actor.Has(Permissions.LicenseRead)
            ? await repo.CountAsync(repo.Query<SoftwareLicense>().Where(l => l.IsActive), ct) : null;

        // Replacement recommendations (Active disposition & Current)
        var recs = repo.Query<ReplacementRecommendation>().Where(r => !r.IsArchived && r.IsCurrent && r.Disposition == "ACTIVE");
        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            recs = recs.Where(r => deptAssetIds.Contains(r.AssetId));
        }
        var pendingReplacements = await repo.CountAsync(recs, ct);

        // Recent 5 assets
        var recentRows = await repo.ListAsync(
            assets.OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
                  .Take(5)
                  .Select(a => new RecentAssetQueryRow
                  {
                      Id = a.Id,
                      AssetCode = a.AssetCode,
                      Name = a.Name,
                      AssetTypeId = a.AssetTypeId,
                      OwningDepartmentId = a.OwningDepartmentId,
                      CurrentStatus = a.CurrentStatus,
                      CreatedAtUtc = a.CreatedAtUtc
                  }), ct);

        var deptIds = recentRows.Select(r => r.OwningDepartmentId).Distinct().ToList();
        var typeIds = recentRows.Select(r => r.AssetTypeId).Distinct().ToList();

        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)).Select(d => new IdNameRow { Id = d.Id, Name = d.Name }), ct))
            .ToDictionary(d => d.Id, d => d.Name);
        var types = (await repo.ListAsync(repo.Query<AssetType>().Where(t => typeIds.Contains(t.Id)).Select(t => new IdNameRow { Id = t.Id, Name = t.Name }), ct))
            .ToDictionary(t => t.Id, t => t.Name);

        var recentAssets = recentRows.Select(r => new RecentAssetDto(
            r.Id,
            r.AssetCode,
            r.Name,
            types.TryGetValue(r.AssetTypeId, out var tn) ? tn : "—",
            departments.TryGetValue(r.OwningDepartmentId, out var dn) ? dn : "—",
            r.CurrentStatus,
            r.CreatedAtUtc
        )).ToList();

        return new DashboardSummaryDto(
            totalAssets,
            inUse,
            maintenance,
            inStock,
            openTickets,
            activeLicenses,
            pendingReplacements,
            byStatus,
            recentAssets,
            NowUtc
        );
    }

    public async Task<IReadOnlyList<DashboardSeriesDto>> GetAssetGroupsAsync(DashboardAssetGroupQuery query, CancellationToken ct = default)
    {
        RequireDashboardRead();
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost);
        var assets = repo.Query<Asset>().Where(a => !a.IsArchived);
        if (query.DepartmentId.HasValue)
            assets = assets.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);

        var groupBy = query.GroupBy.ToLowerInvariant();
        if (groupBy == "status")
        {
            var groups = await repo.ListAsync(
                assets.GroupBy(a => a.CurrentStatus)
                      .Select(g => new StatusCountRow
                      {
                          Status = g.Key,
                          Count = g.Count(),
                          TotalCost = g.Sum(x => x.PurchaseCost ?? 0m)
                      }), ct);

            return groups.Select(g => new DashboardSeriesDto(
                g.Status,
                FormatStatusLabel(g.Status),
                g.Count,
                canReadCost ? g.TotalCost : null
            )).OrderByDescending(x => x.Count).ToList();
        }

        if (groupBy == "department")
        {
            var departments = repo.Query<Department>();
            var groups = await repo.ListAsync(
                from a in assets
                join d in departments on a.OwningDepartmentId equals d.Id into deptJoin
                from d in deptJoin.DefaultIfEmpty()
                group a by new { Id = a.OwningDepartmentId, Label = d != null ? d.Name : "Không xác định" } into g
                select new GroupCountRow
                {
                    Id = g.Key.Id,
                    Label = g.Key.Label,
                    Count = g.Count(),
                    TotalCost = g.Sum(asset => asset.PurchaseCost ?? 0m)
                }, ct);

            return groups.Select(g => new DashboardSeriesDto(
                g.Id.ToString(), g.Label, g.Count, canReadCost ? g.TotalCost : null
            )).OrderByDescending(x => x.Count).ToList();
        }

        if (groupBy == "type")
        {
            var types = repo.Query<AssetType>();
            var groups = await repo.ListAsync(
                from a in assets
                join t in types on a.AssetTypeId equals t.Id into typeJoin
                from t in typeJoin.DefaultIfEmpty()
                group a by new { Id = a.AssetTypeId, Label = t != null ? t.Name : "Không xác định" } into g
                select new GroupCountRow
                {
                    Id = g.Key.Id,
                    Label = g.Key.Label,
                    Count = g.Count(),
                    TotalCost = g.Sum(asset => asset.PurchaseCost ?? 0m)
                }, ct);

            return groups.Select(g => new DashboardSeriesDto(
                g.Id.ToString(), g.Label, g.Count, canReadCost ? g.TotalCost : null
            )).OrderByDescending(x => x.Count).ToList();
        }

        return Array.Empty<DashboardSeriesDto>();
    }

    private sealed record WarrantyAssetQueryRow
    {
        public long Id { get; init; }
        public string AssetCode { get; init; } = "";
        public string Name { get; init; } = "";
        public long OwningDepartmentId { get; init; }
        public DateOnly? WarrantyEndDate { get; init; }
    }

    private sealed record LicenseAlertQueryRow
    {
        public long Id { get; init; }
        public string SoftwareName { get; init; } = "";
        public string LicenseCode { get; init; } = "";
        public string LicenseType { get; init; } = "";
        public string? LicenseKeyLast4 { get; init; }
        public DateTime? ExpiresAtUtc { get; init; }
        public int TotalQuantity { get; init; }
        public int ActiveSeats { get; init; }
    }

    private sealed record TicketAlertQueryRow
    {
        public long Id { get; init; }
        public string TicketCode { get; init; } = "";
        public string Title { get; init; } = "";
        public string Priority { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime? DueAtUtc { get; init; }
    }

    public async Task<DashboardAlertsDto> GetAlertsAsync(DashboardAlertsQuery query, CancellationToken ct = default)
    {
        RequireDashboardRead();
        query.Validate();

        var now = NowUtc;
        var today = DateOnly.FromDateTime(now);
        var targetDate = today.AddDays(query.DaysAhead);
        var targetDateTime = now.AddDays(query.DaysAhead);

        // Warranty alerts
        var assets = repo.Query<Asset>().Where(a => !a.IsArchived && a.WarrantyEndDate != null && a.WarrantyEndDate.Value <= targetDate);
        if (query.DepartmentId.HasValue)
            assets = assets.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);

        var warrantyAlertCount = await repo.CountAsync(assets, ct);

        var warrantyRows = await repo.ListAsync(
            assets.OrderBy(a => a.WarrantyEndDate).ThenBy(a => a.Id)
                  .Take(50)
                  .Select(a => new WarrantyAssetQueryRow
                  {
                      Id = a.Id,
                      AssetCode = a.AssetCode,
                      Name = a.Name,
                      OwningDepartmentId = a.OwningDepartmentId,
                      WarrantyEndDate = a.WarrantyEndDate
                  }), ct);

        var deptIds = warrantyRows.Select(w => w.OwningDepartmentId).Distinct().ToList();
        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)).Select(d => new IdNameRow { Id = d.Id, Name = d.Name }), ct))
            .ToDictionary(d => d.Id, d => d.Name);

        var warrantyAlerts = warrantyRows.Select(w => new AssetAlertItemDto(
            w.Id,
            w.AssetCode,
            w.Name,
            departments.TryGetValue(w.OwningDepartmentId, out var dn) ? dn : "—",
            w.WarrantyEndDate,
            w.WarrantyEndDate.HasValue ? w.WarrantyEndDate.Value.DayNumber - today.DayNumber : 0
        )).ToList();

        // License alerts (Respects license read permission; key is masked)
        IReadOnlyList<LicenseAlertItemDto>? licenseAlerts = null;
        var licenseAlertCount = 0;
        if (actor.Has(Permissions.LicenseRead))
        {
            var expiringLicenses = repo.Query<SoftwareLicense>()
                .Where(l => l.IsActive && l.ExpiresAtUtc != null && l.ExpiresAtUtc.Value <= targetDateTime);
            licenseAlertCount = await repo.CountAsync(expiringLicenses, ct);
            var softwares = repo.Query<Software>();
            var assignments = repo.Query<LicenseAssignment>();
            var licenseRows = await repo.ListAsync(
                (from l in expiringLicenses
                join s in softwares on l.SoftwareId equals s.Id
                orderby l.ExpiresAtUtc, l.Id
                select new LicenseAlertQueryRow
                {
                    Id = l.Id,
                    SoftwareName = s.Name,
                    LicenseCode = l.LicenseCode,
                    LicenseType = l.LicenseType,
                    LicenseKeyLast4 = l.LicenseKeyLast4,
                    ExpiresAtUtc = l.ExpiresAtUtc,
                    TotalQuantity = l.TotalQuantity,
                    ActiveSeats = assignments.Count(a => a.SoftwareLicenseId == l.Id && a.RevokedAtUtc == null && !a.IsArchived)
                }).Take(50), ct);

            licenseAlerts = licenseRows.Select(l => new LicenseAlertItemDto(
                l.Id,
                l.SoftwareName,
                l.LicenseCode,
                l.LicenseType,
                l.LicenseKeyLast4,
                l.ExpiresAtUtc,
                l.ExpiresAtUtc.HasValue ? (int)Math.Floor((l.ExpiresAtUtc.Value - now).TotalDays) : 0,
                Math.Max(0, l.TotalQuantity - l.ActiveSeats)
            )).ToList();
        }

        // Ticket alerts
        var tickets = repo.Query<MaintenanceTicket>().Where(t => !t.IsArchived && (t.Status == "PENDING" || t.Status == "IN_PROGRESS"));
        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            tickets = tickets.Where(t => deptAssetIds.Contains(t.AssetId));
        }

        var ticketRows = await repo.ListAsync(
            tickets.OrderBy(t => t.Priority == "CRITICAL" ? 1 : t.Priority == "HIGH" ? 2 : t.Priority == "MEDIUM" ? 3 : 4)
                   .ThenBy(t => t.DueAtUtc)
                   .Take(50)
                   .Select(t => new TicketAlertQueryRow
                   {
                       Id = t.Id,
                       TicketCode = t.TicketCode,
                       Title = t.Title,
                       Priority = t.Priority,
                       Status = t.Status,
                       DueAtUtc = t.DueAtUtc
                   }), ct);

        var totalOpenTickets = await repo.CountAsync(tickets, ct);
        var highPriorityCount = await repo.CountAsync(tickets.Where(t => t.Priority == "HIGH" || t.Priority == "CRITICAL"), ct);
        var overdueCount = await repo.CountAsync(tickets.Where(t => t.DueAtUtc != null && t.DueAtUtc.Value < now), ct);

        var ticketAlerts = ticketRows.Select(t => new TicketAlertItemDto(
            t.Id,
            t.TicketCode,
            t.Title,
            t.Priority,
            t.Status,
            t.DueAtUtc,
            t.DueAtUtc.HasValue && t.DueAtUtc.Value < now
        )).ToList();

        return new DashboardAlertsDto(
            warrantyAlertCount,
            warrantyAlerts,
            licenseAlertCount,
            licenseAlerts,
            totalOpenTickets,
            highPriorityCount,
            overdueCount,
            ticketAlerts
        );
    }

    private sealed record MonthCostRow
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public decimal TotalCost { get; init; }
    }

    private sealed record PriorityCostRow
    {
        public string Priority { get; init; } = "";
        public int Count { get; init; }
        public decimal TotalCost { get; init; }
    }

    public async Task<DashboardCostSummaryDto> GetCostsAsync(DashboardCostsQuery query, CancellationToken ct = default)
    {
        RequireDashboardCostRead();
        query.Validate();

        var now = NowUtc;
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var assets = repo.Query<Asset>().Where(a => !a.IsArchived);
        if (query.DepartmentId.HasValue)
            assets = assets.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);

        var assetTotal = await repo.FirstAsync(assets.GroupBy(a => 1).Select(g => new { Total = g.Sum(a => a.PurchaseCost ?? 0m) }), ct);
        var totalAssetValue = assetTotal?.Total ?? 0m;

        // Maintenance costs YTD
        var tickets = repo.Query<MaintenanceTicket>().Where(t => !t.IsArchived && t.ResolvedAtUtc != null && t.ResolvedAtUtc.Value >= startOfYear);
        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            tickets = tickets.Where(t => deptAssetIds.Contains(t.AssetId));
        }
        if (query.FromUtc.HasValue)
            tickets = tickets.Where(t => t.ResolvedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            tickets = tickets.Where(t => t.ResolvedAtUtc <= query.ToUtc.Value);

        var monthlyRows = await repo.ListAsync(tickets.Where(t => t.ActualCost.HasValue)
            .GroupBy(t => new { t.ResolvedAtUtc!.Value.Year, t.ResolvedAtUtc.Value.Month })
            .Select(g => new MonthCostRow { Year = g.Key.Year, Month = g.Key.Month, TotalCost = g.Sum(t => t.ActualCost ?? 0m) }), ct);
        var maintenanceCostYtd = monthlyRows.Sum(t => t.TotalCost);
        var monthlyMaintenance = monthlyRows.Select(g => new MonthlyCostDto(g.Year, g.Month, g.TotalCost))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        // Replacement forecast (Active / Planned current recommendations)
        var recs = repo.Query<ReplacementRecommendation>().Where(r => !r.IsArchived && r.IsCurrent && (r.Disposition == "ACTIVE" || r.Disposition == "PLANNED"));
        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            recs = recs.Where(r => deptAssetIds.Contains(r.AssetId));
        }

        var recRows = await repo.ListAsync(recs.GroupBy(r => r.Priority).Select(g => new {
            Priority = g.Key, Count = g.Count(), TotalCost = g.Sum(r => r.EstimatedReplacementCost ?? 0m),
            Missing = g.Count(r => r.EstimatedReplacementCost == null)
        }), ct);
        var totalBudgetForecast = recRows.Sum(r => r.TotalCost);
        var missingEstimateCount = recRows.Sum(r => r.Missing);
        var budgetByPriority = recRows.Select(g => new PriorityBudgetDto(g.Priority, g.Count, g.TotalCost))
            .OrderByDescending(x => x.TotalCost)
            .ToList();

        return new DashboardCostSummaryDto(
            "VND",
            decimal.Round(totalAssetValue, 2),
            decimal.Round(maintenanceCostYtd, 2),
            decimal.Round(totalBudgetForecast, 2),
            missingEstimateCount,
            monthlyMaintenance,
            budgetByPriority
        );
    }

    private static string FormatStatusLabel(string status) => status switch
    {
        "IN_STOCK" => "Sẵn sàng (Trong kho)",
        "IN_USE" => "Đang sử dụng",
        "MAINTENANCE" => "Đang bảo trì",
        "BROKEN" => "Hỏng hóc",
        "DISPOSED" => "Đã thanh lý",
        "RETIRED" => "Ngừng sử dụng",
        "LOST" => "Thất lạc",
        _ => status
    };
}
