using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class DashboardAndReportingTests
{
    [Fact]
    public async Task Replacement_report_paging_has_id_tie_breaker_when_timestamps_match()
    {
        var now = DateTime.UtcNow;
        var recs = new[] { new ReplacementRecommendation { Id = 1, RecommendedAtUtc = now }, new ReplacementRecommendation { Id = 2, RecommendedAtUtc = now } };
        var reader = new ReportQueryService(new InMemoryRepo(recs), new TestActor(1, Permissions.ReportReplacement));
        var first = await reader.GetReplacementReportAsync(new() { Page = 1, PageSize = 1 });
        var second = await reader.GetReplacementReportAsync(new() { Page = 2, PageSize = 1 });
        Assert.Equal(2, Assert.Single(first.Items).Id); Assert.Equal(1, Assert.Single(second.Items).Id);
    }
    [Fact]
    public async Task Dashboard_cost_completeness_distinguishes_unknown_from_known_zero_and_respects_ytd_scope()
    {
        var now = new FixedClock().GetUtcNow().UtcDateTime;
        var assets = new[] {
            new Asset { Id = 1, OwningDepartmentId = 1, PurchaseCost = null },
            new Asset { Id = 2, OwningDepartmentId = 1, PurchaseCost = 0m },
            new Asset { Id = 3, OwningDepartmentId = 2, PurchaseCost = null },
            new Asset { Id = 4, OwningDepartmentId = 1, PurchaseCost = null, IsArchived = true }
        };
        var tickets = new[] {
            new MaintenanceTicket { Id = 1, AssetId = 1, ResolvedAtUtc = now.AddDays(-1), ActualCost = null },
            new MaintenanceTicket { Id = 2, AssetId = 2, ResolvedAtUtc = now.AddDays(-1), ActualCost = 0m },
            new MaintenanceTicket { Id = 3, AssetId = 1, ResolvedAtUtc = now.AddDays(1), ActualCost = 999m },
            new MaintenanceTicket { Id = 4, AssetId = 1, ResolvedAtUtc = now.AddYears(-1), ActualCost = null },
            new MaintenanceTicket { Id = 5, AssetId = 3, ResolvedAtUtc = now.AddDays(-1), ActualCost = null },
            new MaintenanceTicket { Id = 6, AssetId = 1, ResolvedAtUtc = now.AddDays(-1), ActualCost = null, IsArchived = true }
        };
        var repo = new InMemoryRepo(assets, tickets);
        var reader = new DashboardQueryService(repo, new TestActor(1, Permissions.DashboardCost), new FixedClock());
        var result = await reader.GetCostsAsync(new() { DepartmentId = 1 });
        Assert.Equal(0m, result.TotalAssetValue); Assert.Equal(2, result.AssetCount); Assert.Equal(1, result.MissingPurchaseCostCount);
        Assert.Equal(0m, result.MaintenanceCostYtd); Assert.Equal(2, result.MaintenanceTicketCountYtd); Assert.Equal(1, result.MissingMaintenanceCostCountYtd);
        Assert.Equal(2, Assert.Single(result.MonthlyMaintenanceCosts).TicketCount);
        Assert.Equal(1, Assert.Single(result.MonthlyMaintenanceCosts).MissingCostCount);
        var empty = await reader.GetCostsAsync(new() { DepartmentId = 99 });
        Assert.Equal(0, empty.AssetCount); Assert.Equal(0, empty.MissingPurchaseCostCount); Assert.Equal(0, empty.MaintenanceTicketCountYtd); Assert.Empty(empty.MonthlyMaintenanceCosts);
    }

    [Theory]
    [InlineData("month")]
    [InlineData("department")]
    [InlineData("type")]
    public async Task Maintenance_cost_report_counts_missing_costs_and_preserves_group_totals(string group)
    {
        var assets = new[] { new Asset { Id = 1, OwningDepartmentId = 1, AssetTypeId = 1 } };
        var now = new FixedClock().GetUtcNow().UtcDateTime;
        var tickets = new[] {
            new MaintenanceTicket { Id = 1, AssetId = 1, ActualCost = 0m, ResolvedAtUtc = now },
            new MaintenanceTicket { Id = 2, AssetId = 1, ActualCost = null, ResolvedAtUtc = now },
            new MaintenanceTicket { Id = 3, AssetId = 1, ActualCost = null },
            new MaintenanceTicket { Id = 4, AssetId = 1, ActualCost = 25.5m, ResolvedAtUtc = now },
            new MaintenanceTicket { Id = 5, AssetId = 1, ActualCost = null, IsArchived = true }
        };
        var repo = new InMemoryRepo(assets, tickets, new[] { new Department { Id = 1, Name = "IT" } }, new[] { new AssetType { Id = 1, Name = "Laptop" } });
        var reader = new ReportQueryService(repo, new TestActor(1, Permissions.ReportCost));
        var report = await reader.GetMaintenanceCostReportAsync(new() { GroupBy = group });
        Assert.Equal(25.5m, report.TotalCost); Assert.Equal(4, report.TicketCount); Assert.Equal(2, report.MissingCostCount);
        Assert.Equal(report.TicketCount, report.Groups.Sum(g => g.TicketCount));
        Assert.Equal(report.MissingCostCount, report.Groups.Sum(g => g.MissingCostCount));
        var dated = await reader.GetMaintenanceCostReportAsync(new() { GroupBy = group, FromUtc = now, ToUtc = now });
        Assert.Equal(3, dated.TicketCount); Assert.Equal(1, dated.MissingCostCount); Assert.Equal(25.5m, dated.TotalCost);
        var empty = await reader.GetMaintenanceCostReportAsync(new() { GroupBy = group, DepartmentId = 99 });
        Assert.Empty(empty.Groups); Assert.Equal(0, empty.TicketCount); Assert.Equal(0, empty.MissingCostCount);
    }

    [Theory]
    [InlineData("department")]
    [InlineData("type")]
    [InlineData("status")]
    public async Task Group_reports_respect_both_department_and_type_filters(string group)
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets);
        var reader = new ReportQueryService(repo, new TestActor(1, Permissions.ReportAssets));
        var query = new ReportGroupQuery { DepartmentId = 1, AssetTypeId = 20, Status = "MAINTENANCE" };
        var rows = group == "department" ? await reader.GetAssetsByDepartmentReportAsync(query)
            : group == "type" ? await reader.GetAssetsByTypeReportAsync(query) : await reader.GetAssetStatusReportAsync(query);
        Assert.Equal(1, Assert.Single(rows).Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Report_financial_fields_require_report_cost_even_with_other_module_cost_grants(bool assetCost, bool dashboardCost)
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.rules, data.recs);
        var grants = new List<string> { Permissions.ReportAssets, Permissions.ReportMaintenance, Permissions.ReportReplacement };
        if (assetCost) grants.Add(Permissions.AssetCost);
        if (dashboardCost) grants.Add(Permissions.DashboardCost);
        var service = new ReportQueryService(repo, new TestActor(1, grants.ToArray()));
        var inventory = await service.GetAssetInventoryReportAsync(new());
        Assert.NotEmpty(inventory.Items);
        Assert.All(inventory.Items, row => Assert.Null(row.PurchaseCost));
        Assert.All(await service.GetAssetsByDepartmentReportAsync(new()), row => Assert.Null(row.Value));
        Assert.All(await service.GetAssetsByTypeReportAsync(new()), row => Assert.Null(row.Value));
        Assert.All(await service.GetAssetStatusReportAsync(new()), row => Assert.Null(row.Value));
        var maintenance = await service.GetMaintenanceReportAsync(new());
        Assert.NotEmpty(maintenance.Items);
        Assert.All(maintenance.Items, row => Assert.Null(row.Cost));
        var recommendations = await service.GetReplacementReportAsync(new());
        Assert.NotEmpty(recommendations.Items);
        Assert.All(recommendations.Items, row => Assert.Null(row.EstimatedCost));
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAssetInventoryReportAsync(new() { SortBy = "purchaseCost" }))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetMaintenanceCostReportAsync(new()))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetReplacementBudgetReportAsync(new() { Year = 2027 }))).Status);
        var financial = new ReportQueryService(repo, new TestActor(1, [..grants, Permissions.ReportCost]));
        Assert.Contains((await financial.GetAssetInventoryReportAsync(new())).Items, row => row.PurchaseCost is > 0);
        Assert.Contains((await financial.GetMaintenanceReportAsync(new())).Items, row => row.Cost is > 0);
        Assert.Contains((await financial.GetReplacementReportAsync(new())).Items, row => row.EstimatedCost is > 0);
    }

    private sealed class TestActor(long? userId, params string[] permissions) : IActor
    {
        public long? UserId => userId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "GET";
        public string? Path => "/api/v1/dashboard";
        public bool Has(string permission) => permissions.Contains(permission);
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    }

    [Theory]
    [InlineData("dashboard")]
    [InlineData("month")]
    [InlineData("department")]
    [InlineData("type")]
    [InlineData("budget")]
    public async Task Cost_queries_materialize_groups_and_preserve_complete_totals(string operation)
    {
        var assets = Enumerable.Range(1, 51).Select(id => new Asset { Id = id, PurchaseCost = 100m, OwningDepartmentId = 1, AssetTypeId = 1 }).ToArray();
        var tickets = Enumerable.Range(1, 51).Select(id => new MaintenanceTicket { Id = id, AssetId = id, Status = "RESOLVED", ActualCost = 10.25m, ResolvedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) }).ToArray();
        var recs = Enumerable.Range(1, 51).Select(id => new ReplacementRecommendation { Id = id, AssetId = id, IsCurrent = true, Priority = "HIGH", Disposition = "ACTIVE", EstimatedReplacementCost = id == 1 ? null : 100m, PlannedReplacementYear = 2027 }).ToArray();
        var repo = new InMemoryRepo(assets, tickets, recs, new[] { new Department { Id = 1, Name = "IT" } }, new[] { new AssetType { Id = 1, Name = "Laptop" } });
        var actor = new TestActor(1, Permissions.DashboardCost, Permissions.ReportCost);
        if (operation == "dashboard")
        {
            var result = await new DashboardQueryService(repo, actor, new FixedClock()).GetCostsAsync(new());
            Assert.Equal(5100m, result.TotalAssetValue); Assert.Equal(522.75m, result.MaintenanceCostYtd);
            Assert.Equal(5000m, result.TotalBudgetForecast); Assert.Equal(1, result.MissingEstimateCount);
        }
        else if (operation == "budget")
        {
            var result = await new ReportQueryService(repo, actor).GetReplacementBudgetReportAsync(new() { Year = 2027 });
            Assert.Equal(51, result.TotalRecommendations); Assert.Equal(5000m, result.TotalBudget); Assert.Equal(1, result.MissingEstimateCount);
        }
        else
        {
            var result = await new ReportQueryService(repo, actor).GetMaintenanceCostReportAsync(new() { GroupBy = operation });
            Assert.Equal(522.75m, result.TotalCost); Assert.Equal(result.TotalCost, result.Groups.Sum(g => g.TotalCost));
        }
        Assert.True(repo.MaxRowsMaterialized <= 1, $"Materialized {repo.MaxRowsMaterialized} detail rows instead of one aggregate.");
    }

    private sealed class InMemoryRepo(params Array[] data) : IRepository
    {
        public int MaxRowsMaterialized { get; private set; }
        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            var match = data.OfType<T[]>().FirstOrDefault();
            return (match ?? Array.Empty<T>()).AsQueryable();
        }

        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct)
        {
            var rows = q.ToList();
            MaxRowsMaterialized = Math.Max(MaxRowsMaterialized, rows.Count);
            return Task.FromResult(rows);
        }
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public void Add<T>(T entity) where T : class => throw new NotSupportedException();
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task LockAsync(string resource, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) =>
            q.Where(a => a.AssetCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                         a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static (Department[] depts, AssetType[] types, Asset[] assets, MaintenanceTicket[] tickets, Software[] softwares, SoftwareLicense[] licenses, LicenseAssignment[] licenseAssignments, ReplacementRule[] rules, ReplacementRecommendation[] recs) CreateSampleDataset()
    {
        var depts = new[]
        {
            new Department { Id = 1, Code = "IT", Name = "Công nghệ thông tin" },
            new Department { Id = 2, Code = "HR", Name = "Nhân sự" }
        };

        var types = new[]
        {
            new AssetType { Id = 10, Code = "LAPTOP", Name = "Máy tính xách tay" },
            new AssetType { Id = 20, Code = "MONITOR", Name = "Màn hình" }
        };

        var assets = new[]
        {
            new Asset { Id = 101, AssetCode = "AST-001", Name = "Dell Latitude 5420", AssetTypeId = 10, OwningDepartmentId = 1, CurrentStatus = "IN_USE", PurchaseCost = 25000000m, PurchaseDate = new DateOnly(2025, 1, 15), WarrantyEndDate = new DateOnly(2026, 11, 1), CreatedAtUtc = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new Asset { Id = 102, AssetCode = "AST-002", Name = "ThinkPad T14", AssetTypeId = 10, OwningDepartmentId = 1, CurrentStatus = "IN_USE", PurchaseCost = 28000000m, PurchaseDate = new DateOnly(2025, 2, 1), WarrantyEndDate = new DateOnly(2026, 12, 1), CreatedAtUtc = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Asset { Id = 103, AssetCode = "AST-003", Name = "MacBook Pro M2", AssetTypeId = 10, OwningDepartmentId = 2, CurrentStatus = "IN_STOCK", PurchaseCost = 45000000m, PurchaseDate = new DateOnly(2025, 3, 1), WarrantyEndDate = new DateOnly(2026, 10, 20), CreatedAtUtc = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Asset { Id = 104, AssetCode = "AST-004", Name = "Dell UltraSharp 27", AssetTypeId = 20, OwningDepartmentId = 1, CurrentStatus = "MAINTENANCE", PurchaseCost = 12000000m, PurchaseDate = new DateOnly(2025, 4, 1), WarrantyEndDate = new DateOnly(2025, 10, 1), CreatedAtUtc = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Asset { Id = 105, AssetCode = "AST-005", Name = "LG 24 Inch", AssetTypeId = 20, OwningDepartmentId = 2, CurrentStatus = "IN_STOCK", PurchaseCost = null, PurchaseDate = new DateOnly(2025, 5, 1), WarrantyEndDate = null, CreatedAtUtc = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc) },
            // Archived asset: must be excluded by default
            new Asset { Id = 106, AssetCode = "AST-006", Name = "Old Retired Laptop", AssetTypeId = 10, OwningDepartmentId = 1, CurrentStatus = "RETIRED", PurchaseCost = 15000000m, IsArchived = true, CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        };

        var tickets = new[]
        {
            new MaintenanceTicket { Id = 1, TicketCode = "TCK-001", AssetId = 104, Title = "Màn hình bị sọc", Priority = "HIGH", Status = "IN_PROGRESS", OpenedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), DueAtUtc = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc) },
            new MaintenanceTicket { Id = 2, TicketCode = "TCK-002", AssetId = 101, Title = "Thay pin", Priority = "LOW", Status = "RESOLVED", OpenedAtUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), ResolvedAtUtc = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc), ActualCost = 1500000m }
        };

        var softwares = new[]
        {
            new Software { Id = 1, Code = "MS-OFFICE", Name = "Microsoft Office 365" }
        };

        var licenses = new[]
        {
            new SoftwareLicense { Id = 501, SoftwareId = 1, LicenseCode = "LIC-001", LicenseType = "VOLUME", TotalQuantity = 10, LicenseKeyLast4 = "9988", IsActive = true, ExpiresAtUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc) }
        };

        var licenseAssignments = new[]
        {
            new LicenseAssignment { Id = 1, SoftwareLicenseId = 501, AssignedUserId = 7, AssignedAtUtc = DateTime.UtcNow }
        };

        var rules = new[]
        {
            new ReplacementRule { Id = 1, Code = "RULE-BATTERY", Name = "Thay thế pin chai" }
        };

        var recs = new[]
        {
            new ReplacementRecommendation { Id = 1, AssetId = 101, ReplacementRuleId = 1, Priority = "HIGH", Disposition = "ACTIVE", IsCurrent = true, EstimatedReplacementCost = 2000000m, PlannedReplacementYear = 2027, RecommendedAtUtc = DateTime.UtcNow },
            // Superseded recommendation for same asset: must NOT be counted
            new ReplacementRecommendation { Id = 2, AssetId = 101, ReplacementRuleId = 1, Priority = "LOW", Disposition = "SUPERSEDED", IsCurrent = false, EstimatedReplacementCost = 1000000m, PlannedReplacementYear = 2027, RecommendedAtUtc = DateTime.UtcNow.AddDays(-10) }
        };

        return (depts, types, assets, tickets, softwares, licenses, licenseAssignments, rules, recs);
    }

    [Fact]
    public async Task Dashboard_summary_enforces_dashboard_read_permission()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.licenses, data.recs);

        var unauth = new DashboardQueryService(repo, new TestActor(null));
        var missingGrant = new DashboardQueryService(repo, new TestActor(1L, "assets.read"));
        var allowed = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead));

        await Assert.ThrowsAsync<BusinessException>(() => unauth.GetSummaryAsync(new DashboardSummaryQuery()));
        await Assert.ThrowsAsync<BusinessException>(() => missingGrant.GetSummaryAsync(new DashboardSummaryQuery()));

        var summary = await allowed.GetSummaryAsync(new DashboardSummaryQuery());
        Assert.NotNull(summary);
        Assert.Null(summary.ActiveLicenses);
    }

    [Fact]
    public async Task Dashboard_summary_computes_exact_counts_and_excludes_archived()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.licenses, data.recs);
        var service = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead, Permissions.LicenseRead));

        var summary = await service.GetSummaryAsync(new DashboardSummaryQuery());

        // 5 active assets (AST-006 is archived and must NOT be counted)
        Assert.Equal(5, summary.TotalAssets);
        Assert.Equal(2, summary.InUse); // AST-001, AST-002
        Assert.Equal(1, summary.Maintenance); // AST-004
        Assert.Equal(2, summary.InStock); // AST-003, AST-005
        Assert.Equal(1, summary.OpenTickets); // TCK-001 is IN_PROGRESS (TCK-002 is RESOLVED)
        Assert.Equal(1, summary.ActiveLicenses); // LIC-001
        Assert.Equal(1, summary.PendingReplacements); // Rec 1 is ACTIVE and IsCurrent (Rec 2 is SUPERSEDED)

        // Sum of byStatus must match TotalAssets
        Assert.Equal(5, summary.ByStatus.Sum(s => s.Count));
        Assert.All(summary.ByStatus, group => Assert.Null(group.Value));
    }

    [Fact]
    public async Task Dashboard_summary_respects_department_filter()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.licenses, data.recs);
        var service = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead));

        // Dept 1 has AST-001, AST-002, AST-004 (AST-006 is archived)
        var summaryDept1 = await service.GetSummaryAsync(new DashboardSummaryQuery { DepartmentId = 1 });
        Assert.Equal(3, summaryDept1.TotalAssets);
        Assert.Equal(2, summaryDept1.InUse);
        Assert.Equal(1, summaryDept1.Maintenance);
        Assert.Equal(0, summaryDept1.InStock);

        // Dept 2 has AST-003, AST-005
        var summaryDept2 = await service.GetSummaryAsync(new DashboardSummaryQuery { DepartmentId = 2 });
        Assert.Equal(2, summaryDept2.TotalAssets);
        Assert.Equal(0, summaryDept2.InUse);
        Assert.Equal(2, summaryDept2.InStock);
    }

    [Fact]
    public async Task Dashboard_asset_groups_masks_cost_for_support_role_and_shows_for_cost_grant()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets);

        var supportService = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead));
        var adminService = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead, Permissions.DashboardCost));

        var supportGroups = await supportService.GetAssetGroupsAsync(new DashboardAssetGroupQuery { GroupBy = "status" });
        Assert.NotEmpty(supportGroups);
        Assert.All(supportGroups, g => Assert.Null(g.Value)); // Cost is completely masked

        var adminGroups = await adminService.GetAssetGroupsAsync(new DashboardAssetGroupQuery { GroupBy = "status" });
        Assert.NotEmpty(adminGroups);
        Assert.Contains(adminGroups, g => g.Value.HasValue && g.Value > 0); // Cost is present
    }

    [Theory]
    [InlineData("department", 2)]
    [InlineData("type", 1)]
    public async Task Dashboard_asset_groups_materialize_aggregate_rows_instead_of_every_asset(string groupBy, int expectedGroups)
    {
        var assets = Enumerable.Range(1, 51).Select(id => new Asset
        {
            Id = id,
            AssetCode = $"AST-{id:000}",
            Name = $"Fixture {id}",
            AssetTypeId = 10,
            OwningDepartmentId = id % 2 == 0 ? 1 : 2,
            CurrentStatus = "IN_STOCK",
            PurchaseCost = 100m
        }).ToArray();
        var departments = new[]
        {
            new Department { Id = 1, Code = "IT", Name = "IT" },
            new Department { Id = 2, Code = "HR", Name = "HR" }
        };
        var types = new[] { new AssetType { Id = 10, Code = "DEVICE", Name = "Device" } };
        var repo = new InMemoryRepo(assets, departments, types);
        var service = new DashboardQueryService(repo, new TestActor(7, Permissions.DashboardRead, Permissions.DashboardCost));

        var groups = await service.GetAssetGroupsAsync(new DashboardAssetGroupQuery { GroupBy = groupBy });

        Assert.Equal(expectedGroups, groups.Count);
        Assert.Equal(51, groups.Sum(group => group.Count));
        Assert.Equal(5100m, groups.Sum(group => group.Value));
        Assert.True(repo.MaxRowsMaterialized <= expectedGroups, $"Materialized {repo.MaxRowsMaterialized} rows for {expectedGroups} groups.");

        var withoutCost = await new DashboardQueryService(repo, new TestActor(7, Permissions.DashboardRead))
            .GetAssetGroupsAsync(new DashboardAssetGroupQuery { GroupBy = groupBy });
        Assert.All(withoutCost, group => Assert.Null(group.Value));
    }

    [Fact]
    public async Task Dashboard_alerts_enforces_license_read_boundary()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.softwares, data.licenses, data.licenseAssignments);

        var supportService = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead));
        var adminService = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead, Permissions.LicenseRead));

        // Support doesn't have licenses.read -> license alerts must be null
        var supportAlerts = await supportService.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 30 });
        Assert.Null(supportAlerts.ExpiringLicenses);
        Assert.Equal(0, supportAlerts.ExpiringLicensesCount);
        Assert.NotEmpty(supportAlerts.ExpiringWarranties);

        // Admin has licenses.read -> license alerts populated with masked key
        var adminAlerts = await adminService.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 30 });
        Assert.NotNull(adminAlerts.ExpiringLicenses);
        Assert.Single(adminAlerts.ExpiringLicenses);
        Assert.Equal("9988", adminAlerts.ExpiringLicenses[0].LicenseKeyLast4); // Masked to last 4
    }

    [Fact]
    public async Task Dashboard_warranty_alert_count_is_not_limited_to_first_50_rows()
    {
        var assets = Enumerable.Range(1, 51).Select(id => new Asset
        {
            Id = id,
            AssetCode = $"AST-{id:000}",
            Name = $"Fixture {id}",
            OwningDepartmentId = 1,
            WarrantyEndDate = new DateOnly(2026, 10, 20)
        }).ToArray();
        var departments = new[] { new Department { Id = 1, Code = "IT", Name = "IT" } };
        var service = new DashboardQueryService(
            new InMemoryRepo(assets, departments),
            new TestActor(7, Permissions.DashboardRead),
            new FixedTimeProvider());

        var result = await service.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 30 });

        Assert.Equal(51, result.ExpiringWarrantiesCount);
        Assert.Equal(50, result.ExpiringWarranties.Count);
        Assert.Null(result.ExpiringLicenses);
    }

    [Fact]
    public async Task Dashboard_license_alert_count_is_complete_while_materialized_rows_are_bounded()
    {
        var licenses = Enumerable.Range(1, 51).Select(id => new SoftwareLicense
        {
            Id = id,
            SoftwareId = 1,
            LicenseCode = $"LIC-{id:000}",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            IsActive = true,
            ExpiresAtUtc = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        }).ToArray();
        var software = new[] { new Software { Id = 1, Code = "FIXTURE", Name = "Fixture software" } };
        var repo = new InMemoryRepo(licenses, software);
        var service = new DashboardQueryService(repo, new TestActor(7, Permissions.DashboardRead, Permissions.LicenseRead), new FixedTimeProvider());

        var result = await service.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 30 });

        Assert.Equal(51, result.ExpiringLicensesCount);
        Assert.Equal(50, result.ExpiringLicenses!.Count);
        Assert.True(repo.MaxRowsMaterialized <= 50, $"Materialized {repo.MaxRowsMaterialized} rows for a 50-row alert preview.");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task Dashboard_costs_requires_dashboard_cost_read_grant()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.recs);

        var supportService = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead));
        var financeService = new DashboardQueryService(repo, new TestActor(1L, Permissions.DashboardRead, Permissions.DashboardCost));

        await Assert.ThrowsAsync<BusinessException>(() => supportService.GetCostsAsync(new DashboardCostsQuery()));

        var costs = await financeService.GetCostsAsync(new DashboardCostsQuery());
        Assert.Equal("VND", costs.Currency);
        Assert.True(costs.TotalAssetValue > 0);
        Assert.Equal(1500000m, costs.MaintenanceCostYtd); // TCK-002 resolved in 2026
        Assert.Equal(2000000m, costs.TotalBudgetForecast); // Only Rec 1 (ACTIVE & IsCurrent), Rec 2 is SUPERSEDED
    }

    [Fact]
    public async Task Dashboard_queries_reject_invalid_parameters()
    {
        var service = new DashboardQueryService(new InMemoryRepo(), new TestActor(1L, Permissions.DashboardRead, Permissions.DashboardCost));

        // Invalid department
        await Assert.ThrowsAsync<BusinessException>(() => service.GetSummaryAsync(new DashboardSummaryQuery { DepartmentId = 0 }));
        await Assert.ThrowsAsync<BusinessException>(() => service.GetSummaryAsync(new DashboardSummaryQuery { DepartmentId = -1 }));

        // Invalid groupBy
        await Assert.ThrowsAsync<BusinessException>(() => service.GetAssetGroupsAsync(new DashboardAssetGroupQuery { GroupBy = "invalid" }));

        // Invalid daysAhead
        await Assert.ThrowsAsync<BusinessException>(() => service.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 0 }));
        await Assert.ThrowsAsync<BusinessException>(() => service.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 400 }));

        // Invalid date range
        await Assert.ThrowsAsync<BusinessException>(() => service.GetCostsAsync(new DashboardCostsQuery
        {
            FromUtc = new DateTime(2026, 10, 10),
            ToUtc = new DateTime(2026, 10, 1)
        }));
    }

    [Fact]
    public async Task Reports_service_enforces_individual_read_permissions()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.depts, data.types, data.assets, data.tickets, data.softwares, data.licenses, data.licenseAssignments, data.rules, data.recs);

        var unauth = new ReportQueryService(repo, new TestActor(null));
        var assetReader = new ReportQueryService(repo, new TestActor(1L, Permissions.ReportAssets));
        var costReader = new ReportQueryService(repo, new TestActor(1L, Permissions.ReportCost));

        // Asset inventory requires ReportAssets
        await Assert.ThrowsAsync<BusinessException>(() => unauth.GetAssetInventoryReportAsync(new AssetInventoryReportQuery()));
        var inventory = await assetReader.GetAssetInventoryReportAsync(new AssetInventoryReportQuery());
        Assert.Equal(5, inventory.TotalItems);

        // Maintenance cost requires ReportCost
        await Assert.ThrowsAsync<BusinessException>(() => assetReader.GetMaintenanceCostReportAsync(new MaintenanceCostReportQuery()));
        var costReport = await costReader.GetMaintenanceCostReportAsync(new MaintenanceCostReportQuery());
        Assert.Equal(1500000m, costReport.TotalCost);

        // Replacement budget requires ReportCost
        await Assert.ThrowsAsync<BusinessException>(() => assetReader.GetReplacementBudgetReportAsync(new ReplacementBudgetReportQuery { Year = 2027 }));
        var budgetReport = await costReader.GetReplacementBudgetReportAsync(new ReplacementBudgetReportQuery { Year = 2027 });
        Assert.Equal(2000000m, budgetReport.TotalBudget);
        Assert.Equal(1, budgetReport.TotalRecommendations); // Only Rec 1 (ACTIVE & IsCurrent)
    }

    [Theory]
    [InlineData("department", 2)]
    [InlineData("type", 1)]
    public async Task Reports_asset_groups_materialize_only_aggregate_rows(string groupBy, int expectedGroups)
    {
        var assets = Enumerable.Range(1, 51).Select(id => new Asset
        {
            Id = id,
            AssetCode = $"AST-{id:000}",
            Name = $"Fixture {id}",
            AssetTypeId = 10,
            OwningDepartmentId = id % 2 == 0 ? 1 : 2,
            CurrentStatus = "IN_STOCK",
            PurchaseCost = 100m
        }).ToArray();
        var departments = new[]
        {
            new Department { Id = 1, Code = "IT", Name = "IT" },
            new Department { Id = 2, Code = "HR", Name = "HR" }
        };
        var types = new[] { new AssetType { Id = 10, Code = "DEVICE", Name = "Device" } };
        var repo = new InMemoryRepo(assets, departments, types);
        var reader = new ReportQueryService(repo, new TestActor(7, Permissions.ReportAssets, Permissions.ReportCost));

        var groups = groupBy == "department"
            ? await reader.GetAssetsByDepartmentReportAsync(new ReportGroupQuery())
            : await reader.GetAssetsByTypeReportAsync(new ReportGroupQuery());

        Assert.Equal(expectedGroups, groups.Count);
        Assert.Equal(51, groups.Sum(group => group.Count));
        Assert.Equal(5100m, groups.Sum(group => group.Value));
        Assert.True(repo.MaxRowsMaterialized <= expectedGroups, $"Materialized {repo.MaxRowsMaterialized} rows for {expectedGroups} report groups.");

        var masked = new ReportQueryService(repo, new TestActor(7, Permissions.ReportAssets));
        var redacted = groupBy == "department"
            ? await masked.GetAssetsByDepartmentReportAsync(new ReportGroupQuery())
            : await masked.GetAssetsByTypeReportAsync(new ReportGroupQuery());
        Assert.All(redacted, group => Assert.Null(group.Value));
    }

    [Fact]
    public async Task Reports_inventory_cannot_sort_by_hidden_purchase_cost_without_cost_grant()
    {
        var assets = new[]
        {
            new Asset { Id = 1, AssetCode = "AST-001", Name = "Low", AssetTypeId = 10, OwningDepartmentId = 1, PurchaseCost = 100m },
            new Asset { Id = 2, AssetCode = "AST-002", Name = "High", AssetTypeId = 10, OwningDepartmentId = 1, PurchaseCost = 200m }
        };
        var departments = new[] { new Department { Id = 1, Code = "IT", Name = "IT" } };
        var types = new[] { new AssetType { Id = 10, Code = "DEVICE", Name = "Device" } };
        var repo = new InMemoryRepo(assets, departments, types);
        var query = new AssetInventoryReportQuery { SortBy = "purchaseCost", SortDirection = "desc" };

        var restricted = new ReportQueryService(repo, new TestActor(7, Permissions.ReportAssets));
        var denial = await Assert.ThrowsAsync<BusinessException>(() => restricted.GetAssetInventoryReportAsync(query));
        Assert.Equal(403, denial.Status);

        var authorized = new ReportQueryService(repo, new TestActor(7, Permissions.ReportAssets, Permissions.ReportCost));
        var result = await authorized.GetAssetInventoryReportAsync(query);
        Assert.Equal(new[] { "AST-002", "AST-001" }, result.Items.Select(item => item.AssetCode));
        Assert.Equal(new decimal?[] { 200m, 100m }, result.Items.Select(item => item.PurchaseCost));
    }

    [Fact]
    public async Task Reports_license_expiration_always_masks_full_key()
    {
        var data = CreateSampleDataset();
        var repo = new InMemoryRepo(data.softwares, data.licenses, data.licenseAssignments);
        var service = new ReportQueryService(repo, new TestActor(1L, Permissions.ReportLicenses));

        var result = await service.GetLicenseExpirationReportAsync(new LicenseExpiryReportQuery());
        Assert.Single(result.Items);
        Assert.Equal("9988", result.Items[0].LicenseKeyLast4);
    }
}
