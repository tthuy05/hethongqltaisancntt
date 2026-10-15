using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Tests Thuy's existing Asset API guard using fixture-owned ticket snapshots.
// These are not MaintenanceService/Controller or owner workflow acceptance tests.
[Collection("isolated-neon")]
public sealed class AssetMaintenanceGuardApiTests(MvpFixture fixture)
{
    private sealed class Actor(long id) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/isolated-fixture/asset-maintenance-guard";
        public bool Has(string permission) => false;
    }
    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }
    private async Task<(AssetDto Asset, long Ticket)> PrepareAsync(HttpClient client, string status, bool archived = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        using var created = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var initial = (await created.Content.ReadFromJsonAsync<AssetDto>())!;
        var me = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var actor = new Actor(me.Id); var audit = new AuditWriter(db, actor); var repo = provider.GetRequiredService<IRepository>();
        var ticketId = await provider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            await repo.LockAssetAsync(initial.Id, default);
            var asset = await db.Set<Asset>().SingleAsync(a => a.Id == initial.Id);
            var histories = new List<AssetStatusHistory>(); var now = DateTime.UtcNow;
            var started = status is "IN_PROGRESS" or "RESOLVED" or "FAILED";
            if (started)
            {
                histories.Add(AssetStatusTransitions.Apply(asset, "MAINTENANCE", AssetStatusChangeSource.Maintenance, actor, now, "Isolated guard snapshot"));
                if (status != "IN_PROGRESS") histories.Add(AssetStatusTransitions.Apply(asset, status == "FAILED" ? "BROKEN" : "IN_STOCK",
                    AssetStatusChangeSource.Maintenance, actor, now, "Isolated terminal snapshot"));
            }
            var ticket = new MaintenanceTicket
            {
                AssetId = asset.Id, TicketCode = "GUARD" + Guid.NewGuid().ToString("N"), Title = "Isolated Asset guard fixture",
                Description = "Synthetic ticket snapshot for Asset guard tests only",
                RequestedByUserId = me.Id, AssignedToUserId = started ? me.Id : null, OpenedAtUtc = now,
                Status = status, IsArchived = archived, StartedAtUtc = started ? now : null,
                ResolvedAtUtc = status is "RESOLVED" or "FAILED" ? now : null,
                Resolution = status is "RESOLVED" or "FAILED" ? "Synthetic terminal snapshot" : null
            };
            repo.Add(ticket); foreach (var history in histories) repo.Add(history); await repo.SaveAsync(default);
            audit.Record("fixture.asset-workflow-guard", ticket, me.Id, "USER");
            foreach (var history in histories) audit.Record("fixture.asset-workflow-guard", history, me.Id, "USER");
            if (histories.Count > 0) audit.Record("fixture.asset-workflow-guard", asset, me.Id, "USER");
            await repo.SaveAsync(default); return ticket.Id;
        });
        return ((await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{initial.Id}"))!, ticketId);
    }
    private static async Task<HttpResponseMessage> ArchiveAsync(HttpClient client, AssetDto asset)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/assets/{asset.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", '"' + asset.RowVersion + '"'); return await client.SendAsync(request);
    }
    private static Task<HttpResponseMessage> RetireAsync(HttpClient client, AssetDto asset) =>
        client.PatchAsJsonAsync($"/api/v1/assets/{asset.Id}/status", new StatusRequest("Retired", "Isolated guard check", asset.RowVersion));
    private async Task<int> HistoriesAsync(long id)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        return await db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == id);
    }
    private static async Task ErrorAsync(HttpResponseMessage response, string code)
    {
        using (response) { Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()); }
    }
    [NeonFact]
    public async Task Pending_ticket_blocks_retire_and_archive_without_changing_asset_version_or_history()
    {
        using var client = await fixture.ClientAsync(); var prepared = await PrepareAsync(client, "PENDING"); var before = await HistoriesAsync(prepared.Asset.Id);
        await ErrorAsync(await RetireAsync(client, prepared.Asset), "ASSET_ACTIVE_WORKFLOW");
        await ErrorAsync(await ArchiveAsync(client, prepared.Asset), "ASSET_ACTIVE_WORKFLOW");
        var current = (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{prepared.Asset.Id}"))!;
        Assert.Equal(prepared.Asset.RowVersion, current.RowVersion); Assert.Equal("InStock", current.Status); Assert.False(current.IsArchived);
        Assert.Equal(before, await HistoriesAsync(current.Id));
        using var free = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); var unrelated = (await free.Content.ReadFromJsonAsync<AssetDto>())!;
        using var allowed = await RetireAsync(client, unrelated); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
    [NeonFact]
    public async Task In_progress_ticket_blocks_archive_and_asset_status_remains_maintenance_after_denied_calls()
    {
        using var client = await fixture.ClientAsync(); var prepared = await PrepareAsync(client, "IN_PROGRESS"); var before = await HistoriesAsync(prepared.Asset.Id);
        Assert.Equal("Maintenance", prepared.Asset.Status);
        await ErrorAsync(await ArchiveAsync(client, prepared.Asset), "ASSET_ACTIVE_WORKFLOW");
        await ErrorAsync(await RetireAsync(client, prepared.Asset), "ASSET_STATUS_CONFLICT");
        var current = (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{prepared.Asset.Id}"))!;
        Assert.Equal(prepared.Asset.RowVersion, current.RowVersion); Assert.Equal("Maintenance", current.Status); Assert.False(current.IsArchived);
        Assert.Equal(before, await HistoriesAsync(current.Id));
    }
    [NeonFact]
    public async Task Every_terminal_ticket_snapshot_with_or_without_archive_allows_retire_and_soft_archive_preserving_history()
    {
        using var client = await fixture.ClientAsync();
        foreach (var status in new[] { "RESOLVED", "FAILED", "CANCELLED" }) foreach (var archived in new[] { false, true })
        {
            var prepared = await PrepareAsync(client, status, archived); var before = await HistoriesAsync(prepared.Asset.Id);
            using var retired = await RetireAsync(client, prepared.Asset); Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
            var current = (await retired.Content.ReadFromJsonAsync<AssetDto>())!; Assert.Equal("Retired", current.Status);
            using var result = await ArchiveAsync(client, current); Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
            var asset = await db.Set<Asset>().SingleAsync(a => a.Id == current.Id); Assert.True(asset.IsArchived); Assert.Equal("RETIRED", asset.CurrentStatus);
            var ticket = await db.Set<MaintenanceTicket>().SingleAsync(t => t.Id == prepared.Ticket);
            Assert.Equal(status, ticket.Status); Assert.Equal(archived, ticket.IsArchived);
            Assert.Equal(before + 1, await db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == asset.Id));
            Assert.Equal(1, await db.Set<AuditLog>().CountAsync(a => a.EntityType == "Asset" && a.EntityId == asset.Id.ToString() && a.Action == "assets.archive" && a.Outcome == "SUCCESS"));
        }
    }
}
