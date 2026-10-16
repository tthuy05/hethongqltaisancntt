using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class AssetCostWriteApiTests(MvpFixture fixture)
{
    private void VerifyTarget()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }
    private async Task<HttpClient> NonfinancialWriterAsync(bool readOnly = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8]; var code = "NONFIN_" + suffix.ToUpperInvariant();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
            Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
            Assert.Equal(target, db.Database.GetDbConnection().Database);
            var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var role = new Role { Code = code, Name = readOnly ? "Isolated Asset reader" : "Isolated nonfinancial writer", IsActive = true };
                db.Add(role); audit.Record("fixture.asset-cost", role, null, "SYSTEM"); await db.SaveChangesAsync();
                var codes = readOnly ? new[] { Permissions.AssetRead } : new[] { Permissions.AssetRead, Permissions.AssetCreate, Permissions.AssetUpdate };
                foreach (var definition in await db.Set<Permission>().Where(p => codes.Contains(p.Code)).ToListAsync())
                {
                    var grant = new RolePermission { RoleId = role.Id, PermissionId = definition.Id };
                    db.Add(grant); audit.Record("fixture.asset-cost", grant, null, "SYSTEM");
                }
                await db.SaveChangesAsync(); return true;
            });
        }
        return await fixture.ClientAsync(await fixture.UserAsync("nonfin" + suffix, code));
    }
    private Dictionary<string, object?> Metadata(string name, string? rowVersion = null) => new()
    {
        ["name"] = name, ["assetTypeId"] = fixture.TypeId, ["owningDepartmentId"] = fixture.DepartmentId,
        ["rowVersion"] = rowVersion
    };
    [NeonFact]
    public async Task Asset_only_reader_needs_no_catalog_grants_and_denied_commands_preserve_asset_and_history()
    {
        using var reader = await NonfinancialWriterAsync(readOnly: true); using var admin = await fixture.ClientAsync();
        var me = (await reader.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        Assert.Equal(Permissions.AssetRead, Assert.Single(me.Permissions));
        using var created = await admin.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!; var path = $"/api/v1/assets/{asset.Id}";
        var detail = await reader.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(asset.Id, detail.GetProperty("id").GetInt64()); Assert.False(detail.TryGetProperty("purchasePrice", out _));
        Assert.Equal(asset.AssetType.Name, detail.GetProperty("assetType").GetProperty("name").GetString());
        Assert.Equal(asset.OwningDepartment.Name, detail.GetProperty("owningDepartment").GetProperty("name").GetString());
        var list = await reader.GetFromJsonAsync<JsonElement>("/api/v1/assets?keyword=" + Uri.EscapeDataString(asset.AssetCode));
        var row = Assert.Single(list.GetProperty("items").EnumerateArray()); Assert.Equal(asset.Id, row.GetProperty("id").GetInt64());
        Assert.False(row.TryGetProperty("purchasePrice", out _));
        foreach (var route in new[] { "/api/v1/departments", "/api/v1/asset-types", path + "/status-history" })
        {
            using var denied = await reader.GetAsync(route); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var create = await reader.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var update = await reader.PutAsJsonAsync(path, Metadata("Forbidden metadata", asset.RowVersion)); Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        using var status = await reader.PatchAsJsonAsync(path + "/status", new StatusRequest("Retired", "Forbidden fixture transition", asset.RowVersion)); Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        using var command = new HttpRequestMessage(HttpMethod.Delete, path); command.Headers.TryAddWithoutValidation("If-Match", '"' + asset.RowVersion + '"');
        using var archive = await reader.SendAsync(command); Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
        var after = (await admin.GetFromJsonAsync<AssetDto>(path))!;
        Assert.Equal(asset.Name, after.Name); Assert.Equal(asset.RowVersion, after.RowVersion); Assert.Equal(asset.PurchasePrice, after.PurchasePrice);
        Assert.False(after.IsArchived); Assert.Equal(asset.Status, after.Status);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Set<AssetStatusHistory>().CountAsync(h => h.AssetId == asset.Id));
        Assert.False(await db.Set<AuditLog>().AnyAsync(a => a.EntityType == "Asset" && a.EntityId == asset.Id.ToString() &&
            (a.Action == "assets.update" || a.Action == "assets.archive" || a.Action == "assets.status.change") && a.Outcome == "SUCCESS"));
    }
    [NeonFact]
    public async Task Metadata_only_writer_preserves_hidden_cost_and_cannot_probe_or_clear_it()
    {
        using var writer = await NonfinancialWriterAsync(); using var admin = await fixture.ClientAsync();
        using var created = await admin.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var asset = (await created.Content.ReadFromJsonAsync<AssetDto>())!; var path = $"/api/v1/assets/{asset.Id}";
        var visible = (await writer.GetFromJsonAsync<JsonElement>(path)); Assert.False(visible.TryGetProperty("purchasePrice", out _));
        var request = Metadata("Nonfinancial metadata edit", visible.GetProperty("rowVersion").GetString());
        using var update = await writer.PutAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = (await update.Content.ReadFromJsonAsync<JsonElement>()); Assert.False(saved.TryGetProperty("purchasePrice", out _));
        var current = (await admin.GetFromJsonAsync<AssetDto>(path))!;
        Assert.Equal(asset.PurchasePrice, current.PurchasePrice); Assert.Equal("Nonfinancial metadata edit", current.Name);
        Assert.NotEqual(asset.RowVersion, current.RowVersion);
        foreach (var value in new decimal?[] { null, 999m, asset.PurchasePrice })
        {
            var forbidden = Metadata("Forbidden price write", current.RowVersion); forbidden["purchasePrice"] = value;
            forbidden["hasPurchasePrice"] = false; // Cannot override the server-side presence tracker.
            using var response = await writer.PutAsJsonAsync(path, forbidden); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).ToString();
            Assert.DoesNotContain("1200.25", error); Assert.Contains("FORBIDDEN", error);
        }
        using var stale = await writer.PutAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var after = (await admin.GetFromJsonAsync<AssetDto>(path))!;
        Assert.Equal(current.PurchasePrice, after.PurchasePrice); Assert.Equal(current.Name, after.Name); Assert.Equal(current.RowVersion, after.RowVersion);
    }
    [NeonFact]
    public async Task Nonfinancial_creator_cannot_set_price_but_can_create_with_unknown_cost()
    {
        using var writer = await NonfinancialWriterAsync(); using var admin = await fixture.ClientAsync();
        var blockedName = fixture.Prefix + " rejected financial creation";
        foreach (var value in new decimal?[] { null, 999m })
        {
            var forbidden = Metadata(blockedName); forbidden.Remove("rowVersion"); forbidden["purchasePrice"] = value;
            using var response = await writer.PostAsJsonAsync("/api/v1/assets", forbidden); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        var rejected = (await admin.GetFromJsonAsync<PagedResponse<AssetDto>>("/api/v1/assets?keyword=" + Uri.EscapeDataString(blockedName)))!;
        Assert.Equal(0, rejected.TotalItems);
        var request = Metadata("Unknown purchase cost"); request.Remove("rowVersion");
        using var created = await writer.PostAsJsonAsync("/api/v1/assets", request); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var visible = await created.Content.ReadFromJsonAsync<JsonElement>(); Assert.False(visible.TryGetProperty("purchasePrice", out _));
        var current = (await admin.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{visible.GetProperty("id").GetInt64()}"))!;
        Assert.Null(current.PurchasePrice);
    }
    [NeonFact]
    public async Task Financial_writer_can_update_and_explicitly_clear_price_without_changing_existing_put_semantics()
    {
        VerifyTarget();
        using var admin = await fixture.ClientAsync(); using var created = await admin.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var current = (await created.Content.ReadFromJsonAsync<AssetDto>())!;
        foreach (var value in new decimal?[] { 2400.50m, null })
        {
            var request = Metadata("Financial metadata edit", current.RowVersion); request["purchasePrice"] = value;
            using var updated = await admin.PutAsJsonAsync($"/api/v1/assets/{current.Id}", request); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            current = (await updated.Content.ReadFromJsonAsync<AssetDto>())!; Assert.Equal(value, current.PurchasePrice);
        }
    }
}
