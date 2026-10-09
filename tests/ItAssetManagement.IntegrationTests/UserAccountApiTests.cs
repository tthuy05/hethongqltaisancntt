using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public sealed class UserAccountHostTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Development"); b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "", ["Jwt:SigningKey"] = "synthetic-account-host-test-key-not-a-real-secret",
          ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false" }));
    });
    [Fact]
    public async Task Anonymous_and_invalid_token_are_401_on_both_operations_without_database()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        foreach (var token in new[] { "", "invalid" })
        {
            client.DefaultRequestHeaders.Authorization = token == "" ? null : new("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync("/api/v1/users/1/status", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/users/1/roles", new { })).StatusCode);
        }
    }
    [Fact]
    public async Task OpenApi_documents_bearer_errors_explicit_roles_and_safe_change_result()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        foreach (var (path, method) in new[] { ("/api/v1/users/{userId}/status", "patch"), ("/api/v1/users/{userId}/roles", "put") })
        {
            var op = doc.GetProperty("paths").GetProperty(path).GetProperty(method); Assert.Single(op.GetProperty("security").EnumerateArray());
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409" }) Assert.True(op.GetProperty("responses").TryGetProperty(status, out _));
        }
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        Assert.Equal(["roleIds", "rowVersion"], schemas.GetProperty("ReplaceUserRolesRequest").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
        Assert.Contains("roleIds", schemas.GetProperty("ReplaceUserRolesRequest").GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(["isAdminLocked", "roleIds", "user", "warnings"], schemas.GetProperty("AccountChangeResult").GetProperty("properties").EnumerateObject().Select(x => x.Name).Order());
    }
}

