using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit.Abstractions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class DashboardQueryIntegrationTests(MvpFixture fixture, ITestOutputHelper output)
{
    private sealed class Reader(long id) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "GET";
        public string? Path => "/isolated-fixture/dashboard";
        public bool Has(string permission) => permission is Permissions.DashboardRead or Permissions.DashboardCost or Permissions.LicenseRead
            or Permissions.ReportAssets or Permissions.ReportCost or Permissions.ReportLicenses;
    }

    [NeonFact]
    public async Task PostgreSql_translates_dashboard_group_counts_and_bounded_alert_queries()
    {
        using var client = await fixture.ClientAsync();
        var me = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        using var created = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);

        var repo = scope.ServiceProvider.GetRequiredService<IRepository>();
        var reader = new DashboardQueryService(repo, new Reader(me.Id));
        var count = await db.Set<Asset>().CountAsync(asset => !asset.IsArchived);
        Assert.True(count > 0);

        foreach (var groupBy in new[] { "status", "department", "type" })
        {
            var groups = await reader.GetAssetGroupsAsync(new DashboardAssetGroupQuery { GroupBy = groupBy });
            Assert.Equal(count, groups.Sum(group => group.Count));
        }

        var reports = new ReportQueryService(repo, new Reader(me.Id));
        var byDepartment = await reports.GetAssetsByDepartmentReportAsync(new ReportGroupQuery());
        var byType = await reports.GetAssetsByTypeReportAsync(new ReportGroupQuery());
        Assert.Equal(count, byDepartment.Sum(group => group.Count));
        Assert.Equal(count, byType.Sum(group => group.Count));

        var alerts = await reader.GetAlertsAsync(new DashboardAlertsQuery { DaysAhead = 30 });
        var licensePage = await reports.GetLicenseExpirationReportAsync(new());
        Assert.All(licensePage.Items, item => Assert.True(item.ActiveSeats >= 0));
        Assert.True(alerts.ExpiringWarrantiesCount >= alerts.ExpiringWarranties.Count);
        Assert.NotNull(alerts.ExpiringLicenses);
        Assert.True(alerts.ExpiringLicensesCount >= alerts.ExpiringLicenses!.Count);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [NeonFact]
    public async Task PostgreSql_cost_aggregates_match_database_totals_and_record_small_dataset_timings()
    {
        using var client = await fixture.ClientAsync();
        var me = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
        var repo = scope.ServiceProvider.GetRequiredService<IRepository>(); var actor = new Reader(me.Id);
        using var created = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!;
        var writer = new AuditWriter(db, actor);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
            var now = DateTime.UtcNow;
            var rule = new ReplacementRule { Code = "FIX" + Guid.NewGuid().ToString("N"), Name = "Synthetic reporting rule", MinimumAgeMonths = 1,
                Priority = 1, EffectiveFromUtc = now.AddDays(-1), EstimatedUnitCost = 250m };
            repo.Add(rule); await repo.SaveAsync(default); writer.Record("fixture.reporting", rule, me.Id, "USER");
            var rec = new ReplacementRecommendation { AssetId = asset.Id, ReplacementRuleId = rule.Id, Reason = "Synthetic reporting fixture",
                Priority = "HIGH", Disposition = "ACTIVE", IsCurrent = true, PlannedReplacementYear = 2027,
                EstimatedReplacementCost = 250m, EvaluationSnapshotJson = "{}", RecommendedAtUtc = now };
            repo.Add(rec); await repo.SaveAsync(default); writer.Record("fixture.reporting", rec, me.Id, "USER");
            var ticket = new MaintenanceTicket { AssetId = asset.Id, TicketCode = "FIX" + Guid.NewGuid().ToString("N"), Title = "Synthetic cost fixture",
                Description = "Fixture", Status = "RESOLVED", RequestedByUserId = me.Id, AssignedToUserId = me.Id,
                OpenedAtUtc = now.AddHours(-1), StartedAtUtc = now.AddMinutes(-30), ResolvedAtUtc = now, ActualCost = 25.50m, Resolution = "Fixture" };
            repo.Add(ticket); await repo.SaveAsync(default); writer.Record("fixture.reporting", ticket, me.Id, "USER");
            await repo.SaveAsync(default); return true;
        });
        var dashboard = new DashboardQueryService(repo, actor); var reports = new ReportQueryService(repo, actor);
        var cost = await dashboard.GetCostsAsync(new() { DepartmentId = fixture.DepartmentId });
        var assets = db.Set<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == fixture.DepartmentId);
        Assert.Equal(await assets.SumAsync(a => a.PurchaseCost ?? 0m), cost.TotalAssetValue);
        var budget = await reports.GetReplacementBudgetReportAsync(new() { Year = 2027, DepartmentId = fixture.DepartmentId });
        var recs = db.Set<ReplacementRecommendation>().Where(r => !r.IsArchived && r.IsCurrent && (r.Disposition == "ACTIVE" || r.Disposition == "PLANNED") && r.PlannedReplacementYear == 2027 && assets.Select(a => a.Id).Contains(r.AssetId));
        Assert.Equal(await recs.CountAsync(), budget.TotalRecommendations);
        Assert.True(budget.TotalRecommendations > 0);
        Assert.Equal(await recs.SumAsync(r => r.EstimatedReplacementCost ?? 0m), budget.TotalBudget);
        Assert.Equal(await recs.CountAsync(r => r.EstimatedReplacementCost == null), budget.MissingEstimateCount);
        foreach (var group in new[] { "month", "department", "type" })
        {
            var report = await reports.GetMaintenanceCostReportAsync(new() { GroupBy = group, DepartmentId = fixture.DepartmentId });
            var tickets = db.Set<MaintenanceTicket>().Where(t => !t.IsArchived && t.ActualCost != null && assets.Select(a => a.Id).Contains(t.AssetId));
            Assert.Equal(await tickets.SumAsync(t => t.ActualCost ?? 0m), report.TotalCost);
            Assert.Equal(report.TotalCost, report.Groups.Sum(g => g.TotalCost));
        }
        output.WriteLine($"diagnostic-only|target=isolated|assets={await assets.CountAsync()}|tickets={await db.Set<MaintenanceTicket>().CountAsync()}|recommendations={await db.Set<ReplacementRecommendation>().CountAsync()}|concurrency=1|warmup=1|samples=10");
        async Task Measure(string name, Func<Task> read)
        {
            await read(); var samples = new List<double>();
            for (var n = 0; n < 10; n++) { var clock = Stopwatch.StartNew(); await read(); samples.Add(clock.Elapsed.TotalMilliseconds); }
            output.WriteLine($"{name}|ms=" + string.Join(",", samples.Select(x => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))));
        }
        await Measure("dashboard-costs", async () => { await dashboard.GetCostsAsync(new() { DepartmentId = fixture.DepartmentId }); });
        await Measure("maintenance-cost", async () => { await reports.GetMaintenanceCostReportAsync(new() { DepartmentId = fixture.DepartmentId }); });
        await Measure("replacement-budget", async () => { await reports.GetReplacementBudgetReportAsync(new() { Year = 2027, DepartmentId = fixture.DepartmentId }); });
        Assert.False(db.ChangeTracker.HasChanges());
    }
}
