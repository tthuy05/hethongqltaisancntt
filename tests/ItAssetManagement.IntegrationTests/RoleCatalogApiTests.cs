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

public sealed class RoleCatalogHostTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "", ["Jwt:SigningKey"] = "synthetic-role-catalog-host-key-only-for-tests",
          ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false" }));
    });
    [Fact]
    public async Task Anonymous_and_invalid_bearer_cannot_read_catalog_or_account_without_DB()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        foreach (var token in new[] { "", "bad-token" })
        {
            client.DefaultRequestHeaders.Authorization = token == "" ? null : new("Bearer", token);
            foreach (var path in new[] { "/api/v1/roles", "/api/v1/roles/permissions", "/api/v1/roles/1", "/api/v1/users/1/account" })
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        }
    }
    [Fact]
    public async Task OpenApi_describes_safe_reads_and_no_role_definition_writes()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        foreach (var path in new[] { "/api/v1/roles", "/api/v1/roles/permissions", "/api/v1/roles/{roleId}", "/api/v1/users/{userId}/account" })
        {
            var operations = doc.GetProperty("paths").GetProperty(path);
            Assert.Equal(["get"], operations.EnumerateObject().Select(x => x.Name));
            var get = operations.GetProperty("get"); Assert.Single(get.GetProperty("security").EnumerateArray());
            foreach (var status in new[] { "200", "400", "401", "403" }) Assert.True(get.GetProperty("responses").TryGetProperty(status, out _));
        }
        var account = doc.GetProperty("components").GetProperty("schemas").GetProperty("UserAccountStateDto");
        Assert.Equal(["id", "isActive", "isAdminLocked", "roleIds", "rowVersion"], account.GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
    }
}