[Collection("isolated-neon")]
public sealed class UserAccountApiTests(MvpFixture fixture)
{
    private const string Path = "/api/v1/users/";
    private async Task<UserDto> Create(HttpClient client)
    {
        var key = fixture.Prefix + Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/v1/users", new CreateUserRequest
        { Username = key, Email = key + "@fixture.test", DisplayName = "Isolated account test", Password = MvpFixture.Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }
    private async Task<UserDto> Get(HttpClient client, long id) => (await client.GetFromJsonAsync<UserDto>(Path + id))!;
    private async Task<long> Role(string code)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Role>().Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
    }
    private static async Task<AccountChangeResult> Success(HttpResponseMessage response)
    { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<AccountChangeResult>())!; }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString()); Assert.True(json.TryGetProperty("traceId", out _));
    }
    private async Task<AccountChangeResult> Status(HttpClient client, UserDto user, string status) =>
        await Success(await client.PatchAsJsonAsync(Path + user.Id + "/status", new StatusRequest(status, "Isolated administrative reason", user.RowVersion)));
    private async Task<AccountChangeResult> Assign(HttpClient client, UserDto user, params long[] ids) =>
        await Success(await client.PutAsJsonAsync(Path + user.Id + "/roles", new ReplaceUserRolesRequest { RoleIds = ids, RowVersion = user.RowVersion }));

    [NeonFact]
    public async Task Assigning_roles_enables_login_and_replacement_revokes_old_token_without_profile_or_password_reset()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin); var support = await Role("TECHNICAL_SUPPORT");
        var assigned = await Assign(admin, user, support); Assert.Equal([support], assigned.RoleIds); Assert.Empty(assigned.Warnings);
        using var caller = await fixture.ClientAsync(user.Email); Assert.Equal(HttpStatusCode.OK, (await caller.GetAsync("/api/v1/users/lookup")).StatusCode);
        await Error(await caller.PutAsJsonAsync(Path + user.Id + "/roles", new { roleIds = new[] { await Role("ADMIN_IT") }, rowVersion = assigned.User.RowVersion }), HttpStatusCode.Forbidden, "FORBIDDEN");
        var changed = await Assign(admin, await Get(admin, user.Id), await Role("SYSTEM_MANAGER"));
        Assert.Equal(user.Email, changed.User.Email); Assert.Equal(user.DisplayName, changed.User.DisplayName);
        Assert.Equal(HttpStatusCode.Unauthorized, (await caller.GetAsync("/api/v1/auth/me")).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var stored = await db.Set<User>().SingleAsync(x => x.Id == user.Id);
        Assert.True(sp.GetRequiredService<IPasswordService>().Verify(stored, MvpFixture.Password)); Assert.Equal(2, stored.TokenVersion);
        var membership = await db.Set<UserRole>().SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(changed.RoleIds[0], membership.RoleId); Assert.NotNull(membership.AssignedByUserId); Assert.Equal(DateTimeKind.Utc, membership.AssignedAtUtc.Kind);
        Assert.Equal(2, await db.Set<AuditLog>().CountAsync(x => x.EntityId == user.Id.ToString() && x.Action == "users.roles.replace"));
        var cleared = await Assign(admin, changed.User); Assert.Empty(cleared.RoleIds);
        Assert.False(await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id)); Assert.True(await db.Set<User>().AnyAsync(x => x.Id == user.Id));
        using var anonymous = fixture.Factory.CreateClient();
        await Error(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = MvpFixture.Password }), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [NeonFact]
    public async Task Manual_lock_unlock_and_activity_are_orthogonal_and_all_successful_changes_revoke_old_tokens()
    {
        using var admin = await fixture.ClientAsync(); var email = await fixture.UserAsync("manualaccount", "TECHNICAL_SUPPORT");
        using var caller = await fixture.ClientAsync(email); var me = (await caller.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var profile = await Get(admin, me.Id); var locked = await Status(admin, profile, "Locked"); Assert.True(locked.IsAdminLocked); Assert.True(locked.User.IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await caller.GetAsync("/api/v1/auth/me")).StatusCode);
        using var anonymous = fixture.Factory.CreateClient();
        await Error(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = MvpFixture.Password }), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        var inactive = await Status(admin, await Get(admin, me.Id), "Inactive"); Assert.False(inactive.User.IsActive); Assert.True(inactive.IsAdminLocked);
        // The isolated schema now includes asset_assignments. BR-055's approved
        // conservative warning is intentional until per-user allocation review exists;
        // it must not be interpreted as a counted active assignment or auto-return.
        Assert.Equal(["ALLOCATION_REVIEW_REQUIRED"], inactive.Warnings);
        var active = await Status(admin, inactive.User, "Active"); Assert.True(active.User.IsActive); Assert.True(active.IsAdminLocked);
        var unlocked = await Status(admin, active.User, "Unlocked"); Assert.False(unlocked.IsAdminLocked);
        Assert.Empty(locked.Warnings); Assert.Empty(active.Warnings); Assert.Empty(unlocked.Warnings);
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = MvpFixture.Password }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await caller.GetAsync("/api/v1/auth/me")).StatusCode); // Old token never revives.
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Set<User>().SingleAsync(x => x.Id == me.Id); Assert.Null(stored.AdminLockedAtUtc); Assert.Equal(4, stored.TokenVersion);
        Assert.True(await db.Set<UserRole>().AnyAsync(x => x.UserId == me.Id));
        foreach (var audit in await db.Set<AuditLog>().Where(x => x.EntityId == me.Id.ToString() && x.Action == "users.status.change").ToListAsync())
        { Assert.DoesNotContain(email, audit.NewValuesJson!); Assert.DoesNotContain("Isolated administrative reason", audit.NewValuesJson!); Assert.Contains("reasonProvided", audit.NewValuesJson!); }
    }

    [NeonFact]
    public async Task Administrative_unlock_does_not_reset_automatic_lockout_or_failed_login_count()
    {
        using var admin = await fixture.ClientAsync(); var email = await fixture.UserAsync("automaticaccount", "TECHNICAL_SUPPORT", locked: true);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var expiry = DateTime.UtcNow.AddMinutes(15);
        await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var stored = await db.Set<User>().SingleAsync(x => x.Email == email); stored.FailedLoginCount = 5; stored.LockoutEndUtc = expiry;
            sp.GetRequiredService<IAuditWriter>().Record("test.account.lockout", stored, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
        });
        var id = await db.Set<User>().Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        var unlocked = await Status(admin, await Get(admin, id), "Unlocked"); Assert.False(unlocked.IsAdminLocked);
        var result = await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == id); Assert.Equal(5, result.FailedLoginCount);
        Assert.True(result.LockoutEndUtc > DateTime.UtcNow);
        using var anonymous = fixture.Factory.CreateClient();
        await Error(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = MvpFixture.Password }), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [NeonFact]
    public async Task Manager_support_and_self_service_cannot_control_accounts_or_roles()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin);
        foreach (var email in new[] { fixture.ManagerEmail, fixture.SupportEmail })
        {
            using var client = await fixture.ClientAsync(email); var me = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
            foreach (var id in new[] { user.Id, me.Id })
            {
                await Error(await client.PatchAsJsonAsync(Path + id + "/status", new { status = "Active", reason = "Denied", rowVersion = user.RowVersion }), HttpStatusCode.Forbidden, "FORBIDDEN");
                await Error(await client.PutAsJsonAsync(Path + id + "/roles", new { roleIds = new[] { await Role("ADMIN_IT") }, rowVersion = user.RowVersion }), HttpStatusCode.Forbidden, "FORBIDDEN");
            }
        }
    }

    [NeonFact]
    public async Task Invalid_roles_status_unknown_fields_and_missing_roleIds_are_400_and_missing_user_is_404()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin); var path = Path + user.Id;
        foreach (var ids in new long[][] { [0], [-1], [1, 1], [1, 2, 3, 4], [long.MaxValue] })
            await Error(await admin.PutAsJsonAsync(path + "/roles", new { roleIds = ids, rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PutAsJsonAsync(path + "/roles", new { rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PutAsJsonAsync(path + "/roles", new { roleIds = (long[]?)null, rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PutAsJsonAsync(path + "/roles", new { roleIds = Array.Empty<long>(), rowVersion = "bad" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        foreach (var status in new[] { "active", "Disabled", "Lockout", "" })
            await Error(await admin.PatchAsJsonAsync(path + "/status", new { status, reason = "reason", rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PatchAsJsonAsync(path + "/status", new { status = "Locked", reason = " ", rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PatchAsJsonAsync(path + "/status", new { status = "Locked", reason = new string('a', 1001), rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PatchAsJsonAsync(path + "/status", new { status = "Locked", reason = "reason", rowVersion = user.RowVersion, tokenVersion = 0 }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PutAsJsonAsync(path + "/roles", new { roleIds = Array.Empty<long>(), rowVersion = user.RowVersion, isActive = false }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Error(await admin.PutAsJsonAsync(Path + "9223372036854775807/roles", new { roleIds = Array.Empty<long>(), rowVersion = user.RowVersion }), HttpStatusCode.NotFound, "USER_NOT_FOUND");
        await Error(await admin.PatchAsJsonAsync(Path + "0/status", new { status = "Locked", reason = "reason", rowVersion = user.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(user.RowVersion, (await Get(admin, user.Id)).RowVersion);
    }

    [NeonFact]
    public async Task Retained_memberships_keep_ids_and_timestamps_and_inactive_role_is_rejected_without_mutation()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin); var support = await Role("TECHNICAL_SUPPORT"); var manager = await Role("SYSTEM_MANAGER");
        var assigned = await Assign(admin, user, support, manager);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var before = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == user.Id).OrderBy(x => x.RoleId).ToListAsync();
        var repeated = await Assign(admin, assigned.User, manager, support);
        Assert.NotEqual(assigned.User.RowVersion, repeated.User.RowVersion);
        var after = await db.Set<UserRole>().AsNoTracking().Where(x => x.UserId == user.Id).OrderBy(x => x.RoleId).ToListAsync();
        Assert.Equal(before.Select(x => (x.Id, x.AssignedAtUtc)), after.Select(x => (x.Id, x.AssignedAtUtc)));
        async Task Toggle(bool enabled) => await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var role = await db.Set<Role>().SingleAsync(x => x.Id == support); role.IsActive = enabled;
            sp.GetRequiredService<IAuditWriter>().Record("test.account.role", role, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
        });
        try
        {
            await Toggle(false);
            await Error(await admin.PutAsJsonAsync(Path + user.Id + "/roles", new { roleIds = new[] { support }, rowVersion = repeated.User.RowVersion }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
            Assert.Equal(repeated.User.RowVersion, (await Get(admin, user.Id)).RowVersion);
        }
        finally { await Toggle(true); }
    }

    [NeonFact]
    public async Task Noop_roles_and_status_change_version_and_stale_cross_endpoint_writes_are_409()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin); var same = await Assign(admin, user);
        Assert.NotEqual(user.RowVersion, same.User.RowVersion);
        await Error(await admin.PatchAsJsonAsync(Path + user.Id + "/status", new StatusRequest("Locked", "stale", user.RowVersion)), HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
        var status = await Status(admin, same.User, "Active"); Assert.NotEqual(same.User.RowVersion, status.User.RowVersion);
        await Error(await admin.PutAsJsonAsync(Path + user.Id + "/roles", new { roleIds = Array.Empty<long>(), rowVersion = same.User.RowVersion }), HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
        var responses = await Task.WhenAll(admin.PatchAsJsonAsync(Path + user.Id + "/status", new StatusRequest("Inactive", "race", status.User.RowVersion)),
            admin.PutAsJsonAsync(Path + user.Id + "/roles", new { roleIds = new[] { await Role("TECHNICAL_SUPPORT") }, rowVersion = status.User.RowVersion }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    // Temporarily isolate eligibility only in the existing test DB; restore pre-existing
    // users in finally. Never use this helper or these tests against shared development.
    private async Task<List<long>> HideOtherAdmins(long[] keep)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        Assert.Equal(Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE") ?? "it_asset_management_m1_verify_20261002", db.Database.GetDbConnection().Database);
        return await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var ids = from ur in db.Set<UserRole>() join role in db.Set<Role>() on ur.RoleId equals role.Id where role.Code == "ADMIN_IT" select ur.UserId;
            var users = await db.Set<User>().Where(x => ids.Contains(x.Id) && x.IsActive && !keep.Contains(x.Id)).ToListAsync();
            foreach (var user in users) { user.IsActive = false; sp.GetRequiredService<IAuditWriter>().Record("test.admin.isolate", user, null, "SYSTEM"); }
            await db.SaveChangesAsync(); return users.Select(x => x.Id).ToList();
        });
    }
    private async Task RestoreAdmins(List<long> ids)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            foreach (var user in await db.Set<User>().Where(x => ids.Contains(x.Id)).ToListAsync())
            { user.IsActive = true; sp.GetRequiredService<IAuditWriter>().Record("test.admin.restore", user, null, "SYSTEM"); }
            await db.SaveChangesAsync(); return true;
        });
    }
    [NeonFact]
    public async Task Last_available_admin_cannot_be_disabled_locked_or_have_admin_role_removed()
    {
        var email = await fixture.UserAsync("lastadmin", "ADMIN_IT"); using var client = await fixture.ClientAsync(email);
        var me = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!; var user = await Get(client, me.Id);
        var manualEmail = await fixture.UserAsync("blockedlastadmin", "ADMIN_IT", locked: true);
        var automaticEmail = await fixture.UserAsync("autolastadmin", "ADMIN_IT");
        long manualId; long automaticId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
            manualId = await db.Set<User>().Where(x => x.Email == manualEmail).Select(x => x.Id).SingleAsync();
            automaticId = await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var other = await db.Set<User>().SingleAsync(x => x.Email == automaticEmail); other.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15); other.FailedLoginCount = 5;
                sp.GetRequiredService<IAuditWriter>().Record("test.admin.auto-lock", other, null, "SYSTEM"); await db.SaveChangesAsync(); return other.Id;
            });
        }
        var hidden = await HideOtherAdmins([user.Id, manualId, automaticId]);
        try
        {
            foreach (var status in new[] { "Inactive", "Locked" })
                await Error(await client.PatchAsJsonAsync(Path + user.Id + "/status", new StatusRequest(status, "must deny", user.RowVersion)), HttpStatusCode.Conflict, "LAST_ADMIN_PROTECTED");
            await Error(await client.PutAsJsonAsync(Path + user.Id + "/roles", new { roleIds = new[] { await Role("TECHNICAL_SUPPORT") }, rowVersion = user.RowVersion }), HttpStatusCode.Conflict, "LAST_ADMIN_PROTECTED");
            Assert.Equal(user.RowVersion, (await Get(client, user.Id)).RowVersion);
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Set<AuditLog>().AnyAsync(x => x.EntityId == user.Id.ToString() && (x.Action == "users.roles.replace" || x.Action == "users.status.change")));
        }
        finally { await RestoreAdmins(hidden); }
    }
    [NeonFact]
    public async Task Concurrent_self_demotions_of_two_admins_leave_exactly_one_available_admin()
    {
        var oneEmail = await fixture.UserAsync("raceadminone", "ADMIN_IT"); var twoEmail = await fixture.UserAsync("raceadmintwo", "ADMIN_IT");
        using var one = await fixture.ClientAsync(oneEmail); using var two = await fixture.ClientAsync(twoEmail);
        var oneMe = (await one.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!; var twoMe = (await two.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var a = await Get(one, oneMe.Id); var b = await Get(two, twoMe.Id); var support = await Role("TECHNICAL_SUPPORT");
        var hidden = await HideOtherAdmins([a.Id, b.Id]);
        try
        {
            var responses = await Task.WhenAll(one.PutAsJsonAsync(Path + a.Id + "/roles", new { roleIds = new[] { support }, rowVersion = a.RowVersion }),
                two.PutAsJsonAsync(Path + b.Id + "/roles", new { roleIds = new[] { support }, rowVersion = b.RowVersion }));
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var adminRole = await Role("ADMIN_IT");
            Assert.Equal(1, await db.Set<UserRole>().CountAsync(x => (x.UserId == a.Id || x.UserId == b.Id) && x.RoleId == adminRole));
        }
        finally { await RestoreAdmins(hidden); }
    }
    [NeonFact]
    public async Task Token_version_exhaustion_rolls_back_memberships_and_audit_after_intermediate_save()
    {
        using var admin = await fixture.ClientAsync(); var user = await Create(admin);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var target = await db.Set<User>().SingleAsync(x => x.Id == user.Id); target.TokenVersion = int.MaxValue;
            sp.GetRequiredService<IAuditWriter>().Record("test.account.saturation", target, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
        });
        var current = await Get(admin, user.Id);
        await Error(await admin.PutAsJsonAsync(Path + user.Id + "/roles", new { roleIds = new[] { await Role("TECHNICAL_SUPPORT") }, rowVersion = current.RowVersion }), HttpStatusCode.Conflict, "USER_TOKEN_VERSION_EXHAUSTED");
        Assert.Empty(await db.Set<UserRole>().Where(x => x.UserId == user.Id).ToListAsync()); Assert.Equal(current.RowVersion, (await Get(admin, user.Id)).RowVersion);
        Assert.False(await db.Set<AuditLog>().AnyAsync(x => x.Action == "users.roles.replace" && x.EntityId == user.Id.ToString()));
    }
    [NeonFact]
    public async Task Membership_delete_requires_narrow_guard_and_audit_coverage_and_user_delete_is_still_forbidden()
    {
        var email = await fixture.UserAsync("deletionguard", "TECHNICAL_SUPPORT");
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var id = await db.Set<User>().Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == id)); await db.SaveChangesAsync(); return true; }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { sp.GetRequiredService<IAccountPersistence>().RemoveMembership(await db.Set<UserRole>().SingleAsync(x => x.UserId == id)); await db.SaveChangesAsync(); return true; }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { db.Remove(await db.Set<User>().SingleAsync(x => x.Id == id)); await db.SaveChangesAsync(); return true; }));
        Assert.True(await db.Set<UserRole>().AnyAsync(x => x.UserId == id)); Assert.True(await db.Set<User>().AnyAsync(x => x.Id == id));
    }
    private sealed class StaleActor(long id, int version) : IActor
    {
        public long? UserId => id;
        public int? TokenVersion => version;
        public Guid CorrelationId => Guid.NewGuid();
        public string? Method => "PATCH";
        public string? Path => "/api/v1/users/test/status";
        public bool Has(string permission) => true; // Simulates authorization before a racing revocation.
    }
    [NeonFact]
    public async Task Transaction_rechecks_actor_token_version_and_live_permission_not_only_cached_claims()
    {
        using var admin = await fixture.ClientAsync(); var me = (await admin.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!; var user = await Create(admin);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var version = await db.Set<User>().Where(x => x.Id == me.Id).Select(x => x.TokenVersion).SingleAsync();
        UserAccountService Service(int value) => new(sp.GetRequiredService<IRepository>(), sp.GetRequiredService<IAccountPersistence>(),
            sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<IAuditWriter>(), new StaleActor(me.Id, value));
        var error = await Assert.ThrowsAsync<BusinessException>(() => Service(version + 1).StatusAsync(user.Id, new("Inactive", "reason", user.RowVersion), default));
        Assert.Equal(401, error.Status);
        async Task Toggle(bool enabled) => await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var permission = await db.Set<Permission>().SingleAsync(x => x.Code == Permissions.UserStatus); permission.IsActive = enabled;
            sp.GetRequiredService<IAuditWriter>().Record("test.account.permission", permission, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
        });
        try
        {
            await Toggle(false); error = await Assert.ThrowsAsync<BusinessException>(() => Service(version).StatusAsync(user.Id, new("Inactive", "reason", user.RowVersion), default));
            Assert.Equal(403, error.Status);
        }
        finally { await Toggle(true); }
        Assert.Equal(user.RowVersion, (await Get(admin, user.Id)).RowVersion);
    }
}
