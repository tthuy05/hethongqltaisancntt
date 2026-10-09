using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

public sealed class AuditLogHostTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "", ["Jwt:SigningKey"] = "synthetic-audit-host-key-only-for-tests",
          ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false" }));
    });

    [Fact]
    public async Task Anonymous_and_invalid_bearer_cannot_read_audits_without_database()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        foreach (var token in new[] { "", "invalid-audit-token" })
        {
            client.DefaultRequestHeaders.Authorization = token == "" ? null : new("Bearer", token);
            foreach (var path in new[] { "/api/v1/audit-logs", "/api/v1/audit-logs/1" })
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task OpenApi_describes_only_safe_audit_reads_with_bearer_and_stable_errors()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        foreach (var path in new[] { "/api/v1/audit-logs", "/api/v1/audit-logs/{auditLogId}" })
        {
            var operations = document.GetProperty("paths").GetProperty(path);
            Assert.Equal(["get"], operations.EnumerateObject().Select(x => x.Name));
            var read = operations.GetProperty("get"); Assert.Single(read.GetProperty("security").EnumerateArray());
            foreach (var status in new[] { "200", "400", "401", "403" })
                Assert.True(read.GetProperty("responses").TryGetProperty(status, out _));
        }
        Assert.True(document.GetProperty("paths").GetProperty("/api/v1/audit-logs/{auditLogId}")
            .GetProperty("get").GetProperty("responses").TryGetProperty("404", out _));
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.Equal(AuditLogApiTests.SummaryFields, schemas.GetProperty("AuditLogSummaryDto").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(AuditLogApiTests.DetailFields, schemas.GetProperty("AuditLogDetailDto").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
    }
}

[Collection("isolated-neon")]
public sealed class AuditLogApiTests(MvpFixture fixture)
{
    private const string Path = "/api/v1/audit-logs";
    internal static readonly string[] SummaryFields = ["action", "actorType", "actorUserId", "correlationId", "entityId", "entityType", "id", "occurredAt", "outcome"];
    internal static readonly string[] DetailFields = ["action", "actorType", "actorUserId", "correlationId", "entityId", "entityType", "id", "newValues", "occurredAt", "oldValues", "outcome", "snapshotsRedacted"];
    private string Marker() => fixture.Prefix + ".audit." + Guid.NewGuid().ToString("N")[..8];
    private static DateTime WholeSecond(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    private static string Window(DateTime from, DateTime to) => "from=" + Uri.EscapeDataString(from.ToString("O")) + "&to=" + Uri.EscapeDataString(to.ToString("O"));
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString()); Assert.True(json.TryGetProperty("traceId", out _));
    }
    private async Task<long> AdminId()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<User>()
            .Where(x => x.Email == MvpFixture.AdminEmail).Select(x => x.Id).SingleAsync();
    }
    private static AuditLog Log(string action, DateTime occurredAt, long? actorId = null, string outcome = "SUCCESS") => new()
    { Action = action, OccurredAtUtc = occurredAt, ActorUserId = actorId, ActorType = actorId.HasValue ? "USER" : "SYSTEM",
      Outcome = outcome, CorrelationId = Guid.NewGuid(), EntityType = "Asset", EntityId = "fixture-only" };
    private async Task Insert(params AuditLog[] entries)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal(Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE") ?? "it_asset_management_m1_verify_20261002", db.Database.GetDbConnection().Database);
        await services.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { db.AddRange(entries); await db.SaveChangesAsync(); return true; });
    }
    private async Task<int> ViewCount()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AuditLog>().CountAsync(x => x.Action == "audit.view");
    }
    private async Task TogglePermission(string code, bool active)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        await services.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var permission = await db.Set<Permission>().SingleAsync(x => x.Code == code);
            permission.IsActive = active; services.GetRequiredService<IAuditWriter>().Record("test.audit.permission", permission, null, "SYSTEM");
            await db.SaveChangesAsync(); return true;
        });
    }

    [NeonFact]
    public async Task Only_Admin_reads_minimal_summary_and_detail_and_other_roles_are_denied()
    {
        var entry = Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1)), await AdminId()); await Insert(entry);
        using var admin = await fixture.ClientAsync(); using var manager = await fixture.ClientAsync(fixture.ManagerEmail);
        using var support = await fixture.ClientAsync(fixture.SupportEmail);
        var page = await Json(await admin.GetAsync(Path + "?action=" + entry.Action));
        Assert.Equal(1, page.GetProperty("totalItems").GetInt32()); var summary = page.GetProperty("items")[0];
        Assert.Equal(SummaryFields, summary.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(entry.Id, summary.GetProperty("id").GetInt64()); Assert.Equal(entry.ActorUserId, summary.GetProperty("actorUserId").GetInt64());
        Assert.Equal(entry.CorrelationId, summary.GetProperty("correlationId").GetGuid());
        Assert.Equal(entry.OccurredAtUtc, summary.GetProperty("occurredAt").GetDateTime().ToUniversalTime());
        var detail = await Json(await admin.GetAsync(Path + "/" + entry.Id));
        Assert.Equal(DetailFields, detail.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("oldValues").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("newValues").ValueKind); Assert.False(detail.GetProperty("snapshotsRedacted").GetBoolean());
        var before = await ViewCount();
        foreach (var client in new[] { manager, support })
            foreach (var path in new[] { Path, Path + "/" + entry.Id })
                await Error(await client.GetAsync(path), HttpStatusCode.Forbidden, "FORBIDDEN");
        Assert.Equal(before, await ViewCount());
    }

    [NeonFact]
    public async Task PostgreSql_translates_exact_filters_and_half_open_UTC_window_without_wildcard_search()
    {
        var marker = Marker(); var start = WholeSecond(DateTime.UtcNow.AddDays(-1)); var end = start.AddHours(2); var adminId = await AdminId();
        var a = Log(marker, start, adminId); a.EntityId = marker;
        var b = Log(marker, start.AddHours(1), adminId, "FAILURE"); b.EntityType = "User"; b.EntityId = marker;
        var c = Log(marker, start.AddHours(1), outcome: "DENIED"); c.EntityId = marker + "-other";
        var excluded = Log(marker, end); await Insert(a, b, c, excluded);
        using var admin = await fixture.ClientAsync(); var basis = Path + "?action=" + marker + "&" + Window(start, end);
        var page = await Json(await admin.GetAsync(basis)); Assert.Equal(3, page.GetProperty("totalItems").GetInt32());
        Assert.Contains(page.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetInt64() == a.Id);
        Assert.DoesNotContain(page.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetInt64() == excluded.Id);
        foreach (var (filter, expected) in new[] { ("userId=" + adminId, 2), ("entityType=Asset", 2), ("entityId=" + marker, 2),
            ("correlationId=" + b.CorrelationId, 1), ("outcome=FAILURE", 1), ("outcome=DENIED", 1) })
            Assert.Equal(expected, (await Json(await admin.GetAsync(basis + "&" + filter))).GetProperty("totalItems").GetInt32());
        var selected = await Json(await admin.GetAsync(basis + "&userId=" + adminId + "&entityType=User&entityId=" + marker + "&correlationId=" + b.CorrelationId + "&outcome=FAILURE"));
        Assert.Equal(b.Id, selected.GetProperty("items")[0].GetProperty("id").GetInt64());
        foreach (var literal in new[] { "%", "_", "\\", "' OR 1=1 --" })
            Assert.Equal(0, (await Json(await admin.GetAsync(Path + "?action=" + Uri.EscapeDataString(literal)))).GetProperty("totalItems").GetInt32());
        var offsetWindow = "from=" + Uri.EscapeDataString(new DateTimeOffset(start).ToOffset(TimeSpan.FromHours(7)).ToString("O")) +
            "&to=" + Uri.EscapeDataString(new DateTimeOffset(end).ToOffset(TimeSpan.FromHours(7)).ToString("O"));
        Assert.Equal(3, (await Json(await admin.GetAsync(Path + "?action=" + marker + "&" + offsetWindow))).GetProperty("totalItems").GetInt32());
    }

    [NeonFact]
    public async Task Default_window_is_seven_days_and_explicit_bounds_can_read_older_records()
    {
        var marker = Marker(); var now = WholeSecond(DateTime.UtcNow);
        var recent = Log(marker, now.AddDays(-1)); var older = Log(marker, now.AddDays(-8)); await Insert(recent, older);
        using var admin = await fixture.ClientAsync();
        foreach (var suffix in new[] { "", "&to=" + Uri.EscapeDataString(now.ToString("O")) })
        {
            var page = await Json(await admin.GetAsync(Path + "?action=" + marker + suffix));
            Assert.Equal(1, page.GetProperty("totalItems").GetInt32()); Assert.Equal(recent.Id, page.GetProperty("items")[0].GetProperty("id").GetInt64());
        }
        Assert.Equal(2, (await Json(await admin.GetAsync(Path + "?action=" + marker + "&" + Window(now.AddDays(-31), now)))).GetProperty("totalItems").GetInt32());
    }

    [NeonFact]
    public async Task Paging_and_sorting_use_id_tie_breaker_and_past_end_keeps_accurate_totals()
    {
        var marker = Marker(); var instant = WholeSecond(DateTime.UtcNow.AddHours(-2));
        var entries = Enumerable.Range(0, 3).Select(_ => Log(marker, instant)).ToArray(); await Insert(entries);
        using var admin = await fixture.ClientAsync();
        foreach (var sortBy in new[] { "occurredAt", "id" })
            foreach (var direction in new[] { "asc", "desc" })
            {
                var expected = direction == "asc" ? entries.Select(x => x.Id).Order().ToArray() : entries.Select(x => x.Id).OrderDescending().ToArray();
                for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
                {
                    var page = await Json(await admin.GetAsync(Path + $"?action={marker}&pageSize=1&page={pageNumber}&sortBy={sortBy}&sortDirection={direction}"));
                    Assert.Equal(expected[pageNumber - 1], page.GetProperty("items")[0].GetProperty("id").GetInt64());
                    Assert.Equal(3, page.GetProperty("totalItems").GetInt32()); Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
                }
            }
        var past = await Json(await admin.GetAsync(Path + "?action=" + marker + "&pageSize=2&page=99"));
        Assert.Empty(past.GetProperty("items").EnumerateArray()); Assert.Equal(3, past.GetProperty("totalItems").GetInt32()); Assert.Equal(2, past.GetProperty("totalPages").GetInt32());
    }

    [NeonFact]
    public async Task Invalid_queries_and_missing_details_return_stable_errors_without_audit_view_marker()
    {
        using var admin = await fixture.ClientAsync(); var before = await ViewCount(); var now = WholeSecond(DateTime.UtcNow);
        foreach (var query in new[] { "page=0", "pageSize=0", "pageSize=101", "page=2147483647", "page=no", "userId=0", "userId=-1", "userId=no",
            "outcome=success", "sortBy=actorUserId", "sortDirection=DESC", "Page=1", "page=1&page=2", "includeSecrets=true", "correlationId=no",
            "correlationId=00000000-0000-0000-0000-000000000000", "from=2026-10-01", "to=2026-10-01T10%3A00%3A00", "from=", "to=bad",
            "action=" + new string('x', 151), "entityType=" + new string('x', 101), "entityId=" + new string('x', 101),
            Window(now, now), Window(now, now.AddHours(-1)), Window(now.AddDays(-31).AddSeconds(-1), now) })
            await Error(await admin.GetAsync(Path + "?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.GetAsync(Path + "/0"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.GetAsync(Path + "/1?includeSecrets=true"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.GetAsync(Path + "/9223372036854775807"), HttpStatusCode.NotFound, "AUDIT_LOG_NOT_FOUND");
        Assert.Equal(before, await ViewCount());
    }

    [NeonFact]
    public async Task Successful_reads_append_exactly_one_view_after_selection_without_recursive_reread()
    {
        using var admin = await fixture.ClientAsync(); var actorId = await AdminId(); var start = WholeSecond(DateTime.UtcNow.AddDays(-1)); var end = DateTime.UtcNow.AddMinutes(5);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expectedCount = await db.Set<AuditLog>().CountAsync(x => x.Action == "audit.view" && x.OccurredAtUtc >= start && x.OccurredAtUtc < end);
        var before = await ViewCount();
        var page = await Json(await admin.GetAsync(Path + "?action=audit.view&pageSize=1&" + Window(start, end)));
        Assert.Equal(expectedCount, page.GetProperty("totalItems").GetInt32()); Assert.Equal(before + 1, await ViewCount());
        var listed = await db.Set<AuditLog>().AsNoTracking().Where(x => x.Action == "audit.view").OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(actorId, listed.ActorUserId); Assert.Equal("USER", listed.ActorType); Assert.Equal("SUCCESS", listed.Outcome);
        Assert.NotEqual(Guid.Empty, listed.CorrelationId); Assert.Equal("GET", listed.RequestMethod);
        var listSnapshot = JsonSerializer.Deserialize<JsonElement>(listed.NewValuesJson!);
        Assert.Equal(1, listSnapshot.GetProperty("page").GetInt32()); Assert.Equal(1, listSnapshot.GetProperty("pageSize").GetInt32());
        Assert.Equal(Math.Min(1, expectedCount), listSnapshot.GetProperty("returnedCount").GetInt32());
        var detail = await Json(await admin.GetAsync(Path + "/" + listed.Id));
        Assert.Equal(listed.Id, detail.GetProperty("id").GetInt64()); Assert.Equal(before + 2, await ViewCount());
        var viewed = await db.Set<AuditLog>().AsNoTracking().Where(x => x.Action == "audit.view").OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(listed.Id, JsonSerializer.Deserialize<JsonElement>(viewed.NewValuesJson!).GetProperty("auditLogId").GetInt64());
    }

    [NeonFact]
    public async Task Details_reallowlist_stored_snapshots_and_never_expose_credentials_metadata_or_raw_paths()
    {
        const string secretMarker = "synthetic-audit-sensitive-marker-not-real-credentials";
        var entry = Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1)));
        entry.OldValuesJson = "{\"IsActive\":true,\"DepartmentId\":null,\"UserId\":1,\"PurchaseCost\":1250.25}";
        entry.NewValuesJson = JsonSerializer.Serialize(new { isActive = false, departmentId = (long?)null, roleCount = 2, currentStatus = "IN_STOCK",
            password = secretMarker, token = secretMarker, email = secretMarker, name = secretMarker,
            nested = new { isActive = true }, userId = secretMarker, roleId = -1, adminRoleAssigned = "true", purchaseCost = 1250.25m });
        entry.MetadataJson = JsonSerializer.Serialize(new { password = secretMarker }); entry.RequestPath = "/synthetic/" + secretMarker;
        entry.IpAddress = "192.0.2.17"; entry.UserAgent = secretMarker; entry.FailureReasonCode = secretMarker;
        entry.EntryHash = new byte[32]; entry.PreviousEntryHash = new byte[32]; await Insert(entry);
        using var admin = await fixture.ClientAsync(); var response = await admin.GetAsync(Path + "/" + entry.Id); var raw = await response.Content.ReadAsStringAsync();
        var detail = await Json(response); Assert.DoesNotContain(secretMarker, raw); Assert.DoesNotContain("192.0.2.17", raw);
        Assert.Equal(DetailFields, detail.EnumerateObject().Select(x => x.Name).Order()); Assert.True(detail.GetProperty("snapshotsRedacted").GetBoolean());
        Assert.Equal(["departmentId", "isActive", "purchaseCost", "userId"], detail.GetProperty("oldValues").EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(["currentStatus", "departmentId", "isActive", "purchaseCost", "roleCount"], detail.GetProperty("newValues").EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(1250.25m, detail.GetProperty("oldValues").GetProperty("purchaseCost").GetDecimal());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("oldValues").GetProperty("departmentId").ValueKind);
    }

    [NeonFact]
    public async Task Snapshot_array_case_duplicate_and_oversized_object_fail_closed()
    {
        var entries = new[] { Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1))), Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1))), Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1))) };
        entries[0].NewValuesJson = "[{\"isActive\":true}]";
        entries[1].NewValuesJson = "{\"isActive\":true,\"IsActive\":false}";
        entries[2].NewValuesJson = JsonSerializer.Serialize(new { isActive = true, payload = new string('x', 17000) });
        await Insert(entries); using var admin = await fixture.ClientAsync();
        foreach (var entry in entries)
        {
            var detail = await Json(await admin.GetAsync(Path + "/" + entry.Id));
            Assert.Equal(JsonValueKind.Object, detail.GetProperty("newValues").ValueKind);
            Assert.Empty(detail.GetProperty("newValues").EnumerateObject()); Assert.True(detail.GetProperty("snapshotsRedacted").GetBoolean());
        }
    }

    [NeonFact]
    public async Task Live_cost_permission_masks_both_snapshots_even_for_an_existing_Admin_token()
    {
        var entry = Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1))); entry.OldValuesJson = "{\"purchaseCost\":1200.25,\"isActive\":true}";
        entry.NewValuesJson = "{\"purchaseCost\":null,\"isActive\":false}"; await Insert(entry);
        using var admin = await fixture.ClientAsync();
        var visible = await Json(await admin.GetAsync(Path + "/" + entry.Id)); Assert.Equal(1200.25m, visible.GetProperty("oldValues").GetProperty("purchaseCost").GetDecimal());
        Assert.False(visible.GetProperty("snapshotsRedacted").GetBoolean());
        try
        {
            await TogglePermission(Permissions.AssetCost, false); var masked = await Json(await admin.GetAsync(Path + "/" + entry.Id));
            foreach (var key in new[] { "oldValues", "newValues" }) Assert.False(masked.GetProperty(key).TryGetProperty("purchaseCost", out _));
            Assert.True(masked.GetProperty("snapshotsRedacted").GetBoolean());
        }
        finally { await TogglePermission(Permissions.AssetCost, true); }
        Assert.Equal(1200.25m, (await Json(await admin.GetAsync(Path + "/" + entry.Id))).GetProperty("oldValues").GetProperty("purchaseCost").GetDecimal());
    }

    [NeonFact]
    public async Task Live_audit_permission_revocation_denies_an_existing_token_without_view_marker()
    {
        using var admin = await fixture.ClientAsync(); var entry = Log(Marker(), WholeSecond(DateTime.UtcNow.AddHours(-1))); await Insert(entry);
        try
        {
            await TogglePermission(Permissions.AuditRead, false); var before = await ViewCount();
            foreach (var path in new[] { Path, Path + "/" + entry.Id }) await Error(await admin.GetAsync(path), HttpStatusCode.Forbidden, "FORBIDDEN");
            Assert.Equal(before, await ViewCount());
        }
        finally { await TogglePermission(Permissions.AuditRead, true); }
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Path + "/" + entry.Id)).StatusCode);
    }

    [NeonFact]
    public async Task No_audit_write_endpoint_or_schema_change_and_narrow_seed_is_idempotent()
    {
        using var admin = await fixture.ClientAsync(); await using var scope = fixture.Factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>(); var seed = services.GetRequiredService<DevelopmentSeed>();
        var users = await db.Set<User>().CountAsync(); var members = await db.Set<UserRole>().CountAsync(); var grants = await db.Set<RolePermission>().CountAsync();
        var audits = await db.Set<AuditLog>().CountAsync(); Assert.Equal(0, await seed.RunAuditReadAsync()); Assert.Equal(0, await seed.RunAuditReadAsync());
        Assert.Equal(users, await db.Set<User>().CountAsync()); Assert.Equal(members, await db.Set<UserRole>().CountAsync()); Assert.Equal(grants, await db.Set<RolePermission>().CountAsync());
        Assert.Equal(audits, await db.Set<AuditLog>().CountAsync()); Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        foreach (var response in new[] { await admin.PostAsJsonAsync(Path, new { action = "not-allowed" }),
            await admin.PutAsJsonAsync(Path + "/1", new { action = "not-allowed" }), await admin.DeleteAsync(Path + "/1") })
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(audits, await db.Set<AuditLog>().CountAsync());
    }
}