[Collection("isolated-neon")]
public sealed class RoleCatalogApiTests(MvpFixture fixture)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    [NeonFact]
    public async Task Admin_gets_actual_role_ids_and_Manager_gets_only_id_label_Support_is_denied()
    {
        using var admin = await fixture.ClientAsync(); using var manager = await fixture.ClientAsync(fixture.ManagerEmail);
        using var support = await fixture.ClientAsync(fixture.SupportEmail);
        var full = await Json(await admin.GetAsync("/api/v1/roles?pageSize=100&status=Active"));
        Assert.Equal(3, full.GetProperty("totalItems").GetInt32());
        var limited = await Json(await manager.GetAsync("/api/v1/roles?pageSize=100&status=Active"));
        Assert.Equal(full.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()),
            limited.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()));
        foreach (var item in limited.GetProperty("items").EnumerateArray())
            Assert.Equal(["id", "name"], item.EnumerateObject().Select(x => x.Name).Order());
        foreach (var item in full.GetProperty("items").EnumerateArray())
            Assert.Equal(["code", "id", "isActive", "name"], item.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(HttpStatusCode.Forbidden, (await support.GetAsync("/api/v1/roles")).StatusCode);
        Assert.Equal(0, (await Json(await manager.GetAsync("/api/v1/roles?keyword=ADMIN_IT"))).GetProperty("totalItems").GetInt32());
    }
    [NeonFact]
    public async Task Permission_catalog_and_role_mapping_are_Admin_only_and_current_not_planned_grants()
    {
        using var admin = await fixture.ClientAsync(); using var manager = await fixture.ClientAsync(fixture.ManagerEmail);
        using var support = await fixture.ClientAsync(fixture.SupportEmail);
        var list = await Json(await admin.GetAsync("/api/v1/roles?status=Active"));
        var role = list.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "ADMIN_IT");
        var id = role.GetProperty("id").GetInt64();
        var detail = await Json(await admin.GetAsync("/api/v1/roles/" + id));
        Assert.Equal(Permissions.All.Order(), detail.GetProperty("permissions").EnumerateArray().Select(x => x.GetProperty("code").GetString()).Order());
        var permissions = await Json(await admin.GetAsync("/api/v1/roles/permissions?module=roles&keyword=read"));
        Assert.Equal([Permissions.RolePermissionsRead, Permissions.RoleRead], permissions.EnumerateArray().Select(x => x.GetProperty("code").GetString()));
        foreach (var client in new[] { manager, support })
            foreach (var path in new[] { "/api/v1/roles/permissions", "/api/v1/roles/" + id })
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    [NeonFact]
    public async Task Paging_filters_literal_keywords_and_invalid_queries_are_translated_by_PostgreSql()
    {
        using var client = await fixture.ClientAsync();
        var first = await Json(await client.GetAsync("/api/v1/roles?pageSize=1&sortBy=id&sortDirection=desc"));
        var second = await Json(await client.GetAsync("/api/v1/roles?pageSize=1&page=2&sortBy=id&sortDirection=desc"));
        Assert.True(first.GetProperty("items")[0].GetProperty("id").GetInt64() > second.GetProperty("items")[0].GetProperty("id").GetInt64());
        var past = await Json(await client.GetAsync("/api/v1/roles?page=99")); Assert.Empty(past.GetProperty("items").EnumerateArray());
        Assert.Equal(3, past.GetProperty("totalItems").GetInt32());
        foreach (var keyword in new[] { "%", "_", "\\", "' OR 1=1 --" })
            Assert.Equal(0, (await Json(await client.GetAsync("/api/v1/roles?keyword=" + Uri.EscapeDataString(keyword)))).GetProperty("totalItems").GetInt32());
        foreach (var query in new[] { "page=0", "pageSize=101", "page=2147483647", "page=no", "status=active", "sortBy=code",
            "sortBy=description", "sortDirection=DESC", "Page=1", "page=1&page=2", "includePermissions=true", "keyword=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/roles?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/roles/permissions?module=" + new string('x', 101))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/roles/permissions?keyword=x&keyword=y")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/roles/0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/roles/9223372036854775807")).StatusCode);
    }
    [NeonFact]
    public async Task Account_read_is_minimal_and_does_not_change_original_profile_or_security_state()
    {
        using var admin = await fixture.ClientAsync(); using var manager = await fixture.ClientAsync(fixture.ManagerEmail);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Set<User>().SingleAsync(x => x.Email == fixture.ManagerEmail);
        var expected = await db.Set<UserRole>().Where(x => x.UserId == user.Id).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync();
        var state = await Json(await admin.GetAsync("/api/v1/users/" + user.Id + "/account"));
        Assert.Equal(["id", "isActive", "isAdminLocked", "roleIds", "rowVersion"], state.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(expected, state.GetProperty("roleIds").EnumerateArray().Select(x => x.GetInt64()));
        Assert.Equal(Convert.ToBase64String(user.RowVersion), state.GetProperty("rowVersion").GetString());
        Assert.False(state.GetProperty("isAdminLocked").GetBoolean());
        var profile = await Json(await admin.GetAsync("/api/v1/users/" + user.Id));
        Assert.False(profile.TryGetProperty("roleIds", out _)); Assert.False(profile.TryGetProperty("isAdminLocked", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/users/" + user.Id + "/account")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/users/0/account")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/users/9223372036854775807/account")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/users/" + user.Id + "/account?includeHash=true")).StatusCode);
    }
    [NeonFact]
    public async Task Catalog_permission_revocation_denies_an_existing_token_and_restores_in_finally()
    {
        using var client = await fixture.ClientAsync(fixture.ManagerEmail);
        async Task Toggle(bool active)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<AppDbContext>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var permission = await db.Set<Permission>().SingleAsync(x => x.Code == Permissions.RoleRead);
                permission.IsActive = active; sp.GetRequiredService<IAuditWriter>().Record("test.role-catalog.permission", permission, null, "SYSTEM");
                await db.SaveChangesAsync(); return true;
            });
        }
        try { await Toggle(false); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/roles")).StatusCode); }
        finally { await Toggle(true); }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/roles")).StatusCode);
    }
    [NeonFact]
    public async Task Read_only_calls_preserve_audits_users_schema_and_definition_writes_are_not_allowed()
    {
        using var client = await fixture.ClientAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Set<User>().FirstAsync(); var before = await db.Set<AuditLog>().CountAsync(); var count = await db.Set<User>().CountAsync();
        await Json(await client.GetAsync("/api/v1/roles")); await Json(await client.GetAsync("/api/v1/roles/permissions"));
        await Json(await client.GetAsync("/api/v1/users/" + user.Id + "/account"));
        Assert.Equal(before, await db.Set<AuditLog>().CountAsync()); Assert.Equal(count, await db.Set<User>().CountAsync());
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/roles", new { code = "CUSTOM" })).StatusCode);
    }
    [NeonFact]
    public async Task Narrow_catalog_seed_is_idempotent_and_preserves_users_memberships_and_other_grants()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>(); var seed = sp.GetRequiredService<DevelopmentSeed>();
        var users = await db.Set<User>().CountAsync(); var members = await db.Set<UserRole>().CountAsync();
        var grants = await db.Set<RolePermission>().CountAsync(); var audits = await db.Set<AuditLog>().CountAsync();
        Assert.Equal(0, await seed.RunRoleCatalogAsync()); Assert.Equal(0, await seed.RunRoleCatalogAsync());
        Assert.Equal(users, await db.Set<User>().CountAsync()); Assert.Equal(members, await db.Set<UserRole>().CountAsync());
        Assert.Equal(grants, await db.Set<RolePermission>().CountAsync()); Assert.Equal(audits, await db.Set<AuditLog>().CountAsync());
    }
}
