using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

public sealed class UserLookupHostTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "",
            ["Jwt:SigningKey"] = "synthetic-user-lookup-host-key-only-for-tests",
            ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false"
        }));
    });

    [Fact]
    public async Task Anonymous_and_invalid_token_are_401_without_a_database_connection()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        foreach (var token in new[] { "", "invalid-token" })
        {
            client.DefaultRequestHeaders.Authorization = token == "" ? null : new("Bearer", token);
            var response = await client.GetAsync("/api/v1/users/lookup");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("UNAUTHORIZED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task OpenApi_documents_bearer_query_parameters_and_minimal_response()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operation = document.GetProperty("paths").GetProperty("/api/v1/users/lookup").GetProperty("get");
        Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.Equal(["departmentId", "keyword", "page", "pageSize", "sortBy", "sortDirection", "status"],
            operation.GetProperty("parameters").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Order());
        foreach (var status in new[] { "200", "400", "401", "403" }) Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        var dto = document.GetProperty("components").GetProperty("schemas").GetProperty("UserLookupDto");
        Assert.Equal(["departmentId", "displayName", "id"], dto.GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
    }
}

[Collection("isolated-neon")]
public sealed class UserLookupApiTests(MvpFixture fixture)
{
    private const string Path = "/api/v1/users/lookup";
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<long> RecipientAsync(string name, long? departmentId = null, bool active = true)
    {
        var email = await fixture.UserAsync("lookup" + Guid.NewGuid().ToString("N")[..8], "TECHNICAL_SUPPORT", active);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        return await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var user = await db.Set<User>().SingleAsync(x => x.Email == email);
            user.FullName = name; user.DepartmentId = departmentId;
            sp.GetRequiredService<IAuditWriter>().Record("test.lookup.recipient", user, null, "SYSTEM");
            await db.SaveChangesAsync(); return user.Id;
        });
    }

    [NeonFact]
    public async Task All_three_roles_can_read_only_the_minimal_field_set()
    {
        foreach (var email in new[] { MvpFixture.AdminEmail, fixture.ManagerEmail, fixture.SupportEmail })
        {
            using var client = await fixture.ClientAsync(email);
            var response = await Json(await client.GetAsync(Path + "?pageSize=100"));
            Assert.NotEmpty(response.GetProperty("items").EnumerateArray());
            foreach (var item in response.GetProperty("items").EnumerateArray())
                Assert.Equal(["departmentId", "displayName", "id"], item.EnumerateObject().Select(x => x.Name).Order());
            // Email is not a searchable field, even when the authenticated caller knows it.
            var emailSearch = await Json(await client.GetAsync(Path + "?keyword=" + Uri.EscapeDataString(email)));
            Assert.Equal(0, emailSearch.GetProperty("totalItems").GetInt32());
        }
    }

    [NeonFact]
    public async Task Active_filter_department_and_stable_paging_are_translated_to_PostgreSql()
    {
        var name = fixture.Prefix + " lookup paging";
        var ids = new List<long>();
        for (var i = 0; i < 3; i++) ids.Add(await RecipientAsync(name, fixture.DepartmentId));
        await RecipientAsync(name, fixture.DepartmentId, active: false);
        await RecipientAsync(name); // Nullable department is valid but does not match a department filter.
        using var client = await fixture.ClientAsync(fixture.SupportEmail);
        var query = Path + $"?keyword={Uri.EscapeDataString(name.ToUpperInvariant())}&departmentId={fixture.DepartmentId}&status=Active&pageSize=2&sortDirection=desc";
        var first = await Json(await client.GetAsync(query));
        Assert.Equal(3, first.GetProperty("totalItems").GetInt32()); Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(ids.Take(2), first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()));
        var second = await Json(await client.GetAsync(query + "&page=2"));
        Assert.Equal(ids.Skip(2), second.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()));
        var past = await Json(await client.GetAsync(query + "&page=99"));
        Assert.Empty(past.GetProperty("items").EnumerateArray()); Assert.Equal(3, past.GetProperty("totalItems").GetInt32());
        var allDepartments = await Json(await client.GetAsync(Path + "?keyword=" + Uri.EscapeDataString(name) + "&sortBy=id&sortDirection=desc"));
        Assert.Equal(4, allDepartments.GetProperty("totalItems").GetInt32());
        Assert.Equal(JsonValueKind.Null, allDepartments.GetProperty("items")[0].GetProperty("departmentId").ValueKind);
    }

    [NeonFact]
    public async Task Search_treats_wildcards_backslash_and_SQL_text_as_literal_values()
    {
        var name = fixture.Prefix + " literal%_\\' OR 1=1 --";
        var id = await RecipientAsync(name);
        await RecipientAsync(fixture.Prefix + " literalXX-other");
        using var client = await fixture.ClientAsync();
        foreach (var keyword in new[] { name, fixture.Prefix + " literal%_", fixture.Prefix + " literal%_\\' OR 1=1 --" })
        {
            var result = await Json(await client.GetAsync(Path + "?keyword=" + Uri.EscapeDataString(keyword)));
            Assert.Equal(1, result.GetProperty("totalItems").GetInt32());
            Assert.Equal(id, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        }
    }

    [NeonFact]
    public async Task Invalid_duplicate_unknown_and_sensitive_query_parameters_are_400()
    {
        using var client = await fixture.ClientAsync();
        foreach (var query in new[] { "page=0", "page=-1", "page=2147483647", "page=no", "pageSize=0", "pageSize=101",
            "pageSize=999999999999999", "departmentId=0", "departmentId=-1", "departmentId=no", "status=Inactive", "status=active",
            "sortBy=email", "sortBy=passwordHash", "sortDirection=DESC", "page=1&page=2", "Page=1", "includeInactive=true",
            "roles=ADMIN_IT", "assetTypeId=1", "keyword=" + new string('a', 201) })
        {
            var response = await client.GetAsync(Path + "?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
            Assert.True(error.TryGetProperty("traceId", out _)); Assert.True(error.TryGetProperty("errors", out _));
        }
        var empty = await Json(await client.GetAsync(Path + "?departmentId=9223372036854775807"));
        Assert.Equal(0, empty.GetProperty("totalItems").GetInt32());
    }

    [NeonFact]
    public async Task Disabling_permission_denies_an_existing_token_and_restoring_allows_it()
    {
        using var client = await fixture.ClientAsync(fixture.SupportEmail);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path)).StatusCode);
        async Task ToggleAsync(bool enabled)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<AppDbContext>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var permission = await db.Set<Permission>().SingleAsync(x => x.Code == Permissions.UserLookup);
                permission.IsActive = enabled;
                sp.GetRequiredService<IAuditWriter>().Record("test.lookup.permission", permission, null, "SYSTEM");
                await db.SaveChangesAsync(); return true;
            });
        }
        try
        {
            await ToggleAsync(false);
            var response = await client.GetAsync(Path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("FORBIDDEN", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        finally { await ToggleAsync(true); }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path)).StatusCode);
    }

    [NeonFact]
    public async Task Deactivating_the_caller_invalidates_an_existing_token()
    {
        var email = await fixture.UserAsync("lookupcaller", "TECHNICAL_SUPPORT");
        using var client = await fixture.ClientAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path)).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var user = await db.Set<User>().SingleAsync(x => x.Email == email); user.IsActive = false;
            sp.GetRequiredService<IAuditWriter>().Record("test.lookup.caller", user, null, "SYSTEM");
            await db.SaveChangesAsync(); return true;
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path)).StatusCode);
    }

    [NeonFact]
    public async Task Lookup_is_read_only_and_role_definition_writes_remain_unimplemented()
    {
        using var client = await fixture.ClientAsync();
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Set<User>().CountAsync(); var audits = await db.Set<AuditLog>().CountAsync();
        await Json(await client.GetAsync(Path));
        Assert.Equal(users, await db.Set<User>().CountAsync()); Assert.Equal(audits, await db.Set<AuditLog>().CountAsync());
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/roles", new { code = "CUSTOM" })).StatusCode);
    }
}
