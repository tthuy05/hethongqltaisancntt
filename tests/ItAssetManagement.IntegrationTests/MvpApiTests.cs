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

[Collection("isolated-neon")]
public sealed class MvpApiTests(MvpFixture fixture)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();
    private async Task<(HttpClient Client, JsonElement Asset, AssetRequest Request)> Created()
    {
        var client = await fixture.ClientAsync(); var request = fixture.Asset();
        var response = await client.PostAsJsonAsync("/api/v1/assets", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (client, await Json(response), request);
    }
    [NeonFact] public async Task Login_success_returns_valid_bearer_and_me()
    {
        var client = await fixture.ClientAsync(); var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode); var body = await Json(me);
        Assert.Equal(MvpFixture.AdminEmail, body.GetProperty("email").GetString());
        Assert.Contains("ADMIN_IT", body.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }
    [NeonFact] public async Task Wrong_unknown_inactive_and_locked_credentials_are_generic_401()
    {
        using var client = fixture.Factory.CreateClient();
        foreach (var (email, password) in new[] { (MvpFixture.AdminEmail, "wrong"), ("unknown@fixture.test", MvpFixture.Password),
            (fixture.InactiveEmail, MvpFixture.Password), (fixture.LockedEmail, MvpFixture.Password) })
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("INVALID_CREDENTIALS", (await Json(response)).GetProperty("code").GetString());
        }
    }
    [NeonFact] public async Task Automatic_lockout_blocks_even_correct_password()
    {
        var email = await fixture.UserAsync("lockout", "ADMIN_IT"); using var client = fixture.Factory.CreateClient();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = MvpFixture.Password })).StatusCode);
    }
    [NeonFact] public async Task Anonymous_and_invalid_token_cannot_access_assets()
    {
        using var client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
    }
    [NeonFact] public async Task Support_is_read_only_and_cost_is_omitted_in_detail_and_list()
    {
        var (_, asset, _) = await Created(); using var support = await fixture.ClientAsync(fixture.SupportEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostAsJsonAsync("/api/v1/assets", fixture.Asset())).StatusCode);
        var response = await support.GetAsync("/api/v1/assets/" + asset.GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.False((await Json(response)).TryGetProperty("purchasePrice", out _));
        var list = await Json(await support.GetAsync("/api/v1/assets?keyword=" + asset.GetProperty("assetCode").GetString()));
        Assert.False(list.GetProperty("items")[0].TryGetProperty("purchasePrice", out _));
    }
    [NeonFact] public async Task Manager_can_write_assets_but_cannot_write_master_data()
    {
        using var client = await fixture.ClientAsync(fixture.ManagerEmail);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/departments", new { code = fixture.Prefix + "forbidden", name = "Forbidden" })).StatusCode);
    }
    [NeonFact] public async Task Create_get_etag_and_initial_history_are_persisted()
    {
        var (client, asset, _) = await Created(); var id = asset.GetProperty("id").GetInt64();
        var get = await client.GetAsync("/api/v1/assets/" + id); Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal('"' + asset.GetProperty("rowVersion").GetString() + '"', get.Headers.ETag!.Tag);
        Assert.True(asset.TryGetProperty("createdAt", out _));
        Assert.False(asset.TryGetProperty("createdAtUtc", out _));
        var histories = await Json(await client.GetAsync($"/api/v1/assets/{id}/status-history"));
        Assert.Equal("InStock", histories.GetProperty("items")[0].GetProperty("toStatus").GetString());
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Set<Asset>().AnyAsync(x => x.Id == id));
        Assert.True(await db.Set<AuditLog>().AnyAsync(x => x.EntityType == "Asset" && x.EntityId == id.ToString() && x.Action == "assets.create"));
    }
    [NeonFact] public async Task Duplicate_code_is_case_insensitive_409()
    {
        var (client, _, request) = await Created(); request.AssetCode = " " + request.AssetCode.ToLowerInvariant() + " ";
        var response = await client.PostAsJsonAsync("/api/v1/assets", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal("ASSET_CODE_CONFLICT", (await Json(response)).GetProperty("code").GetString());
    }
    [NeonFact] public async Task Duplicate_serial_is_case_insensitive_but_null_serial_is_allowed()
    {
        using var client = await fixture.ClientAsync(); var first = fixture.Asset(); first.SerialNumber = fixture.Prefix + "Serial";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/assets", first)).StatusCode);
        var second = fixture.Asset(); second.SerialNumber = first.SerialNumber.ToLowerInvariant();
        var response = await client.PostAsJsonAsync("/api/v1/assets", second); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ASSET_SERIAL_CONFLICT", (await Json(response)).GetProperty("code").GetString());
        second.SerialNumber = " "; Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/assets", second)).StatusCode);
    }
    [NeonFact] public async Task Update_regenerates_version_and_stale_write_returns_409()
    {
        var (client, asset, request) = await Created(); var path = "/api/v1/assets/" + asset.GetProperty("id").GetInt64();
        request.RowVersion = asset.GetProperty("rowVersion").GetString(); request.Name = "Updated real asset";
        var update = await client.PutAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await Json(update); Assert.Equal(request.Name, body.GetProperty("name").GetString());
        Assert.NotEqual(request.RowVersion, body.GetProperty("rowVersion").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, request)).StatusCode);
    }
    [NeonFact] public async Task Competing_updates_have_exactly_one_winner()
    {
        var (client, asset, request) = await Created(); request.RowVersion = asset.GetProperty("rowVersion").GetString();
        var path = "/api/v1/assets/" + asset.GetProperty("id").GetInt64(); request.Name = "Concurrent";
        var responses = await Task.WhenAll(client.PutAsJsonAsync(path, request), client.PutAsJsonAsync(path, request));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }
    [NeonFact] public async Task Archive_requires_etag_and_keeps_record_and_history()
    {
        var (client, asset, _) = await Created(); var id = asset.GetProperty("id").GetInt64(); var path = "/api/v1/assets/" + id;
        Assert.Equal((HttpStatusCode)428, (await client.DeleteAsync(path)).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Delete, path); request.Headers.TryAddWithoutValidation("If-Match", '"' + asset.GetProperty("rowVersion").GetString() + '"');
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await db.Set<Asset>().SingleAsync(x => x.Id == id)).IsArchived);
        Assert.True(await db.Set<AssetStatusHistory>().AnyAsync(x => x.AssetId == id));
    }
    [NeonFact] public async Task Retire_is_allowed_but_workflow_status_is_not()
    {
        var (client, asset, _) = await Created(); var path = "/api/v1/assets/" + asset.GetProperty("id").GetInt64() + "/status";
        var version = asset.GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path, new { status = "InUse", reason = "Invalid direct workflow", rowVersion = version })).StatusCode);
        var response = await client.PatchAsJsonAsync(path, new { status = "Retired", reason = "M1 retirement", rowVersion = version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("Retired", (await Json(response)).GetProperty("status").GetString());
    }
    [NeonFact] public async Task Query_search_filters_paging_and_sort_are_server_side()
    {
        using var client = await fixture.ClientAsync(); var prefix = fixture.Prefix + "Paging";
        foreach (var suffix in new[] { "C", "A", "B" })
        { var asset = fixture.Asset(prefix + suffix); Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/assets", asset)).StatusCode); }
        var query = $"/api/v1/assets?keyword={prefix.ToLowerInvariant()}&assetTypeId={fixture.TypeId}&departmentId={fixture.DepartmentId}&status=InStock&pageSize=2&sortBy=assetCode&sortDirection=desc";
        var first = await Json(await client.GetAsync(query)); Assert.Equal(3, first.GetProperty("totalItems").GetInt32()); Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        var items = first.GetProperty("items"); Assert.Equal(2, items.GetArrayLength()); Assert.EndsWith("C", items[0].GetProperty("assetCode").GetString());
        var second = await Json(await client.GetAsync(query + "&page=2")); Assert.Single(second.GetProperty("items").EnumerateArray());
        var past = await Json(await client.GetAsync(query + "&page=100")); Assert.Empty(past.GetProperty("items").EnumerateArray());
    }
    [NeonFact] public async Task Search_treats_percent_and_underscore_literally()
    {
        using var client = await fixture.ClientAsync(); var asset = fixture.Asset(); asset.Name = fixture.Prefix + "Literal%_search";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/assets", asset)).StatusCode);
        var body = await Json(await client.GetAsync("/api/v1/assets?keyword=" + Uri.EscapeDataString(fixture.Prefix + "Literal%_")));
        Assert.Equal(1, body.GetProperty("totalItems").GetInt32());
    }
    [NeonFact] public async Task Invalid_queries_are_400_not_silently_ignored()
    {
        using var client = await fixture.ClientAsync();
        foreach (var query in new[] { "page=0", "pageSize=101", "sortBy=passwordHash", "status=UNKNOWN", "departmentId=-1", "purchaseYear=2026", "page=1&page=2", "page=abc" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/assets?" + query)).StatusCode);
    }
    [NeonFact] public async Task Invalid_asset_fields_and_server_owned_fields_are_400()
    {
        using var client = await fixture.ClientAsync();
        var bad = fixture.Asset(); bad.AssetCode = " "; Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/assets", bad)).StatusCode);
        bad = fixture.Asset(); bad.PurchasePrice = -1; Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/assets", bad)).StatusCode);
        bad = fixture.Asset(); bad.AssetTypeId = long.MaxValue; Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/assets", bad)).StatusCode);
        bad = fixture.Asset(); bad.PurchaseDate = new(2026, 10, 2); bad.WarrantyExpirationDate = new(2026, 10, 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/assets", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/assets", new { assetCode = "forbidden", status = "InUse", isArchived = true })).StatusCode);
    }
    [NeonFact] public async Task Masters_can_be_created_updated_inactivated_and_read()
    {
        using var client = await fixture.ClientAsync();
        foreach (var path in new[] { "/api/v1/departments", "/api/v1/asset-types" })
        {
            var code = fixture.Prefix + (path.EndsWith("departments") ? "Dept" : "Type");
            var created = await client.PostAsJsonAsync(path, new { code, name = "Test master" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var body = await Json(created); var itemPath = path + "/" + body.GetProperty("id").GetInt64();
            var updatedCode = path.EndsWith("asset-types") ? code + "U" : code;
            var updated = await client.PutAsJsonAsync(itemPath, new { code = updatedCode, name = "Updated master", rowVersion = body.GetProperty("rowVersion").GetString() });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode); var current = await Json(updated);
            var status = await client.PatchAsJsonAsync(itemPath + "/status", new { status = "Inactive", reason = "Test", rowVersion = current.GetProperty("rowVersion").GetString() });
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            var list = await client.GetAsync(path + "?keyword=" + updatedCode + "&status=Inactive&sortBy=code"); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Equal(1, (await Json(list)).GetProperty("totalItems").GetInt32());
        }
    }
    [NeonFact] public async Task Department_cycle_and_code_change_are_rejected()
    {
        using var client = await fixture.ClientAsync();
        var root = await Json(await client.PostAsJsonAsync("/api/v1/departments", new { code = fixture.Prefix + "Parent", name = "Parent" }));
        var child = await Json(await client.PostAsJsonAsync("/api/v1/departments", new { code = fixture.Prefix + "Child", name = "Child", parentDepartmentId = root.GetProperty("id").GetInt64() }));
        var path = "/api/v1/departments/" + root.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { code = root.GetProperty("code").GetString(), name = "Loop", parentDepartmentId = child.GetProperty("id").GetInt64(), rowVersion = root.GetProperty("rowVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { code = "changed", name = "Changed", rowVersion = root.GetProperty("rowVersion").GetString() })).StatusCode);
    }
    [NeonFact] public async Task Seed_is_idempotent_and_unaudited_transaction_is_rolled_back()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var result = await sp.GetRequiredService<DevelopmentSeed>().RunAsync(new(MvpFixture.AdminEmail, MvpFixture.Password));
        Assert.Equal(0, result.Added); Assert.Equal(3, result.Roles);
        Assert.Equal(Permissions.All.Length + Permissions.Read.Length + Permissions.Operate.Length + Permissions.Read.Length, result.RolePermissions);
        var db = sp.GetRequiredService<AppDbContext>(); var code = fixture.Prefix + "Unaudited";
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { db.Add(new Department { Code = code, Name = "Must rollback" }); await db.SaveChangesAsync(); return true; }));
        Assert.False(await db.Set<Department>().AnyAsync(x => x.Code == code));
    }
    [NeonFact] public async Task Audit_snapshots_do_not_contain_hashes_passwords_or_tokens()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var writerAction = "test.writer-redaction." + fixture.Prefix;
        await services.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            services.GetRequiredService<IAuditWriter>().Record(writerAction, null, null, "SYSTEM",
                after: new { Password = "synthetic", AccessToken = "synthetic", ConnectionString = "synthetic", IsActive = true });
            await db.SaveChangesAsync(); return true;
        });
        var written = await db.Set<AuditLog>().SingleAsync(x => x.Action == writerAction);
        var snapshot = JsonSerializer.Deserialize<JsonElement>(written.NewValuesJson!);
        Assert.True(snapshot.GetProperty("IsActive").GetBoolean());
        Assert.Equal(["IsActive"], snapshot.EnumerateObject().Select(x => x.Name));
        // Audit-reader security tests deliberately bypass the writer with raw hostile
        // JSON in exact namespaced fixtures. Retained isolated runs must not make
        // those reader inputs look like production writer output; never delete them.
        var logs = await db.Set<AuditLog>().Where(x => !(x.Action.StartsWith("T") && x.Action.Contains(".audit.")))
            .Select(x => new { x.OldValuesJson, x.NewValuesJson, x.MetadataJson }).ToListAsync();
        foreach (var log in logs)
        {
            var text = log.OldValuesJson + log.NewValuesJson + log.MetadataJson;
            Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("accessToken", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ConnectionString", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
