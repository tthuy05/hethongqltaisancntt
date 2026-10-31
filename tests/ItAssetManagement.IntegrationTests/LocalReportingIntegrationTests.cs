using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalReportingIntegrationTests
{
    private sealed class Reader(bool financial = false, string? otherModuleCost = null) : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId => Guid.Empty;
        public string? Method => "GET";
        public string? Path => "/isolated/reports";
        public bool Has(string permission) => permission is Permissions.DashboardRead or Permissions.ReportAssets or
            Permissions.ReportAssignments or Permissions.ReportMaintenance or Permissions.ReportLicenses or
            Permissions.ReportReplacement || financial && permission is Permissions.ReportCost or Permissions.DashboardCost || permission == otherModuleCost;
    }

    [LocalPostgresFact]
    public async Task All_eleven_report_queries_and_dashboard_execute_on_PostgreSql_without_changing_runtime_grants()
    {
        await using var db = LocalSequenceAuthenticationTests.Database();
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var department = new Department { Code = "RPT" + suffix, Name = "Report fixture" };
        var type = new AssetType { Code = "RPT" + suffix, Name = "Report fixture", DefaultUsefulLifeMonths = 36 };
        var audit = new AuditWriter(db, new Reader());
        await new AuditedUnitOfWork(db).RunAsync(async () => {
            db.AddRange(department, type);
            await db.SaveChangesAsync();
            foreach (var entity in new object[] { department, type }) audit.Record("test.fixture", entity, null, "SYSTEM");
            var asset = new Asset { AssetCode = "RPT" + suffix, Name = "Report fixture", AssetTypeId = type.Id,
                OwningDepartmentId = department.Id, PurchaseCost = 123.45m, PurchaseDate = new(2026, 1, 1),
                WarrantyEndDate = new(2026, 12, 31) };
            db.Add(asset);
            await db.SaveChangesAsync();
            audit.Record("test.fixture", asset, null, "SYSTEM");
            await db.SaveChangesAsync();
            return true;
        });
        var repo = new EfRepository(db);
        var report = new ReportQueryService(repo, new Reader(true));
        var inventory = await report.GetAssetInventoryReportAsync(new() { DepartmentId = department.Id }); // EP-076
        Assert.Equal(123.45m, Assert.Single(inventory.Items).PurchaseCost);
        Assert.Single(await report.GetAssetsByDepartmentReportAsync(new() { DepartmentId = department.Id })); // EP-077
        Assert.Single(await report.GetAssetsByTypeReportAsync(new() { DepartmentId = department.Id })); // EP-078
        Assert.Single(await report.GetAssetStatusReportAsync(new() { DepartmentId = department.Id })); // EP-079
        Assert.Empty((await report.GetAssignmentReportAsync(new() { DepartmentId = department.Id })).Items); // EP-080
        Assert.Empty((await report.GetMaintenanceReportAsync(new() { AssetId = inventory.Items[0].Id })).Items); // EP-081
        Assert.Equal(0, (await report.GetMaintenanceCostReportAsync(new() { DepartmentId = department.Id })).TicketCount); // EP-082
        Assert.Single((await report.GetWarrantyExpirationReportAsync(new() { DepartmentId = department.Id,
            FromDate = new(2026, 1, 1), ToDate = new(2027, 1, 1) })).Items); // EP-083
        await report.GetLicenseExpirationReportAsync(new()); // EP-084: no license credentials in this fixture.
        Assert.Empty((await report.GetReplacementReportAsync(new() { DepartmentId = department.Id })).Items); // EP-085
        await report.GetReplacementBudgetReportAsync(new() { DepartmentId = department.Id, Year = 2027 }); // EP-086
        var masked = new ReportQueryService(repo, new Reader());
        Assert.Null(Assert.Single((await masked.GetAssetInventoryReportAsync(new() { DepartmentId = department.Id })).Items).PurchaseCost);
        foreach (var otherModuleCost in new[] { Permissions.AssetCost, Permissions.DashboardCost })
        {
            var scoped = new ReportQueryService(repo, new Reader(otherModuleCost: otherModuleCost));
            Assert.Null(Assert.Single((await scoped.GetAssetInventoryReportAsync(new() { DepartmentId = department.Id })).Items).PurchaseCost);
            Assert.All(await scoped.GetAssetsByDepartmentReportAsync(new() { DepartmentId = department.Id }), row => Assert.Null(row.Value));
            Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => scoped.GetAssetInventoryReportAsync(new() { DepartmentId = department.Id, SortBy = "purchaseCost" }))).Status);
        }
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => masked.GetMaintenanceCostReportAsync(new()))).Status);
        var dashboard = new DashboardQueryService(repo, new Reader(true));
        Assert.Equal(1, (await dashboard.GetSummaryAsync(new() { DepartmentId = department.Id })).TotalAssets);
        Assert.Single(await dashboard.GetAssetGroupsAsync(new() { DepartmentId = department.Id, GroupBy = "department" }));
        await dashboard.GetAlertsAsync(new() { DepartmentId = department.Id });
        await dashboard.GetCostsAsync(new() { DepartmentId = department.Id });
    }
}
