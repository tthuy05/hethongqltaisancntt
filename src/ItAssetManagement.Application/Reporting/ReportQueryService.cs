using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Reporting;

public sealed class ReportQueryService(IRepository repo, IActor actor, TimeProvider? timeProvider = null)
{
    private DateTime NowUtc => timeProvider?.GetUtcNow().UtcDateTime ?? DateTime.UtcNow;

    private void RequirePermission(string permission, string message)
    {
        if (actor.UserId is not > 0 || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", message);
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
        public int MissingCostCount { get; init; }
    }

    // ---------------------------------------------------------
    // EP-076: GET /reports/asset-inventory
    // ---------------------------------------------------------
    public async Task<PagedResponse<AssetInventoryRowDto>> GetAssetInventoryReportAsync(
        AssetInventoryReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportAssets, "Yêu cầu quyền xem báo cáo tài sản (reports.assets.read).");
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        var q = repo.Query<Asset>();

        if (!query.IncludeArchived)
            q = q.Where(a => !a.IsArchived);

        if (query.AssetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);

        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(a => a.CurrentStatus == query.Status);

        if (query.PurchasedFrom.HasValue)
            q = q.Where(a => a.PurchaseDate != null && a.PurchaseDate.Value >= query.PurchasedFrom.Value);

        if (query.PurchasedTo.HasValue)
            q = q.Where(a => a.PurchaseDate != null && a.PurchaseDate.Value <= query.PurchasedTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
            q = repo.SearchAssets(q, query.Keyword.Trim());

        var totalItems = await repo.CountAsync(q, ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        // Sorting
        if (string.Equals(query.SortBy, "purchasecost", StringComparison.OrdinalIgnoreCase) && !canReadCost)
            throw new BusinessException(403, "FORBIDDEN", "Yêu cầu quyền xem chi phí để sắp xếp theo giá mua.");

        var sortDesc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Asset> ordered = (query.SortBy?.ToLowerInvariant(), sortDesc) switch
        {
            ("assetcode", false) => q.OrderBy(a => a.AssetCode),
            ("assetcode", true) => q.OrderByDescending(a => a.AssetCode),
            ("name", false) => q.OrderBy(a => a.Name),
            ("name", true) => q.OrderByDescending(a => a.Name),
            ("purchasedate", false) => q.OrderBy(a => a.PurchaseDate),
            ("purchasedate", true) => q.OrderByDescending(a => a.PurchaseDate),
            ("purchasecost", false) => q.OrderBy(a => a.PurchaseCost),
            ("purchasecost", true) => q.OrderByDescending(a => a.PurchaseCost),
            _ => sortDesc ? q.OrderByDescending(a => a.CreatedAtUtc) : q.OrderBy(a => a.CreatedAtUtc)
        };

        var pagedAssets = await repo.ListAsync(
            ordered.ThenBy(a => a.Id).Skip((query.Page - 1) * query.PageSize)
             .Take(query.PageSize), ct);

        var deptIds = pagedAssets.Select(a => a.OwningDepartmentId).Distinct().ToList();
        var typeIds = pagedAssets.Select(a => a.AssetTypeId).Distinct().ToList();

        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)).Select(d => new IdNameRow { Id = d.Id, Name = d.Name }), ct))
            .ToDictionary(d => d.Id, d => d.Name);
        var types = (await repo.ListAsync(repo.Query<AssetType>().Where(t => typeIds.Contains(t.Id)).Select(t => new IdNameRow { Id = t.Id, Name = t.Name }), ct))
            .ToDictionary(t => t.Id, t => t.Name);

        var items = pagedAssets.Select(a => new AssetInventoryRowDto(
            a.Id,
            a.AssetCode,
            a.Name,
            types.TryGetValue(a.AssetTypeId, out var tn) ? tn : "—",
            departments.TryGetValue(a.OwningDepartmentId, out var dn) ? dn : "—",
            a.CurrentStatus,
            a.PurchaseDate,
            canReadCost ? a.PurchaseCost : null,
            a.WarrantyEndDate,
            a.SerialNumber,
            a.IsArchived
        )).ToList();

        return new PagedResponse<AssetInventoryRowDto>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    // ---------------------------------------------------------
    // EP-077: GET /reports/assets-by-department
    // ---------------------------------------------------------
    public async Task<IReadOnlyList<DashboardSeriesDto>> GetAssetsByDepartmentReportAsync(
        ReportGroupQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportAssets, "Yêu cầu quyền xem báo cáo tài sản (reports.assets.read).");
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        var q = repo.Query<Asset>().Where(a => !a.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(a => a.CurrentStatus == query.Status);
        if (query.AssetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);

        var departments = repo.Query<Department>();
        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);
        var groups = await repo.ListAsync(
            from a in q
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

    // ---------------------------------------------------------
    // EP-078: GET /reports/assets-by-type
    // ---------------------------------------------------------
    public async Task<IReadOnlyList<DashboardSeriesDto>> GetAssetsByTypeReportAsync(
        ReportGroupQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportAssets, "Yêu cầu quyền xem báo cáo tài sản (reports.assets.read).");
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        var q = repo.Query<Asset>().Where(a => !a.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(a => a.CurrentStatus == query.Status);
        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);

        var types = repo.Query<AssetType>();
        if (query.AssetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);
        var groups = await repo.ListAsync(
            from a in q
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

    // ---------------------------------------------------------
    // EP-079: GET /reports/asset-status
    // ---------------------------------------------------------
    public async Task<IReadOnlyList<DashboardSeriesDto>> GetAssetStatusReportAsync(
        ReportGroupQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportAssets, "Yêu cầu quyền xem báo cáo tài sản (reports.assets.read).");
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        var q = repo.Query<Asset>().Where(a => !a.IsArchived);
        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(a => a.CurrentStatus == query.Status);

        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);
        if (query.AssetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);

        var rows = await repo.ListAsync(
            q.GroupBy(a => a.CurrentStatus)
             .Select(g => new
             {
                 Status = g.Key,
                 Count = g.Count(),
                 TotalCost = g.Sum(x => x.PurchaseCost ?? 0m)
             }), ct);

        return rows.Select(g => new DashboardSeriesDto(
            g.Status,
            g.Status,
            g.Count,
            canReadCost ? g.TotalCost : null
        )).OrderByDescending(x => x.Count).ToList();
    }

    // ---------------------------------------------------------
    // EP-080: GET /reports/assignment-history
    // ---------------------------------------------------------
    public async Task<PagedResponse<AssignmentReportRowDto>> GetAssignmentReportAsync(
        AssignmentReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportAssignments, "Yêu cầu quyền xem báo cáo phân bổ (reports.assignments.read).");
        query.Validate();

        var q = repo.Query<AssetAssignment>();
        if (!query.IncludeArchived)
            q = q.Where(a => !a.IsArchived);

        if (query.AssetId.HasValue)
            q = q.Where(a => a.AssetId == query.AssetId.Value);
        if (query.UserId.HasValue)
            q = q.Where(a => a.AssignedUserId == query.UserId.Value);
        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.AssignedDepartmentId == query.DepartmentId.Value);
        if (query.FromUtc.HasValue)
            q = q.Where(a => a.AssignedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            q = q.Where(a => a.AssignedAtUtc <= query.ToUtc.Value);

        var totalItems = await repo.CountAsync(q, ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        var assignments = await repo.ListAsync(
            q.OrderByDescending(a => a.AssignedAtUtc)
             .ThenByDescending(a => a.Id)
             .Skip((query.Page - 1) * query.PageSize)
             .Take(query.PageSize), ct);

        var assetIds = assignments.Select(a => a.AssetId).Distinct().ToList();
        var userIds = assignments.Where(a => a.AssignedUserId.HasValue).Select(a => a.AssignedUserId!.Value).Distinct().ToList();
        var deptIds = assignments.Where(a => a.AssignedDepartmentId.HasValue).Select(a => a.AssignedDepartmentId!.Value).Distinct().ToList();

        var assets = (await repo.ListAsync(repo.Query<Asset>().Where(a => assetIds.Contains(a.Id)).Select(a => new { a.Id, a.AssetCode, a.Name }), ct))
            .ToDictionary(a => a.Id, a => (AssetCode: a.AssetCode, Name: a.Name));
        var users = (await repo.ListAsync(repo.Query<User>().Where(u => userIds.Contains(u.Id)).Select(u => new IdNameRow { Id = u.Id, Name = u.FullName }), ct))
            .ToDictionary(u => u.Id, u => u.Name);
        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)).Select(d => new IdNameRow { Id = d.Id, Name = d.Name }), ct))
            .ToDictionary(d => d.Id, d => d.Name);

        var items = assignments.Select(a =>
        {
            var assetInfo = assets.TryGetValue(a.AssetId, out var ai) ? ai : (AssetCode: "—", Name: "—");
            var userName = a.AssignedUserId.HasValue && users.TryGetValue(a.AssignedUserId.Value, out var un) ? un : null;
            var deptName = a.AssignedDepartmentId.HasValue && departments.TryGetValue(a.AssignedDepartmentId.Value, out var dn) ? dn : null;
            var status = a.ReturnedAtUtc.HasValue ? "RETURNED" : "ASSIGNED";

            return new AssignmentReportRowDto(
                a.Id,
                a.AssetId,
                assetInfo.AssetCode,
                assetInfo.Name,
                userName,
                deptName,
                a.AssignedAtUtc,
                a.ReturnedAtUtc,
                status
            );
        }).ToList();

        return new PagedResponse<AssignmentReportRowDto>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    // ---------------------------------------------------------
    // EP-081: GET /reports/maintenance-history
    // ---------------------------------------------------------
    public async Task<PagedResponse<MaintenanceReportRowDto>> GetMaintenanceReportAsync(
        MaintenanceReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportMaintenance, "Yêu cầu quyền xem báo cáo bảo trì (reports.maintenance.read).");
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        var q = repo.Query<MaintenanceTicket>();

        if (!query.IncludeArchived)
            q = q.Where(t => !t.IsArchived);

        if (query.AssetId.HasValue)
            q = q.Where(t => t.AssetId == query.AssetId.Value);
        if (query.TechnicianId.HasValue)
            q = q.Where(t => t.AssignedToUserId == query.TechnicianId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(t => t.Status == query.Status);
        if (query.FromUtc.HasValue)
            q = q.Where(t => t.OpenedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            q = q.Where(t => t.OpenedAtUtc <= query.ToUtc.Value);

        var totalItems = await repo.CountAsync(q, ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        var tickets = await repo.ListAsync(
            q.OrderByDescending(t => t.OpenedAtUtc)
             .ThenByDescending(t => t.Id)
             .Skip((query.Page - 1) * query.PageSize)
             .Take(query.PageSize), ct);

        var assetIds = tickets.Select(t => t.AssetId).Distinct().ToList();
        var techIds = tickets.Where(t => t.AssignedToUserId.HasValue).Select(t => t.AssignedToUserId!.Value).Distinct().ToList();

        var assets = (await repo.ListAsync(repo.Query<Asset>().Where(a => assetIds.Contains(a.Id)).Select(a => new { a.Id, a.AssetCode }), ct))
            .ToDictionary(a => a.Id, a => a.AssetCode);
        var techs = (await repo.ListAsync(repo.Query<User>().Where(u => techIds.Contains(u.Id)).Select(u => new IdNameRow { Id = u.Id, Name = u.FullName }), ct))
            .ToDictionary(u => u.Id, u => u.Name);

        var items = tickets.Select(t => new MaintenanceReportRowDto(
            t.Id,
            t.TicketCode,
            t.AssetId,
            assets.TryGetValue(t.AssetId, out var code) ? code : "—",
            t.Title,
            t.Priority,
            t.Status,
            t.AssignedToUserId.HasValue && techs.TryGetValue(t.AssignedToUserId.Value, out var techName) ? techName : null,
            t.OpenedAtUtc,
            t.ResolvedAtUtc,
            canReadCost ? t.ActualCost : null
        )).ToList();

        return new PagedResponse<MaintenanceReportRowDto>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    // ---------------------------------------------------------
    // EP-082: GET /reports/maintenance-cost
    // ---------------------------------------------------------
    public async Task<CostReportDto> GetMaintenanceCostReportAsync(
        MaintenanceCostReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportCost, "Yêu cầu quyền xem báo cáo chi phí (reports.cost.read).");
        query.Validate();

        var q = repo.Query<MaintenanceTicket>().Where(t => !t.IsArchived);

        if (query.FromUtc.HasValue)
            q = q.Where(t => t.ResolvedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            q = q.Where(t => t.ResolvedAtUtc <= query.ToUtc.Value);
        if (query.DepartmentId.HasValue || query.AssetTypeId.HasValue)
        {
            var assets = repo.Query<Asset>().Where(a => !a.IsArchived);
            if (query.DepartmentId.HasValue)
                assets = assets.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);
            if (query.AssetTypeId.HasValue)
                assets = assets.Where(a => a.AssetTypeId == query.AssetTypeId.Value);
            var assetIds = assets.Select(a => a.Id);
            q = q.Where(t => assetIds.Contains(t.AssetId));
        }

        var groupBy = query.GroupBy.ToLowerInvariant();
        if (groupBy == "month")
        {
            var groups = await repo.ListAsync(q.GroupBy(t => new {
                    Year = t.ResolvedAtUtc.HasValue ? t.ResolvedAtUtc.Value.Year : 0,
                    Month = t.ResolvedAtUtc.HasValue ? t.ResolvedAtUtc.Value.Month : 0
                }).Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Total = g.Sum(t => t.ActualCost ?? 0m), Missing = g.Count(t => t.ActualCost == null) }), ct);
            var items = groups.OrderBy(g => g.Year).ThenBy(g => g.Month).Select(g => new CostGroupItemDto(
                $"{g.Year:D4}-{g.Month:D2}", g.Year == 0 ? "Chưa có ngày hoàn thành" : $"Tháng {g.Year:D4}-{g.Month:D2}",
                g.Count, decimal.Round(g.Total, 2), g.Missing)).ToList();
            return new("VND", decimal.Round(groups.Sum(g => g.Total), 2), items, groups.Sum(g => g.Count), groups.Sum(g => g.Missing));
        }

        IQueryable<GroupCountRow> queryGroups;
        if (groupBy == "department")
        {
            queryGroups = from ticket in q
                          join asset in repo.Query<Asset>() on ticket.AssetId equals asset.Id
                          join dept in repo.Query<Department>() on asset.OwningDepartmentId equals dept.Id
                          group ticket by new { dept.Id, dept.Name } into g
                          select new GroupCountRow { Id = g.Key.Id, Label = g.Key.Name, Count = g.Count(), TotalCost = g.Sum(t => t.ActualCost ?? 0m), MissingCostCount = g.Count(t => t.ActualCost == null) };
        }
        else
        {
            queryGroups = from ticket in q
                          join asset in repo.Query<Asset>() on ticket.AssetId equals asset.Id
                          join type in repo.Query<AssetType>() on asset.AssetTypeId equals type.Id
                          group ticket by new { type.Id, type.Name } into g
                          select new GroupCountRow { Id = g.Key.Id, Label = g.Key.Name, Count = g.Count(), TotalCost = g.Sum(t => t.ActualCost ?? 0m), MissingCostCount = g.Count(t => t.ActualCost == null) };
        }
        var rows = await repo.ListAsync(queryGroups, ct);
        var result = rows.OrderByDescending(g => g.TotalCost).ThenBy(g => g.Id).Select(g =>
            new CostGroupItemDto(g.Id.ToString(), g.Label, g.Count, decimal.Round(g.TotalCost, 2), g.MissingCostCount)).ToList();
        return new("VND", decimal.Round(rows.Sum(g => g.TotalCost), 2), result, rows.Sum(g => g.Count), rows.Sum(g => g.MissingCostCount));
    }

    // ---------------------------------------------------------
    // EP-083: GET /reports/warranty-expiration
    // ---------------------------------------------------------
    public async Task<PagedResponse<WarrantyReportRowDto>> GetWarrantyExpirationReportAsync(
        WarrantyExpiryReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportAssets, "Yêu cầu quyền xem báo cáo bảo hành (reports.assets.read).");
        query.Validate();

        var today = DateOnly.FromDateTime(NowUtc);
        var q = repo.Query<Asset>().Where(a => !a.IsArchived && a.WarrantyEndDate != null);

        if (query.FromDate.HasValue)
            q = q.Where(a => a.WarrantyEndDate >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(a => a.WarrantyEndDate <= query.ToDate.Value);
        if (query.DepartmentId.HasValue)
            q = q.Where(a => a.OwningDepartmentId == query.DepartmentId.Value);
        if (query.AssetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == query.AssetTypeId.Value);

        var totalItems = await repo.CountAsync(q, ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        var assets = await repo.ListAsync(
            q.OrderBy(a => a.WarrantyEndDate)
             .ThenBy(a => a.Id)
             .Skip((query.Page - 1) * query.PageSize)
             .Take(query.PageSize), ct);

        var deptIds = assets.Select(a => a.OwningDepartmentId).Distinct().ToList();
        var typeIds = assets.Select(a => a.AssetTypeId).Distinct().ToList();

        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)).Select(d => new IdNameRow { Id = d.Id, Name = d.Name }), ct))
            .ToDictionary(d => d.Id, d => d.Name);
        var types = (await repo.ListAsync(repo.Query<AssetType>().Where(t => typeIds.Contains(t.Id)).Select(t => new IdNameRow { Id = t.Id, Name = t.Name }), ct))
            .ToDictionary(t => t.Id, t => t.Name);

        var items = assets.Select(a =>
        {
            var daysRemaining = a.WarrantyEndDate.HasValue ? a.WarrantyEndDate.Value.DayNumber - today.DayNumber : 0;
            return new WarrantyReportRowDto(
                a.Id,
                a.AssetCode,
                a.Name,
                types.TryGetValue(a.AssetTypeId, out var tn) ? tn : "—",
                departments.TryGetValue(a.OwningDepartmentId, out var dn) ? dn : "—",
                a.WarrantyEndDate,
                daysRemaining,
                daysRemaining < 0
            );
        }).ToList();

        return new PagedResponse<WarrantyReportRowDto>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    // ---------------------------------------------------------
    // EP-084: GET /reports/license-expiration
    // ---------------------------------------------------------
    public async Task<PagedResponse<LicenseExpiryRowDto>> GetLicenseExpirationReportAsync(
        LicenseExpiryReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportLicenses, "Yêu cầu quyền xem báo cáo bản quyền (reports.licenses.read).");
        query.Validate();

        var now = NowUtc;
        var q = repo.Query<SoftwareLicense>().Where(l => l.IsActive);

        if (query.SoftwareId.HasValue)
            q = q.Where(l => l.SoftwareId == query.SoftwareId.Value);
        if (query.FromUtc.HasValue)
            q = q.Where(l => l.ExpiresAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            q = q.Where(l => l.ExpiresAtUtc <= query.ToUtc.Value);
        if (query.Status == "Expired") q = q.Where(l => l.ExpiresAtUtc < now);
        if (query.Status == "Expiring") q = q.Where(l => l.ExpiresAtUtc >= now && l.ExpiresAtUtc <= now.AddDays(30));
        if (query.Status == "Valid") q = q.Where(l => l.ExpiresAtUtc == null || l.ExpiresAtUtc > now.AddDays(30));

        var totalItems = await repo.CountAsync(q, ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        var allocations = repo.Query<LicenseAssignment>();
        var licenses = await repo.ListAsync(
            q.OrderBy(l => l.ExpiresAtUtc)
             .ThenBy(l => l.Id)
             .Skip((query.Page - 1) * query.PageSize)
             .Take(query.PageSize).Select(l => new {
                 l.Id, l.SoftwareId, l.LicenseCode, l.LicenseType, l.TotalQuantity, l.LicenseKeyLast4, l.ExpiresAtUtc,
                 ActiveSeats = allocations.Count(a => a.SoftwareLicenseId == l.Id && a.RevokedAtUtc == null && !a.IsArchived)
             }), ct);

        var softwareIds = licenses.Select(l => l.SoftwareId).Distinct().ToList();

        var softwares = (await repo.ListAsync(repo.Query<Software>().Where(s => softwareIds.Contains(s.Id)).Select(s => new IdNameRow { Id = s.Id, Name = s.Name }), ct))
            .ToDictionary(s => s.Id, s => s.Name);

        var items = licenses.Select(l =>
        {
            var daysRemaining = l.ExpiresAtUtc.HasValue ? (int)Math.Floor((l.ExpiresAtUtc.Value - now).TotalDays) : 0;
            var activeSeats = l.ActiveSeats;

            return new LicenseExpiryRowDto(
                l.Id,
                l.SoftwareId,
                softwares.TryGetValue(l.SoftwareId, out var sn) ? sn : "—",
                l.LicenseCode,
                l.LicenseType,
                l.TotalQuantity,
                activeSeats,
                l.LicenseKeyLast4,
                l.ExpiresAtUtc,
                daysRemaining,
                daysRemaining < 0
            );
        }).ToList();

        return new PagedResponse<LicenseExpiryRowDto>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    // ---------------------------------------------------------
    // EP-085: GET /reports/replacement-recommendations
    // ---------------------------------------------------------
    public async Task<PagedResponse<ReplacementReportRowDto>> GetReplacementReportAsync(
        ReplacementReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportReplacement, "Yêu cầu quyền xem báo cáo đề xuất thay thế (reports.replacement.read).");
        query.Validate();

        var canReadCost = actor.Has(Permissions.DashboardCost) || actor.Has(Permissions.AssetCost) || actor.Has(Permissions.ReportCost);
        var q = repo.Query<ReplacementRecommendation>().Where(r => !r.IsArchived);

        if (query.GeneratedFromUtc.HasValue)
            q = q.Where(r => r.RecommendedAtUtc >= query.GeneratedFromUtc.Value);
        if (query.GeneratedToUtc.HasValue)
            q = q.Where(r => r.RecommendedAtUtc <= query.GeneratedToUtc.Value);
        if (!string.IsNullOrWhiteSpace(query.Priority))
            q = q.Where(r => r.Priority == query.Priority);
        if (!string.IsNullOrWhiteSpace(query.Disposition))
            q = q.Where(r => r.Disposition == query.Disposition);
        if (query.PlannedYear.HasValue)
            q = q.Where(r => r.PlannedReplacementYear == query.PlannedYear.Value);

        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            q = q.Where(r => deptAssetIds.Contains(r.AssetId));
        }

        var totalItems = await repo.CountAsync(q, ct);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        var recs = await repo.ListAsync(
            q.OrderByDescending(r => r.RecommendedAtUtc)
             .ThenByDescending(r => r.Id)
             .Skip((query.Page - 1) * query.PageSize)
             .Take(query.PageSize), ct);

        var assetIds = recs.Select(r => r.AssetId).Distinct().ToList();
        var ruleIds = recs.Select(r => r.ReplacementRuleId).Distinct().ToList();

        var assets = await repo.ListAsync(repo.Query<Asset>().Where(a => assetIds.Contains(a.Id)).Select(a => new { a.Id, a.AssetCode, a.Name, a.OwningDepartmentId }), ct);
        var assetMap = assets.ToDictionary(a => a.Id, a => (AssetCode: a.AssetCode, Name: a.Name, OwningDepartmentId: a.OwningDepartmentId));

        var deptIds = assets.Select(a => a.OwningDepartmentId).Distinct().ToList();
        var departments = (await repo.ListAsync(repo.Query<Department>().Where(d => deptIds.Contains(d.Id)).Select(d => new IdNameRow { Id = d.Id, Name = d.Name }), ct))
            .ToDictionary(d => d.Id, d => d.Name);

        var rules = (await repo.ListAsync(repo.Query<ReplacementRule>().Where(ru => ruleIds.Contains(ru.Id)).Select(ru => new { ru.Id, ru.Code }), ct))
            .ToDictionary(ru => ru.Id, ru => ru.Code);

        var items = recs.Select(r =>
        {
            var assetInfo = assetMap.TryGetValue(r.AssetId, out var ai) ? ai : (AssetCode: "—", Name: "—", OwningDepartmentId: 0L);
            var deptName = departments.TryGetValue(assetInfo.OwningDepartmentId, out var dn) ? dn : "—";
            var ruleCode = rules.TryGetValue(r.ReplacementRuleId, out var rc) ? rc : "—";

            return new ReplacementReportRowDto(
                r.Id,
                r.AssetId,
                assetInfo.AssetCode,
                assetInfo.Name,
                deptName,
                ruleCode,
                r.Priority,
                r.Disposition,
                r.Score,
                canReadCost ? r.EstimatedReplacementCost : null,
                r.PlannedReplacementYear,
                r.IsCurrent,
                r.RecommendedAtUtc
            );
        }).ToList();

        return new PagedResponse<ReplacementReportRowDto>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    // ---------------------------------------------------------
    // EP-086: GET /reports/replacement-budget
    // ---------------------------------------------------------
    public async Task<ReplacementBudgetReportDto> GetReplacementBudgetReportAsync(
        ReplacementBudgetReportQuery query, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ReportCost, "Yêu cầu quyền xem ngân sách dự toán (reports.cost.read).");
        query.Validate();

        var q = repo.Query<ReplacementRecommendation>()
                    .Where(r => !r.IsArchived && r.IsCurrent &&
                                (r.Disposition == "ACTIVE" || r.Disposition == "PLANNED") &&
                                r.PlannedReplacementYear == (short)query.Year);

        if (query.DepartmentId.HasValue)
        {
            var deptAssetIds = repo.Query<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == query.DepartmentId.Value).Select(a => a.Id);
            q = q.Where(r => deptAssetIds.Contains(r.AssetId));
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
            q = q.Where(r => r.Priority == query.Priority);

        var recRows = await repo.ListAsync(q.GroupBy(r => r.Priority).Select(g => new {
            Priority = g.Key, Count = g.Count(), Total = g.Sum(r => r.EstimatedReplacementCost ?? 0m),
            Missing = g.Count(r => r.EstimatedReplacementCost == null)
        }), ct);
        var totalCount = recRows.Sum(r => r.Count);
        var totalBudget = recRows.Sum(r => r.Total);
        var missingCount = recRows.Sum(r => r.Missing);
        var byPriority = recRows
            .Select(g => new PriorityBudgetDto(
                g.Priority,
                g.Count,
                decimal.Round(g.Total, 2)
            )).OrderByDescending(x => x.TotalCost).ToList();

        return new ReplacementBudgetReportDto(
            query.Year,
            "VND",
            totalCount,
            decimal.Round(totalBudget, 2),
            missingCount,
            byPriority
        );
    }
}
