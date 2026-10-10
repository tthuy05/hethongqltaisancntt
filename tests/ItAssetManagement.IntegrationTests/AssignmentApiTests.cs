using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

// Real HTTP evidence for Thiện's four handed-off endpoints, using the existing
// opt-in isolated database. No new routes, schema setup, shared database or cleanup.
[Collection("isolated-neon")]
public sealed class AssignmentApiTests(MvpFixture fixture)
{
    private const string Path = "/api/v1/asset-assignments";
    private const string IsolatedDatabase = "it_asset_management_m1_verify_20261002";

    private async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(IsolatedDatabase, db.Database.GetDbConnection().Database);
        return await work(db);
    }
    private async Task<AssetDto> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }
    private static async Task<CurrentUser> MeAsync(HttpClient client) => (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
    private static async Task<AssignmentDto> AssignAsync(HttpClient client, long assetId, long userId)
    {
        var response = await client.PostAsJsonAsync(Path, new { assetId, userId, note = "Isolated HTTP handoff" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssignmentDto>())!;
    }
    private static async Task<AssignmentDto> ReturnAsync(HttpClient client, AssignmentDto assignment)
    {
        var response = await client.PostAsJsonAsync($"{Path}/{assignment.Id}/return",
            new { rowVersion = assignment.RowVersion, note = "Isolated HTTP return" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssignmentDto>())!;
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        if (code != null)
        {
            var body = (await response.Content.ReadFromJsonAsync<JsonElement>());
            Assert.Equal(code, body.GetProperty("code").GetString());
            Assert.True(body.TryGetProperty("traceId", out _));
        }
    }
    private static async Task<HttpResponseMessage> ArchiveAsync(HttpClient client, AssetDto asset)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/assets/{asset.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", '"' + asset.RowVersion + '"');
        return await client.SendAsync(request);
    }

    [NeonFact]
    public async Task All_four_endpoints_require_valid_authentication()
    {
        using var client = fixture.Factory.CreateClient();
        foreach (var invalidToken in new[] { false, true })
        {
            if (invalidToken) client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-assignment-token");
            await Error(await client.GetAsync(Path), HttpStatusCode.Unauthorized);
            await Error(await client.GetAsync(Path + "/1"), HttpStatusCode.Unauthorized);
            await Error(await client.PostAsJsonAsync(Path, new { assetId = 1, userId = 1 }), HttpStatusCode.Unauthorized);
            await Error(await client.PostAsJsonAsync(Path + "/1/return",
                new { rowVersion = Convert.ToBase64String(new byte[16]), note = "Unauthorized" }), HttpStatusCode.Unauthorized);
        }
    }

    [NeonFact]
    public async Task Technical_support_is_denied_by_all_four_HTTP_permission_policies()
    {
        using var admin = await fixture.ClientAsync(); var asset = await CreateAsync(admin);
        using var support = await fixture.ClientAsync(fixture.SupportEmail); var me = await MeAsync(support);
        await Error(await support.GetAsync(Path), HttpStatusCode.Forbidden);
        await Error(await support.GetAsync(Path + "/1"), HttpStatusCode.Forbidden);
        await Error(await support.PostAsJsonAsync(Path, new { assetId = asset.Id, userId = me.Id }), HttpStatusCode.Forbidden);
        await Error(await support.PostAsJsonAsync(Path + "/1/return",
            new { rowVersion = Convert.ToBase64String(new byte[16]), note = "Forbidden" }), HttpStatusCode.Forbidden);
        Assert.False(await ReadAsync(db => db.Set<AssetAssignment>().AnyAsync(x => x.AssetId == asset.Id)));
        Assert.Equal("IN_STOCK", await ReadAsync(db => db.Set<Asset>().Where(x => x.Id == asset.Id).Select(x => x.CurrentStatus).SingleAsync()));
    }

    [NeonFact]
    public async Task Admin_and_manager_can_use_all_four_routes_with_Location_ETag_and_atomic_audit_pairs()
    {
        foreach (var email in new[] { MvpFixture.AdminEmail, fixture.ManagerEmail })
        {
            using var client = await fixture.ClientAsync(email); var me = await MeAsync(client); var asset = await CreateAsync(client);
            var create = await client.PostAsJsonAsync(Path,
                new { assetId = asset.Id, userId = me.Id, assignedAt = DateTime.UtcNow.AddMinutes(-1), note = "Isolated HTTP handoff" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var assignment = (await create.Content.ReadFromJsonAsync<AssignmentDto>())!;
            Assert.NotNull(create.Headers.Location); Assert.EndsWith($"{Path}/{assignment.Id}", create.Headers.Location!.ToString());
            var detail = await client.GetAsync(create.Headers.Location);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode); Assert.Equal('"' + assignment.RowVersion + '"', detail.Headers.ETag!.Tag);
            var detailJson = await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(detailJson.TryGetProperty("assignedAt", out _)); Assert.True(detailJson.TryGetProperty("returnedAt", out _));
            Assert.False(detailJson.TryGetProperty("assignedAtUtc", out _)); Assert.Equal(me.Id, assignment.AssignedUserId);
            var current = (await client.GetFromJsonAsync<PagedResponse<AssignmentDto>>($"{Path}?assetId={asset.Id}&active=true"))!;
            Assert.Equal(assignment.Id, Assert.Single(current.Items).Id);
            await AssertAuditPairAsync(asset.Id, assignment.Id, "assignments.assign", me.Id);
            var returned = await ReturnAsync(client, assignment);
            Assert.NotNull(returned.ReturnedAtUtc); Assert.NotEqual(assignment.RowVersion, returned.RowVersion);
            var returnedGet = await client.GetAsync($"{Path}/{assignment.Id}");
            Assert.Equal(HttpStatusCode.OK, returnedGet.StatusCode); Assert.Equal('"' + returned.RowVersion + '"', returnedGet.Headers.ETag!.Tag);
            Assert.Empty((await client.GetFromJsonAsync<PagedResponse<AssignmentDto>>($"{Path}?assetId={asset.Id}&active=true"))!.Items);
            Assert.Single((await client.GetFromJsonAsync<PagedResponse<AssignmentDto>>($"{Path}?assetId={asset.Id}"))!.Items);
            Assert.Equal("InStock", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
            await AssertAuditPairAsync(asset.Id, assignment.Id, "assignments.return", me.Id);
            Assert.Equal(2, await ReadAsync(db => db.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == asset.Id && x.Source == "ASSIGNMENT")));
        }
    }

    private Task<bool> AssertAuditPairAsync(long assetId, long assignmentId, string action, long actorId) => ReadAsync(async db =>
    {
        var logs = await db.Set<AuditLog>().AsNoTracking().Where(x => x.Action == action &&
            (x.EntityType == "Asset" && x.EntityId == assetId.ToString() || x.EntityType == "AssetAssignment" && x.EntityId == assignmentId.ToString())).ToListAsync();
        Assert.Equal(2, logs.Count); Assert.All(logs, log => { Assert.Equal(actorId, log.ActorUserId); Assert.Equal("SUCCESS", log.Outcome); });
        Assert.Equal(logs[0].CorrelationId, logs[1].CorrelationId);
        var assignmentLog = logs.Single(x => x.EntityType == "AssetAssignment");
        using var snapshot = JsonDocument.Parse(assignmentLog.NewValuesJson!);
        Assert.Equal(assetId, snapshot.RootElement.GetProperty("AssetId").GetInt64());
        Assert.False(snapshot.RootElement.TryGetProperty("AssignmentNote", out _)); Assert.False(snapshot.RootElement.TryGetProperty("ReturnNote", out _));
        return true;
    });

    [NeonFact]
    public async Task XOR_positive_ID_and_inactive_target_validation_reject_without_business_side_effects()
    {
        using var client = await fixture.ClientAsync(); var me = await MeAsync(client); var asset = await CreateAsync(client);
        var inactiveId = await ReadAsync(db => db.Set<User>().Where(x => x.Email == fixture.InactiveEmail).Select(x => x.Id).SingleAsync());
        foreach (var request in new object[]
        {
            new { assetId = asset.Id }, new { assetId = asset.Id, userId = me.Id, departmentId = fixture.DepartmentId },
            new { assetId = asset.Id, userId = 0 }, new { assetId = asset.Id, userId = inactiveId },
            new { assetId = asset.Id, userId = me.Id, note = new string('x', 1001) },
        }) await Error(await client.PostAsJsonAsync(Path, request), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.False(await ReadAsync(db => db.Set<AssetAssignment>().AnyAsync(x => x.AssetId == asset.Id)));
        Assert.False(await ReadAsync(db => db.Set<AssetStatusHistory>().AnyAsync(x => x.AssetId == asset.Id && x.Source == "ASSIGNMENT")));
        Assert.Equal("InStock", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
    }

    [NeonFact]
    public async Task Return_malformed_version_stale_version_and_closed_replay_have_correct_HTTP_errors()
    {
        using var client = await fixture.ClientAsync(); var me = await MeAsync(client); var asset = await CreateAsync(client);
        var assignment = await AssignAsync(client, asset.Id, me.Id);
        await Error(await client.PostAsJsonAsync($"{Path}/{assignment.Id}/return", new { rowVersion = "malformed", note = "Invalid" }),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.PostAsJsonAsync($"{Path}/{assignment.Id}/return", new { rowVersion = Convert.ToBase64String(new byte[16]), note = "Stale" }),
            HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
        Assert.Null((await client.GetFromJsonAsync<AssignmentDto>($"{Path}/{assignment.Id}"))!.ReturnedAtUtc);
        Assert.False(await ReadAsync(db => db.Set<AuditLog>().AnyAsync(x => x.Action == "assignments.return" && x.EntityType == "AssetAssignment" && x.EntityId == assignment.Id.ToString())));
        var returned = await ReturnAsync(client, assignment);
        await Error(await client.PostAsJsonAsync($"{Path}/{assignment.Id}/return", new { rowVersion = assignment.RowVersion, note = "Old token" }),
            HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
        await Error(await client.PostAsJsonAsync($"{Path}/{assignment.Id}/return", new { rowVersion = returned.RowVersion, note = "Replay" }),
            HttpStatusCode.Conflict, "ASSIGNMENT_ALREADY_RETURNED");
        await AssertAuditPairAsync(asset.Id, assignment.Id, "assignments.return", me.Id);
    }

    [NeonFact]
    public async Task List_uses_handed_off_filters_and_rejects_unimplemented_or_invalid_query_contracts()
    {
        using var client = await fixture.ClientAsync(); var me = await MeAsync(client); var asset = await CreateAsync(client);
        var assignment = await AssignAsync(client, asset.Id, me.Id);
        var response = await client.GetAsync($"{Path}?assetId={asset.Id}&userId={me.Id}&active=true&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<AssignmentDto>>())!;
        Assert.Equal(1, page.TotalItems); Assert.Equal(1, page.TotalPages); Assert.Equal(assignment.Id, Assert.Single(page.Items).Id);
        foreach (var query in new[] { "page=0", "pageSize=101", "page=2147483647&pageSize=100", "assetId=-1", "active=invalid", "page=1&page=2",
            "status=Active", "from=2026-10-01", "to=2026-10-08", "sortBy=assignedAt" })
            await Error(await client.GetAsync(Path + "?" + query), HttpStatusCode.BadRequest);
        // status/from/to/client sort remain contract gaps, not silently supported by this Controller.
    }

    [NeonFact]
    public async Task Asset_HTTP_archive_and_retire_block_only_real_active_workflow_and_allow_returned_assets()
    {
        using var client = await fixture.ClientAsync(); var me = await MeAsync(client);
        var occupied = await CreateAsync(client); var unrelated = await CreateAsync(client); var assignment = await AssignAsync(client, occupied.Id, me.Id);
        var current = (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{occupied.Id}"))!;
        await Error(await ArchiveAsync(client, current), HttpStatusCode.Conflict, "ASSET_ACTIVE_WORKFLOW");
        await Error(await client.PatchAsJsonAsync($"/api/v1/assets/{occupied.Id}/status",
            new { status = "Retired", reason = "Blocked active assignment", rowVersion = current.RowVersion }), HttpStatusCode.Conflict);
        var retireFree = await client.PatchAsJsonAsync($"/api/v1/assets/{unrelated.Id}/status",
            new { status = "Retired", reason = "Unrelated asset is eligible", rowVersion = unrelated.RowVersion });
        Assert.Equal(HttpStatusCode.OK, retireFree.StatusCode);
        var retiredFree = (await retireFree.Content.ReadFromJsonAsync<AssetDto>())!;
        Assert.Equal("Retired", retiredFree.Status); Assert.Equal(HttpStatusCode.NoContent, (await ArchiveAsync(client, retiredFree)).StatusCode);
        await ReturnAsync(client, assignment);
        current = (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{occupied.Id}"))!;
        var retireReturned = await client.PatchAsJsonAsync($"/api/v1/assets/{occupied.Id}/status",
            new { status = "Retired", reason = "Returned asset is eligible", rowVersion = current.RowVersion });
        Assert.Equal(HttpStatusCode.OK, retireReturned.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ArchiveAsync(client, (await retireReturned.Content.ReadFromJsonAsync<AssetDto>())!)).StatusCode);
    }

    [NeonFact]
    public async Task Concurrent_HTTP_assigns_return_one_201_one_409_and_one_audited_transition()
    {
        using var client = await fixture.ClientAsync(fixture.ManagerEmail); var me = await MeAsync(client); var asset = await CreateAsync(client);
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Path, new { assetId = asset.Id, userId = me.Id }),
            client.PostAsJsonAsync(Path, new { assetId = asset.Id, userId = me.Id }));
        var created = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var assignment = (await created.Content.ReadFromJsonAsync<AssignmentDto>())!;
        Assert.Equal(1, await ReadAsync(db => db.Set<AssetAssignment>().CountAsync(x => x.AssetId == asset.Id && x.ReturnedAtUtc == null)));
        Assert.Equal(1, await ReadAsync(db => db.Set<AssetStatusHistory>().CountAsync(x => x.AssetId == asset.Id && x.Source == "ASSIGNMENT")));
        await AssertAuditPairAsync(asset.Id, assignment.Id, "assignments.assign", me.Id);
    }

    [NeonFact]
    public async Task Retired_and_archived_assets_cannot_be_assigned_through_HTTP()
    {
        using var client = await fixture.ClientAsync(); var me = await MeAsync(client); var retired = await CreateAsync(client); var archived = await CreateAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/assets/{retired.Id}/status",
            new { status = "Retired", reason = "Isolated retire guard", rowVersion = retired.RowVersion })).StatusCode);
        await Error(await client.PostAsJsonAsync(Path, new { assetId = retired.Id, userId = me.Id }), HttpStatusCode.Conflict, "ASSET_NOT_IN_STOCK");
        Assert.Equal(HttpStatusCode.NoContent, (await ArchiveAsync(client, archived)).StatusCode);
        await Error(await client.PostAsJsonAsync(Path, new { assetId = archived.Id, userId = me.Id }), HttpStatusCode.Conflict, "ASSET_ARCHIVED");
    }

    [NeonFact]
    public async Task Missing_assignment_has_404_and_invalid_return_time_keeps_assignment_open()
    {
        using var client = await fixture.ClientAsync(); var me = await MeAsync(client); var asset = await CreateAsync(client);
        await Error(await client.GetAsync(Path + "/9223372036854775807"), HttpStatusCode.NotFound, "ASSIGNMENT_NOT_FOUND");
        await Error(await client.PostAsJsonAsync(Path + "/9223372036854775807/return",
            new { rowVersion = Convert.ToBase64String(new byte[16]), note = "Missing" }), HttpStatusCode.NotFound, "ASSIGNMENT_NOT_FOUND");
        var assignment = await AssignAsync(client, asset.Id, me.Id);
        await Error(await client.PostAsJsonAsync($"{Path}/{assignment.Id}/return",
            new { rowVersion = assignment.RowVersion, returnedAt = assignment.AssignedAtUtc.AddMinutes(-1), note = "Wrong chronology" }),
            HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Null((await client.GetFromJsonAsync<AssignmentDto>($"{Path}/{assignment.Id}"))!.ReturnedAtUtc);
        Assert.Equal("InUse", (await client.GetFromJsonAsync<AssetDto>($"/api/v1/assets/{asset.Id}"))!.Status);
    }
}
