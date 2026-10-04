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

public sealed class UserManagementHostTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "", ["Jwt:SigningKey"] = "synthetic-user-admin-host-key-only-for-tests",
            ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false"
        }));
    });
    [Fact]
    public async Task All_admin_routes_require_bearer_without_touching_unconfigured_database()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        foreach (var token in new[] { "", "invalid-token" })
        {
            client.DefaultRequestHeaders.Authorization = token == "" ? null : new("Bearer", token);
            foreach (var (method, path) in new[] { (HttpMethod.Get, "/api/v1/users"), (HttpMethod.Get, "/api/v1/users/1"),
                (HttpMethod.Post, "/api/v1/users"), (HttpMethod.Put, "/api/v1/users/1") })
            {
                using var request = new HttpRequestMessage(method, path);
                if (method != HttpMethod.Get) request.Content = JsonContent.Create(new { });
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
            }
        }
    }
    [Fact]
    public async Task OpenApi_exposes_exact_admin_contracts_without_security_state()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json"); var paths = document.GetProperty("paths");
        foreach (var (path, method) in new[] { ("/api/v1/users", "get"), ("/api/v1/users", "post"),
            ("/api/v1/users/{userId}", "get"), ("/api/v1/users/{userId}", "put") })
            Assert.Single(paths.GetProperty(path).GetProperty(method).GetProperty("security").EnumerateArray());
        Assert.Equal(["departmentId", "keyword", "page", "pageSize", "roleId", "sortBy", "sortDirection", "status"],
            paths.GetProperty("/api/v1/users").GetProperty("get").GetProperty("parameters").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Order());
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.Equal(["departmentId", "displayName", "email", "employeeCode", "password", "phone", "username"],
            schemas.GetProperty("CreateUserRequest").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(["departmentId", "displayName", "email", "employeeCode", "phone", "rowVersion", "username"],
            schemas.GetProperty("UpdateUserRequest").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(["createdAt", "departmentId", "displayName", "email", "employeeCode", "id", "isActive", "phone", "rowVersion", "updatedAt", "username"],
            schemas.GetProperty("UserDto").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
        Assert.True(paths.TryGetProperty("/api/v1/users/{userId}/roles", out _));
        Assert.True(paths.TryGetProperty("/api/v1/users/{userId}/status", out _));
    }
}

[Collection("isolated-neon")]
public sealed class UserManagementApiTests(MvpFixture fixture)
{
    private const string Path = "/api/v1/users";
    private CreateUserRequest Request() => new()
    {
        Username = fixture.Prefix + Guid.NewGuid().ToString("N")[..8],
        Email = fixture.Prefix + Guid.NewGuid().ToString("N")[..8] + "@fixture.test",
        DisplayName = fixture.Prefix + " user admin", EmployeeCode = fixture.Prefix + Guid.NewGuid().ToString("N")[..8],
        Phone = "0123456789", DepartmentId = fixture.DepartmentId, Password = MvpFixture.Password
    };
    private static UpdateUserRequest Update(UserDto user) => new()
    {
        Username = user.Username, Email = user.Email, DisplayName = user.DisplayName, EmployeeCode = user.EmployeeCode,
        DepartmentId = user.DepartmentId, Phone = user.Phone, RowVersion = user.RowVersion
    };
    private async Task<UserDto> Create(HttpClient client, CreateUserRequest? request = null)
    {
        var response = await client.PostAsJsonAsync(Path, request ?? Request()); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        var location = response.Headers.Location!;
        Assert.Equal(Path + "/" + result.Id, location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString);
        return result;
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString()); Assert.True(json.TryGetProperty("traceId", out _));
    }

    [NeonFact]
    public async Task Create_hashes_password_preserves_whitespace_and_creates_no_roles()
    {
        using var client = await fixture.ClientAsync(); var request = Request(); request.Username = " " + request.Username + " ";
        request.Email = " " + request.Email + " "; request.Password = " " + MvpFixture.Password + " ";
        var user = await Create(client, request); Assert.True(user.IsActive); Assert.Equal(request.Username.Trim(), user.Username);
        Assert.Equal(16, Convert.FromBase64String(user.RowVersion).Length);
        var response = await client.GetAsync(Path + "/" + user.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.TryGetProperty("password", out _)); Assert.False(json.TryGetProperty("passwordHash", out _));
        Assert.False(json.TryGetProperty("tokenVersion", out _)); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var stored = await db.Set<User>().SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(request.Password, stored.PasswordHash); Assert.True(sp.GetRequiredService<IPasswordService>().Verify(stored, request.Password));
        Assert.False(sp.GetRequiredService<IPasswordService>().Verify(stored, request.Password.Trim()));
        Assert.Equal(user.Email.ToUpperInvariant(), stored.NormalizedEmail); Assert.Equal(user.Username.ToUpperInvariant(), stored.NormalizedUsername);
        Assert.False(await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id));
        using var anonymous = fixture.Factory.CreateClient();
        await Error(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { user.Email, request.Password }), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [NeonFact]
    public async Task Manager_and_support_cannot_list_read_create_or_update_even_their_own_profile()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin);
        foreach (var email in new[] { fixture.ManagerEmail, fixture.SupportEmail })
        {
            using var client = await fixture.ClientAsync(email);
            await Error(await client.GetAsync(Path), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Error(await client.GetAsync(Path + "/" + user.Id), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Error(await client.PostAsJsonAsync(Path, Request()), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Error(await client.PutAsJsonAsync(Path + "/" + user.Id, Update(user)), HttpStatusCode.Forbidden, "FORBIDDEN");
            var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me");
            await Error(await client.GetAsync(Path + "/" + me!.Id), HttpStatusCode.Forbidden, "FORBIDDEN");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path + "/lookup")).StatusCode);
        }
    }

    [NeonFact]
    public async Task Duplicate_email_username_employee_code_are_case_insensitive_and_do_not_write_audit()
    {
        using var client = await fixture.ClientAsync(); var first = await Create(client);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audits = await db.Set<AuditLog>().CountAsync(x => x.Action == "users.create");
        foreach (var field in new[] { "Email", "Username", "EmployeeCode" })
        {
            var request = Request(); typeof(CreateUserRequest).GetProperty(field)!.SetValue(request,
                " " + ((string)typeof(UserDto).GetProperty(field)!.GetValue(first)!).ToLowerInvariant() + " ");
            var code = field == "EmployeeCode" ? "USER_EMPLOYEE_CODE_CONFLICT" : "USER_" + field.ToUpperInvariant() + "_CONFLICT";
            await Error(await client.PostAsJsonAsync(Path, request), HttpStatusCode.Conflict, code);
        }
        Assert.Equal(audits, await db.Set<AuditLog>().CountAsync(x => x.Action == "users.create"));
        var nullable = Request(); nullable.EmployeeCode = " "; nullable.DepartmentId = null; nullable.Phone = " ";
        var created = await Create(client, nullable); Assert.Null(created.EmployeeCode); Assert.Null(created.DepartmentId); Assert.Null(created.Phone);
        nullable = Request(); nullable.EmployeeCode = null; await Create(client, nullable);
    }

    [NeonFact]
    public async Task Database_unique_constraints_are_mapped_to_safe_errors_with_transaction_rollback()
    {
        using var client = await fixture.ClientAsync(); var first = await Create(client);
        foreach (var (field, code) in new[] { ("Email", "USER_EMAIL_CONFLICT"), ("Username", "USER_USERNAME_CONFLICT"), ("EmployeeCode", "USER_EMPLOYEE_CODE_CONFLICT") })
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
            var count = await db.Set<User>().CountAsync(); var identity = Request();
            var entity = new User { Username = identity.Username, NormalizedUsername = identity.Username.ToUpperInvariant(), Email = identity.Email,
                NormalizedEmail = identity.Email.ToUpperInvariant(), FullName = identity.DisplayName, EmployeeCode = identity.EmployeeCode };
            entity.PasswordHash = sp.GetRequiredService<IPasswordService>().Hash(entity, MvpFixture.Password);
            if (field == "Email") entity.NormalizedEmail = first.Email.ToUpperInvariant();
            else if (field == "Username") entity.NormalizedUsername = first.Username.ToUpperInvariant();
            else entity.EmployeeCode = first.EmployeeCode!.ToLowerInvariant();
            var error = await Assert.ThrowsAsync<BusinessException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                db.Add(entity); sp.GetRequiredService<IAuditWriter>().Record("test.user.conflict", entity, null, "SYSTEM");
                await db.SaveChangesAsync(); return true;
            }));
            Assert.Equal(409, error.Status); Assert.Equal(code, error.Code); Assert.Equal(count, await db.Set<User>().CountAsync());
        }
    }

    [NeonFact]
    public async Task Put_clears_nullable_fields_preserves_security_and_regenerates_version_with_safe_audit()
    {
        using var client = await fixture.ClientAsync(); var user = await Create(client); var request = Update(user);
        request.DisplayName = "Changed sensitive name"; request.EmployeeCode = null; request.Phone = null; request.DepartmentId = null;
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == user.Id);
        var response = await client.PutAsJsonAsync(Path + "/" + user.Id, request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<UserDto>())!; Assert.Null(result.Phone); Assert.Null(result.EmployeeCode); Assert.Null(result.DepartmentId);
        Assert.NotEqual(user.RowVersion, result.RowVersion); Assert.NotNull(result.UpdatedAtUtc);
        var after = await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == user.Id);
        Assert.Equal(before.PasswordHash, after.PasswordHash); Assert.Equal(before.IsActive, after.IsActive);
        Assert.Equal(before.IsAdminLocked, after.IsAdminLocked); Assert.Equal(before.TokenVersion, after.TokenVersion);
        Assert.False(await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id));
        var audit = await db.Set<AuditLog>().SingleAsync(x => x.EntityId == user.Id.ToString() && x.Action == "users.update");
        Assert.Equal(after.UpdatedByUserId, audit.ActorUserId); Assert.Equal("USER", audit.ActorType); Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        var old = JsonSerializer.Deserialize<JsonElement>(audit.OldValuesJson!); Assert.Equal(fixture.DepartmentId, old.GetProperty("DepartmentId").GetInt64());
        var snapshot = JsonSerializer.Deserialize<JsonElement>(audit.NewValuesJson!); Assert.True(snapshot.GetProperty("displayNameChanged").GetBoolean());
        Assert.DoesNotContain(request.DisplayName, audit.NewValuesJson!); Assert.DoesNotContain(before.PasswordHash, audit.NewValuesJson!);
        Assert.DoesNotContain(user.Email, audit.NewValuesJson!); Assert.DoesNotContain(user.Phone!, audit.NewValuesJson!);
    }

    [NeonFact]
    public async Task Stale_noop_and_concurrent_updates_have_one_winner()
    {
        using var client = await fixture.ClientAsync(); var user = await Create(client); var request = Update(user); var path = Path + "/" + user.Id;
        var responses = await Task.WhenAll(client.PutAsJsonAsync(path, request), client.PutAsJsonAsync(path, request));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await Error(await client.PutAsJsonAsync(path, request), HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
        var current = (await client.GetFromJsonAsync<UserDto>(path))!; request = Update(current); request.DisplayName = "Edited";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, request)).StatusCode);
    }

    [NeonFact]
    public async Task Profile_identity_change_revokes_old_token_without_password_or_role_change()
    {
        var email = await fixture.UserAsync("profileidentity", "TECHNICAL_SUPPORT"); using var oldClient = await fixture.ClientAsync(email);
        var me = (await oldClient.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!; using var admin = await fixture.ClientAsync();
        var profile = (await admin.GetFromJsonAsync<UserDto>(Path + "/" + me.Id))!; var request = Update(profile);
        request.Email = fixture.Prefix + "changed@fixture.test"; request.Username += "changed";
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(Path + "/" + me.Id, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/v1/auth/me")).StatusCode);
        using var anonymous = fixture.Factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { request.Email, password = MvpFixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(request.Email, json.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(["TECHNICAL_SUPPORT"], json.GetProperty("user").GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
    }

    [NeonFact]
    public async Task Inactive_department_cannot_be_new_reference_but_existing_reference_can_be_retained()
    {
        using var client = await fixture.ClientAsync();
        var department = await client.PostAsJsonAsync("/api/v1/departments", new { code = fixture.Prefix + "userdept", name = "Profile test department" });
        Assert.Equal(HttpStatusCode.Created, department.StatusCode); var dept = (await department.Content.ReadFromJsonAsync<MasterDto>())!;
        var create = Request(); create.DepartmentId = dept.Id; var user = await Create(client, create);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/v1/departments/" + dept.Id + "/status",
            new { status = "Inactive", reason = "Isolated user reference test", rowVersion = dept.RowVersion })).StatusCode);
        var request = Update(user); request.DisplayName += " edited";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Path + "/" + user.Id, request)).StatusCode);
        create = Request(); create.DepartmentId = dept.Id; await Error(await client.PostAsJsonAsync(Path, create), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        create.DepartmentId = long.MaxValue; await Error(await client.PostAsJsonAsync(Path, create), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        var other = await Create(client); var move = Update(other); move.DepartmentId = dept.Id;
        await Error(await client.PutAsJsonAsync(Path + "/" + other.Id, move), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [NeonFact]
    public async Task Directory_search_filters_role_and_paging_execute_on_PostgreSql()
    {
        using var client = await fixture.ClientAsync(); var name = fixture.Prefix + " directory%_\\literal"; var ids = new List<long>();
        for (var i = 0; i < 3; i++) { var request = Request(); request.DisplayName = name; ids.Add((await Create(client, request)).Id); }
        var path = Path + "?keyword=" + Uri.EscapeDataString(name.ToUpperInvariant()) + $"&departmentId={fixture.DepartmentId}&status=Active&pageSize=2&sortDirection=desc";
        var first = (await client.GetFromJsonAsync<PagedResponse<UserDto>>(path))!; Assert.Equal(3, first.TotalItems); Assert.Equal(2, first.TotalPages);
        Assert.Equal(ids.Take(2), first.Items.Select(x => x.Id));
        var second = (await client.GetFromJsonAsync<PagedResponse<UserDto>>(path + "&page=2"))!; Assert.Equal(ids.Skip(2), second.Items.Select(x => x.Id));
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<UserDto>>(path + "&page=99"))!.Items);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var supportRole = await db.Set<Role>().Where(x => x.Code == "TECHNICAL_SUPPORT").Select(x => x.Id).SingleAsync();
        var members = (await client.GetFromJsonAsync<PagedResponse<UserDto>>(Path + "?roleId=" + supportRole + "&pageSize=100"))!;
        Assert.NotEmpty(members.Items);
        foreach (var user in members.Items) Assert.True(await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id && x.RoleId == supportRole));
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<UserDto>>(Path + "?roleId=9223372036854775807"))!.Items);
        var inactive = (await client.GetFromJsonAsync<PagedResponse<UserDto>>(Path + "?status=Inactive&keyword=" + Uri.EscapeDataString(fixture.InactiveEmail)))!;
        Assert.Single(inactive.Items); Assert.False(inactive.Items[0].IsActive);
        foreach (var sort in new[] { "id", "username", "email", "displayName", "createdAt", "updatedAt" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path + "?sortBy=" + sort + "&sortDirection=desc")).StatusCode);
    }

    [NeonFact]
    public async Task Competing_creates_with_same_email_have_one_winner_and_update_conflict_rolls_back()
    {
        using var client = await fixture.ClientAsync(); var one = Request(); var two = Request(); two.Email = one.Email.ToUpperInvariant();
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Path, one), client.PostAsJsonAsync(Path, two));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var winner = (await responses.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<UserDto>())!;
        var other = await Create(client); var update = Update(other); update.Email = winner.Email; update.DisplayName = "Must roll back";
        await Error(await client.PutAsJsonAsync(Path + "/" + other.Id, update), HttpStatusCode.Conflict, "USER_EMAIL_CONFLICT");
        var unchanged = (await client.GetFromJsonAsync<UserDto>(Path + "/" + other.Id))!;
        Assert.Equal(other.RowVersion, unchanged.RowVersion); Assert.Equal(other.DisplayName, unchanged.DisplayName); Assert.Equal(other.Email, unchanged.Email);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<AuditLog>().AnyAsync(x => x.EntityId == other.Id.ToString() && x.Action == "users.update"));
    }

    [NeonFact]
    public async Task Revoked_read_permission_denies_existing_admin_token_and_is_restored_in_finally()
    {
        using var client = await fixture.ClientAsync(); var me = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        async Task Toggle(bool enabled)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var permission = await db.Set<Permission>().SingleAsync(x => x.Code == Permissions.UserRead); permission.IsActive = enabled;
                sp.GetRequiredService<IAuditWriter>().Record("test.user.permission", permission, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
            });
        }
        try
        {
            await Toggle(false); await Error(await client.GetAsync(Path), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Error(await client.GetAsync(Path + "/" + me.Id), HttpStatusCode.Forbidden, "FORBIDDEN");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path + "/lookup")).StatusCode);
        }
        finally { await Toggle(true); }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path)).StatusCode);
    }

    [NeonFact]
    public async Task Invalid_and_mass_assignment_requests_are_400_and_missing_user_is_404()
    {
        using var client = await fixture.ClientAsync(); var user = await Create(client);
        foreach (var query in new[] { "page=0", "page=no", "pageSize=101", "page=2147483647", "departmentId=0", "roleId=-1",
            "status=Locked", "status=active", "sortBy=passwordHash", "sortDirection=DESC", "page=1&page=2", "Page=1", "includeSecrets=true" })
            await Error(await client.GetAsync(Path + "?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        foreach (var property in new[] { "isActive", "isAdminLocked", "tokenVersion", "passwordHash", "roles", "roleIds", "createdByUserId", "rowVersion" })
        {
            var body = JsonSerializer.SerializeToElement(Request()); var fields = body.EnumerateObject().ToDictionary(x => x.Name, x => (object)x.Value);
            fields[property] = "must reject";
            await Error(await client.PostAsJsonAsync(Path, fields), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        }
        foreach (var property in new[] { "password", "roles", "isActive", "isAdminLocked", "tokenVersion", "normalizedEmail" })
        {
            var fields = JsonSerializer.SerializeToElement(Update(user)).EnumerateObject().ToDictionary(x => x.Name, x => (object)x.Value);
            fields[property] = "must reject";
            await Error(await client.PutAsJsonAsync(Path + "/" + user.Id, fields), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        }
        var bad = Request(); bad.Password = "short"; await Error(await client.PostAsJsonAsync(Path, bad), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        var update = Update(user); update.RowVersion = "invalid";
        await Error(await client.PutAsJsonAsync(Path + "/" + user.Id, update), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync(Path + "/0"), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await client.GetAsync(Path + "/9223372036854775807"), HttpStatusCode.NotFound, "USER_NOT_FOUND");
        await Error(await client.PutAsJsonAsync(Path + "/9223372036854775807", Update(user)), HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }
}
