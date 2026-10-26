using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Mvp;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalMaintenanceCompletionTests
{
    private const string Path = "/api/v1/maintenance-tickets";
    [LocalPostgresFact]
    public async Task Real_Support_sees_only_assigned_tickets_and_cannot_read_or_write_financial_fields()
    {
        await using var factory = LocalAssignmentCompletionTests.Factory(maintenance: true);
        await LocalAssignmentCompletionTests.SeedAsync(factory);
        using var admin = await LocalAssignmentCompletionTests.LoginAsync(factory);
        var refs = await LocalAssignmentCompletionTests.References(factory);
        var actor = await LocalAssignmentCompletionTests.ActorAsync(factory, "TECHNICAL_SUPPORT");
        using var support = await LocalAssignmentCompletionTests.LoginAsync(factory, actor.Email);
        var asset = await LocalAssignmentCompletionTests.AssetAsync(admin, refs.Department, refs.Type);
        using var created = await support.PostAsJsonAsync(Path, new { assetId = asset.Id, description = "Support reported fault" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        Assert.Null(ticket.EstimatedCost); Assert.Null(ticket.ActualCost);
        Assert.Equal(HttpStatusCode.NotFound, (await support.GetAsync(Path + $"/{ticket.Id}")).StatusCode);
        using var assigned = await admin.PostAsJsonAsync(Path + $"/{ticket.Id}/assign", new { technicianId = actor.Id, rowVersion = ticket.RowVersion, note = "Assign support" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode); ticket = (await assigned.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        var secondAsset = await LocalAssignmentCompletionTests.AssetAsync(admin, refs.Department, refs.Type);
        using var other = await admin.PostAsJsonAsync(Path, new { assetId = secondAsset.Id, description = "Private other ticket", estimatedCost = 987654.32m });
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        var otherTicket = (await other.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await support.GetAsync(Path + $"/{otherTicket.Id}/history")).StatusCode);
        var list = await support.GetFromJsonAsync<PagedResponse<MaintenanceDto>>(Path);
        Assert.Equal(ticket.Id, Assert.Single(list!.Items).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await support.GetAsync(Path + "/technicians")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostAsJsonAsync(Path + $"/{ticket.Id}/cancel", new { rowVersion = ticket.RowVersion, reason = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PutAsJsonAsync(Path + $"/{ticket.Id}", new { rowVersion = ticket.RowVersion, actualCost = (decimal?)null, note = "Denied financial clear" })).StatusCode);
        using var started = await support.PostAsJsonAsync(Path + $"/{ticket.Id}/start", new { rowVersion = ticket.RowVersion, note = "Start repair" });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode); ticket = (await started.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        using var deniedFinish = await support.PostAsJsonAsync(Path + $"/{ticket.Id}/resolve", new { rowVersion = ticket.RowVersion, resolution = "Done", actualCost = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, deniedFinish.StatusCode);
        using var resolved = await support.PostAsJsonAsync(Path + $"/{ticket.Id}/resolve", new { rowVersion = ticket.RowVersion, resolution = "Done" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Null((await resolved.Content.ReadFromJsonAsync<MaintenanceDto>())!.ActualCost);
        var history = await support.GetFromJsonAsync<PagedResponse<MaintenanceHistoryDto>>(Path + $"/{ticket.Id}/history");
        Assert.All(history!.Items, row => Assert.Null(row.Cost));
    }

    [LocalPostgresTheory]
    [InlineData("fail")]
    [InlineData("reassign")]
    [InlineData("cost")]
    public async Task Competing_commands_with_the_same_version_commit_one_event(string mode)
    {
        await using var factory = LocalAssignmentCompletionTests.Factory(maintenance: true);
        await LocalAssignmentCompletionTests.SeedAsync(factory);
        using var admin = await LocalAssignmentCompletionTests.LoginAsync(factory);
        using var second = await LocalAssignmentCompletionTests.LoginAsync(factory);
        var refs = await LocalAssignmentCompletionTests.References(factory);
        var asset = await LocalAssignmentCompletionTests.AssetAsync(admin, refs.Department, refs.Type);
        using var created = await admin.PostAsJsonAsync(Path, new { assetId = asset.Id, description = "Competing command fixture" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        using var assigned = await admin.PostAsJsonAsync(Path + $"/{ticket.Id}/assign", new { rowVersion = ticket.RowVersion, technicianId = refs.Admin, note = "Assign" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode); ticket = (await assigned.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        if (mode != "reassign") {
            using var started = await admin.PostAsJsonAsync(Path + $"/{ticket.Id}/start", new { rowVersion = ticket.RowVersion, note = "Start" });
            Assert.Equal(HttpStatusCode.OK, started.StatusCode); ticket = (await started.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        }
        var before = (await admin.GetFromJsonAsync<PagedResponse<MaintenanceHistoryDto>>(Path + $"/{ticket.Id}/history"))!.TotalItems;
        Task<HttpResponseMessage> first, concurrent;
        if (mode == "fail") {
            first = admin.PostAsJsonAsync(Path + $"/{ticket.Id}/fail", new { rowVersion = ticket.RowVersion, reason = "Unrepairable", cost = 20m });
            concurrent = second.PostAsJsonAsync(Path + $"/{ticket.Id}/resolve", new { rowVersion = ticket.RowVersion, resolution = "Repaired", actualCost = 10m });
        } else if (mode == "cost") {
            first = admin.PutAsJsonAsync(Path + $"/{ticket.Id}", new { rowVersion = ticket.RowVersion, actualCost = 10m, note = "Correct A" });
            concurrent = second.PutAsJsonAsync(Path + $"/{ticket.Id}", new { rowVersion = ticket.RowVersion, actualCost = 20m, note = "Correct B" });
        } else {
            var technician = await LocalAssignmentCompletionTests.ActorAsync(factory, "TECHNICAL_SUPPORT");
            first = admin.PostAsJsonAsync(Path + $"/{ticket.Id}/assign", new { rowVersion = ticket.RowVersion, technicianId = technician.Id, note = "Reassign" });
            concurrent = second.PostAsJsonAsync(Path + $"/{ticket.Id}/start", new { rowVersion = ticket.RowVersion, note = "Start original technician" });
        }
        var replies = await Task.WhenAll(first, concurrent);
        try { Assert.Single(replies, response => response.StatusCode == HttpStatusCode.OK); Assert.Single(replies, response => response.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var reply in replies) reply.Dispose(); }
        var history = await admin.GetFromJsonAsync<PagedResponse<MaintenanceHistoryDto>>(Path + $"/{ticket.Id}/history");
        Assert.Equal(before + 1, history!.TotalItems);
        var current = (await admin.GetFromJsonAsync<MaintenanceDto>(Path + $"/{ticket.Id}"))!;
        if (mode == "cost") { Assert.Equal("IN_PROGRESS", current.Status); Assert.Contains(current.ActualCost, new decimal?[] { 10m, 20m }); }
        if (mode == "fail") Assert.Contains(current.Status, new[] { "RESOLVED", "FAILED" });
        var expectedAsset = current.Status switch { "RESOLVED" => "InStock", "FAILED" => "Broken", "PENDING" => "InStock", _ => "Maintenance" };
        Assert.Equal(expectedAsset, (await admin.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
    }
    private sealed class CancelAuditFailure(IAuditWriter inner) : IAuditWriter
    {
        private int count;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null)
        {
            inner.Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
            if (action == "maintenance.cancel" && ++count == 2) throw new InvalidOperationException("Synthetic cancellation audit failure");
        }
    }

    [LocalPostgresFact]
    public async Task Concurrent_start_has_one_winner_and_cancel_audit_failure_rolls_back_all_changes()
    {
        await using var factory = LocalAssignmentCompletionTests.Factory(maintenance: true);
        await LocalAssignmentCompletionTests.SeedAsync(factory);
        using var client = await LocalAssignmentCompletionTests.LoginAsync(factory);
        var refs = await LocalAssignmentCompletionTests.References(factory);
        var asset = await LocalAssignmentCompletionTests.AssetAsync(client, refs.Department, refs.Type);
        using var created = await client.PostAsJsonAsync(Path, new { assetId = asset.Id, description = "Concurrent repair" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        using var assigned = await client.PostAsJsonAsync(Path + $"/{ticket.Id}/assign", new { rowVersion = ticket.RowVersion, technicianId = refs.Admin, note = "Assign" });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        ticket = (await assigned.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        using var second = await LocalAssignmentCompletionTests.LoginAsync(factory);
        var replies = await Task.WhenAll(client.PostAsJsonAsync(Path + $"/{ticket.Id}/start", new { rowVersion = ticket.RowVersion, note = "Start A" }),
            second.PostAsJsonAsync(Path + $"/{ticket.Id}/start", new { rowVersion = ticket.RowVersion, note = "Start B" }));
        try {
            Assert.Single(replies, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(replies, response => response.StatusCode == HttpStatusCode.Conflict);
        } finally { foreach (var response in replies) response.Dispose(); }
        ticket = (await client.GetFromJsonAsync<MaintenanceDto>(Path + $"/{ticket.Id}"))!;
        var history = await client.GetFromJsonAsync<PagedResponse<MaintenanceHistoryDto>>(Path + $"/{ticket.Id}/history");
        Assert.Equal(3, history!.TotalItems);
        await using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            services.RemoveAll<IAuditWriter>();
            services.AddScoped<IAuditWriter>(sp => new CancelAuditFailure(new AuditWriter(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IActor>())));
        }));
        using var failingClient = await LocalAssignmentCompletionTests.LoginAsync(failing);
        using var failed = await failingClient.PostAsJsonAsync(Path + $"/{ticket.Id}/cancel", new { rowVersion = ticket.RowVersion, reason = "Rollback fixture" });
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var unchanged = (await client.GetFromJsonAsync<MaintenanceDto>(Path + $"/{ticket.Id}"))!;
        Assert.Equal(ticket.RowVersion, unchanged.RowVersion); Assert.Equal("IN_PROGRESS", unchanged.Status);
        Assert.Equal(3, (await client.GetFromJsonAsync<PagedResponse<MaintenanceHistoryDto>>(Path + $"/{ticket.Id}/history"))!.TotalItems);
        Assert.Equal("Maintenance", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
        await using (var scope = factory.Services.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Set<MaintenanceHistory>().AnyAsync(x => x.MaintenanceTicketId == ticket.Id && x.EventType == "CANCELLED"));
        }
        using var cancelled = await client.PostAsJsonAsync(Path + $"/{ticket.Id}/cancel", new { rowVersion = ticket.RowVersion, reason = "Cancel repair" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("InStock", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
    }
    [LocalPostgresFact]
    public async Task Real_JWT_pending_assign_start_resolve_and_history_preserve_asset_assignment()
    {
        await using var factory = LocalAssignmentCompletionTests.Factory(maintenance: true);
        await LocalAssignmentCompletionTests.SeedAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunMaintenancePermissionsAsync());
        using var client = await LocalAssignmentCompletionTests.LoginAsync(factory);
        var refs = await LocalAssignmentCompletionTests.References(factory);
        var asset = await LocalAssignmentCompletionTests.AssetAsync(client, refs.Department, refs.Type);
        await LocalAssignmentCompletionTests.AssignAsync(client, asset.Id, refs.Admin);
        using var created = await client.PostAsJsonAsync(Path, new { assetId = asset.Id, description = "Replace fan", title = "Fan", priority = "HIGH", estimatedCost = 123.45m });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        Assert.Matches("^BT[0-9]{6,}$", ticket.TicketCode);
        Assert.Equal(asset.AssetCode, ticket.AssetCode);
        Assert.Equal("PENDING", ticket.Status);
        Assert.Equal("InUse", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
        ticket = await Command("assign", new { technicianId = refs.Admin, rowVersion = ticket.RowVersion, note = "Assign" });
        ticket = await Command("start", new { rowVersion = ticket.RowVersion, note = "Start work" });
        Assert.Equal("IN_PROGRESS", ticket.Status);
        Assert.Equal("Maintenance", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
        using var stale = await client.PostAsJsonAsync(Path + $"/{ticket.Id}/cancel", new { rowVersion = Convert.ToBase64String(new byte[16]), reason = "Stale" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        ticket = await Command("resolve", new { rowVersion = ticket.RowVersion, resolution = "Replaced", actualCost = 120m });
        Assert.Equal("RESOLVED", ticket.Status); Assert.Equal(120m, ticket.ActualCost);
        Assert.Equal("InUse", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
        var history = await client.GetFromJsonAsync<PagedResponse<MaintenanceHistoryDto>>(Path + $"/{ticket.Id}/history");
        Assert.Equal(4, history!.TotalItems);
        using var terminal = await client.PostAsJsonAsync(Path + $"/{ticket.Id}/start", new { rowVersion = ticket.RowVersion, note = "Again" });
        Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode);

        async Task<MaintenanceDto> Command(string action, object body)
        {
            using var response = await client.PostAsJsonAsync(Path + $"/{ticket.Id}/" + action, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<MaintenanceDto>())!;
        }
    }
}
