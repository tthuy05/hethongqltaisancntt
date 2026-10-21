using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Application.Reporting;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class ReportsCompletionIntegrationTests(MvpFixture fixture, ITestOutputHelper output)
{
    private sealed class Reader(long id, bool financial = true) : IActor
    {
        public long? UserId => id; public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "GET"; public string? Path => "/isolated-fixture/reports";
        public bool Has(string p) => p == Permissions.ReportAssets || financial && p == Permissions.ReportCost;
    }
    private sealed class Counter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Reads++; return ValueTask.FromResult(result); }
    }
    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }

    [NeonFact]
    public async Task Inventory_filters_dates_sort_paging_cost_permissions_and_bounded_query_count_match_PostgreSql()
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var prefix = "RPT" + Guid.NewGuid().ToString("N"); var ids = new List<long>();
        foreach (var purchased in new DateOnly?[] { new(2026, 1, 1), new(2026, 10, 21), new(2026, 10, 22), null })
        {
            var request = fixture.Asset(); request.Name = prefix + ids.Count; request.PurchaseDate = purchased;
            using var response = await client.PostAsJsonAsync("/api/v1/assets", request); response.EnsureSuccessStatusCode();
            ids.Add((await response.Content.ReadFromJsonAsync<AssetDto>())!.Id);
        }
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(source);
        var counter = new Counter();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(source.Database.GetConnectionString(),
            p => p.MigrationsHistoryTable("ef_migrations_history", "public")).AddInterceptors(counter).Options;
        await using var db = new AppDbContext(options); VerifyTarget(db);
        var reader = new ReportQueryService(new EfRepository(db), new Reader(user.Id));
        var query = new AssetInventoryReportQuery { Keyword = prefix, DepartmentId = fixture.DepartmentId, AssetTypeId = fixture.TypeId,
            Status = "IN_STOCK", PurchasedFrom = new(2026, 1, 1), PurchasedTo = new(2026, 10, 21), PageSize = 1,
            SortBy = "purchaseDate", SortDirection = "asc" };
        var first = await reader.GetAssetInventoryReportAsync(query);
        Assert.Equal(2, first.TotalItems); Assert.Equal(2, first.TotalPages); Assert.Equal(ids[0], Assert.Single(first.Items).Id);
        Assert.Equal(4, counter.Reads); // count + bounded page + department/type projections; no per-row queries.
        query.Page = 2; var second = await reader.GetAssetInventoryReportAsync(query);
        Assert.Equal(ids[1], Assert.Single(second.Items).Id);
        query.SortDirection = "desc"; query.Page = 1;
        Assert.Equal(ids[1], Assert.Single((await reader.GetAssetInventoryReportAsync(query)).Items).Id);
        var nonfinancial = new ReportQueryService(new EfRepository(db), new Reader(user.Id, false));
        Assert.Null(Assert.Single((await nonfinancial.GetAssetInventoryReportAsync(query)).Items).PurchaseCost);
        query.SortBy = "purchaseCost";
        var denied = await Assert.ThrowsAsync<BusinessException>(() => nonfinancial.GetAssetInventoryReportAsync(query));
        Assert.Equal(403, denied.Status);
        query.SortBy = "purchaseDate";
        await reader.GetAssetInventoryReportAsync(query); // Warmup. Small synthetic diagnostic, not a load/SLA claim.
        var timings = new List<double>();
        for (var n = 0; n < 10; n++)
        {
            counter.Reads = 0; var watch = Stopwatch.StartNew(); await reader.GetAssetInventoryReportAsync(query); watch.Stop();
            Assert.Equal(4, counter.Reads); timings.Add(watch.Elapsed.TotalMilliseconds);
        }
        output.WriteLine("inventory-diagnostic|concurrency=1|warmup=1|samples=10|matching=2|pageSize=1|reads=4|ms=" +
            string.Join(",", timings.Select(x => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))));
        // Representative inventory page predicate/order, not the full service's
        // four-query SQL. EXPLAIN ANALYZE only SELECTs from the verified isolated DB.
        await db.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
            SELECT id, asset_code, name FROM public.assets
            WHERE NOT is_archived AND owning_department_id=@department AND asset_type_id=@type
              AND name LIKE @prefix AND current_status='IN_STOCK'
              AND purchase_date >= @from AND purchase_date <= @to
            ORDER BY purchase_date DESC, id LIMIT 1
            """, (NpgsqlConnection)db.Database.GetDbConnection());
        command.Parameters.AddWithValue("department", fixture.DepartmentId); command.Parameters.AddWithValue("type", fixture.TypeId);
        command.Parameters.AddWithValue("prefix", prefix + "%"); command.Parameters.AddWithValue("from", new DateOnly(2026, 1, 1));
        command.Parameters.AddWithValue("to", new DateOnly(2026, 10, 21));
        using var plan = System.Text.Json.JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!);
        var root = plan.RootElement[0]; Assert.Equal(1d, root.GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
        output.WriteLine("representative-page-explain|" + root.GetRawText());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [NeonFact]
    public async Task Group_counts_and_filtered_cost_window_reconcile_with_database_without_financial_group_leak()
    {
        using var client = await fixture.ClientAsync(); var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var repo = scope.ServiceProvider.GetRequiredService<IRepository>();
        var reader = new ReportQueryService(repo, new Reader(user.Id, false));
        var q = new ReportGroupQuery { DepartmentId = fixture.DepartmentId, AssetTypeId = fixture.TypeId, Status = "IN_STOCK" };
        var expected = await db.Set<Asset>().CountAsync(a => !a.IsArchived && a.OwningDepartmentId == fixture.DepartmentId && a.AssetTypeId == fixture.TypeId && a.CurrentStatus == "IN_STOCK");
        foreach (var groups in new[] { await reader.GetAssetsByDepartmentReportAsync(q), await reader.GetAssetsByTypeReportAsync(q), await reader.GetAssetStatusReportAsync(q) })
        { Assert.Equal(expected, groups.Sum(g => g.Count)); Assert.All(groups, g => Assert.Null(g.Value)); }
        var from = DateTime.UtcNow.AddDays(-30); var to = DateTime.UtcNow;
        var costs = new ReportQueryService(repo, new Reader(user.Id));
        var assets = db.Set<Asset>().Where(a => !a.IsArchived && a.OwningDepartmentId == fixture.DepartmentId && a.AssetTypeId == fixture.TypeId);
        var tickets = db.Set<MaintenanceTicket>().Where(t => !t.IsArchived && t.ResolvedAtUtc >= from && t.ResolvedAtUtc <= to && assets.Select(a => a.Id).Contains(t.AssetId));
        foreach (var group in new[] { "month", "type", "department" })
        {
            var result = await costs.GetMaintenanceCostReportAsync(new() { FromUtc = from, ToUtc = to, DepartmentId = fixture.DepartmentId, AssetTypeId = fixture.TypeId, GroupBy = group });
            Assert.Equal(await tickets.CountAsync(), result.TicketCount);
            Assert.Equal(await tickets.CountAsync(t => t.ActualCost == null), result.MissingCostCount);
            Assert.Equal(await tickets.SumAsync(t => t.ActualCost ?? 0m), result.TotalCost);
            Assert.Equal(result.TotalCost, result.Groups.Sum(g => g.TotalCost));
        }
        Assert.False(db.ChangeTracker.HasChanges());
    }
}
