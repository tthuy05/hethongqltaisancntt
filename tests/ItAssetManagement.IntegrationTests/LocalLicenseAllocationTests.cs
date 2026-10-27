using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalLicenseAllocationTests
{
    private const string Path = "/api/v1/license-assignments";
    private static async Task WaitersAsync(int count) {
        await using var observer = LocalSequenceAuthenticationTests.Database(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (await observer.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'").SingleAsync(deadline.Token) < count) await Task.Delay(20, deadline.Token);
    }
    [LocalPostgresFact]
    public async Task Revoke_waits_for_capacity_lock_and_denies_permission_revoked_during_wait()
    {
        await using var app = Factory(); await LocalAssignmentCompletionTests.SeedAsync(app); using var admin = await LocalAssignmentCompletionTests.LoginAsync(app);
        var license = await LicenseAsync(admin); var refs = await LocalAssignmentCompletionTests.References(app); var assignment = await AssignAsync(admin, license.Id, refs.Admin);
        var actor = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER", Permissions.LicenseRead, LicensePermissions.AssignmentRead, LicensePermissions.AssignmentRevoke);
        using var writer = await LocalAssignmentCompletionTests.LoginAsync(app, actor.Email);
        await using var blocker = LocalSequenceAuthenticationTests.Database(); await using var tx = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM software_licenses WHERE id={license.Id} FOR UPDATE");
        var pending = writer.PostAsJsonAsync(Path + $"/{assignment.Id}/revoke", new RevokeLicenseAssignmentRequest { RowVersion = assignment.RowVersion });
        try {
            await WaitersAsync(1); await using var scope = app.Services.CreateAsyncScope(); var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>();
            await services.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                await services.GetRequiredService<IRepository>().LockAsync("users-identity", default);
                var permission = await db.Set<Permission>().SingleAsync(x => x.Code == LicensePermissions.AssignmentRevoke);
                var grant = await db.Set<UserPermission>().SingleAsync(x => x.UserId == actor.Id && x.PermissionId == permission.Id);
                services.GetRequiredService<IAccountPersistence>().RemoveUserPermission(grant); services.GetRequiredService<IAuditWriter>().Record("test.revoke", grant, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
            });
        } finally { await tx.RollbackAsync(); }
        using var response = await pending; Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await admin.GetFromJsonAsync<LicenseAssignmentDto>(Path + "/" + assignment.Id))!.RevokedAtUtc);
    }
    [LocalPostgresFact]
    public async Task Quantity_decrease_racing_assignment_never_overallocates()
    {
        await using var app = Factory(); await LocalAssignmentCompletionTests.SeedAsync(app); using var admin = await LocalAssignmentCompletionTests.LoginAsync(app);
        var license = await LicenseAsync(admin, 2); var refs = await LocalAssignmentCompletionTests.References(app); await AssignAsync(admin, license.Id, refs.Admin);
        var target = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER");
        await using var blocker = LocalSequenceAuthenticationTests.Database(); await using var tx = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM software_licenses WHERE id={license.Id} FOR UPDATE");
        var assign = admin.PostAsJsonAsync(Path, new { softwareLicenseId = license.Id, userId = target.Id });
        var decrease = admin.PutAsJsonAsync($"/api/v1/software-licenses/{license.Id}", new { softwareId = license.Software.Id, licenseType = license.LicenseType, totalQuantity = 1, rowVersion = license.RowVersion });
        try { await WaitersAsync(2); } finally { await tx.RollbackAsync(); }
        var responses = await Task.WhenAll(assign, decrease);
        try { Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in responses) response.Dispose(); }
        var actual = (await admin.GetFromJsonAsync<LicenseDto>($"/api/v1/software-licenses/{license.Id}"))!; Assert.True(actual.UsedQuantity <= actual.TotalQuantity);
    }
    internal static WebApplicationFactory<Program> Factory(bool frontend = false, int? port = null) => LocalAssignmentCompletionTests.Factory(frontend: frontend, port: port, software: true, license: true, allocation: true);
    internal static async Task<LicenseDto> LicenseAsync(HttpClient client, int seats = 1, string type = "PER_USER") {
        using var created = await client.PostAsJsonAsync("/api/v1/software", new { name = "Seat QA " + Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var software = (await created.Content.ReadFromJsonAsync<SoftwareDto>())!;
        using var license = await client.PostAsJsonAsync("/api/v1/software-licenses", new { softwareId = software.Id, licenseType = type, totalQuantity = seats });
        Assert.Equal(HttpStatusCode.Created, license.StatusCode); return (await license.Content.ReadFromJsonAsync<LicenseDto>())!;
    }
    internal static async Task<LicenseAssignmentDto> AssignAsync(HttpClient client, long license, long user) {
        using var response = await client.PostAsJsonAsync(Path, new AssignLicenseRequest { SoftwareLicenseId = license, UserId = user });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<LicenseAssignmentDto>())!;
    }
    [LocalPostgresFact]
    public async Task Real_JWT_assign_transfer_at_full_capacity_revoke_and_history_are_atomic()
    {
        await using var app = Factory(); await LocalAssignmentCompletionTests.SeedAsync(app); using var client = await LocalAssignmentCompletionTests.LoginAsync(app);
        var license = await LicenseAsync(client); var refs = await LocalAssignmentCompletionTests.References(app);
        var target = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER");
        var first = await AssignAsync(client, license.Id, refs.Admin);
        using var transferred = await client.PostAsJsonAsync(Path + $"/{first.Id}/transfer", new TransferLicenseAssignmentRequest { UserId = target.Id, RowVersion = first.RowVersion, Reason = "Isolated transfer" });
        Assert.Equal(HttpStatusCode.OK, transferred.StatusCode); var result = (await transferred.Content.ReadFromJsonAsync<TransferResultDto>())!;
        Assert.NotNull(result.Previous.RevokedAtUtc); Assert.False(result.Previous.IsArchived); Assert.Equal(target.Id, result.Current.AssignedUserId);
        var capacity = (await client.GetFromJsonAsync<LicenseDto>($"/api/v1/software-licenses/{license.Id}"))!; Assert.Equal(1, capacity.UsedQuantity); Assert.Equal(0, capacity.AvailableQuantity);
        var history = (await client.GetFromJsonAsync<PagedResponse<LicenseAssignmentDto>>($"/api/v1/software-licenses/{license.Id}/assignments?status=Revoked&sortBy=id&sortDirection=asc"))!;
        Assert.Equal(first.Id, Assert.Single(history.Items).Id);
        using var stale = await client.PostAsJsonAsync(Path + $"/{first.Id}/revoke", new RevokeLicenseAssignmentRequest { RowVersion = first.RowVersion }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var revoked = await client.PostAsJsonAsync(Path + $"/{result.Current.Id}/revoke", new RevokeLicenseAssignmentRequest { RowVersion = result.Current.RowVersion, Reason = "Seat returned" }); Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<LicenseDto>($"/api/v1/software-licenses/{license.Id}"))!.UsedQuantity);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.Set<LicenseAssignment>().CountAsync(x => x.SoftwareLicenseId == license.Id));
        Assert.Equal(4, await db.Set<AuditLog>().CountAsync(x => x.EntityType == nameof(LicenseAssignment) && (x.EntityId == first.Id.ToString() || x.EntityId == result.Current.Id.ToString())));
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [LocalPostgresFact]
    public async Task Concurrent_last_seat_has_one_winner_and_catalog_seed_is_idempotent()
    {
        await using var app = Factory(); await LocalAssignmentCompletionTests.SeedAsync(app); using var client = await LocalAssignmentCompletionTests.LoginAsync(app);
        var license = await LicenseAsync(client); var refs = await LocalAssignmentCompletionTests.References(app);
        var other = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER");
        var results = await Task.WhenAll(new[] { refs.Admin, other.Id }.Select(id => client.PostAsJsonAsync(Path, new { softwareLicenseId = license.Id, userId = id })));
        try { Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in results) response.Dispose(); }
        Assert.Equal(1, (await client.GetFromJsonAsync<LicenseDto>($"/api/v1/software-licenses/{license.Id}"))!.UsedQuantity);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunLicenseAllocationPermissionsAsync());
        var grants = from rp in db.Set<RolePermission>() join p in db.Set<Permission>() on rp.PermissionId equals p.Id join role in db.Set<Role>() on rp.RoleId equals role.Id where LicensePermissions.Assignments.Contains(p.Code) select role.Code;
        Assert.Equal(8, await grants.CountAsync()); Assert.DoesNotContain("TECHNICAL_SUPPORT", await grants.ToListAsync());
    }
    [LocalPostgresFact]
    public async Task Real_custom_grants_type_expiry_validation_and_privacy_are_enforced()
    {
        await using var app = Factory(); await LocalAssignmentCompletionTests.SeedAsync(app); using var admin = await LocalAssignmentCompletionTests.LoginAsync(app);
        var license = await LicenseAsync(admin); var refs = await LocalAssignmentCompletionTests.References(app); var asset = await LocalAssignmentCompletionTests.AssetAsync(admin, refs.Department, refs.Type);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Path, new { softwareLicenseId = license.Id, assetId = asset.Id })).StatusCode);
        await AssignAsync(admin, license.Id, refs.Admin);
        var reader = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER", Permissions.LicenseRead, LicensePermissions.AssignmentRead);
        using var restricted = await LocalAssignmentCompletionTests.LoginAsync(app, reader.Email);
        using var list = await restricted.GetAsync(Path + "?licenseId=" + license.Id); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync()); var row = json.RootElement.GetProperty("items")[0];
        foreach (var secret in new[] { "assignedUserEmail", "purchaseCost", "licenseKey", "ciphertext" }) Assert.False(row.TryGetProperty(secret, out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await restricted.PostAsJsonAsync(Path, new { softwareLicenseId = license.Id, userId = reader.Id })).StatusCode);
        foreach (var query in new[] { "page=0", "page=2147483647&pageSize=100", "status=Bad", "targetType=Bad", "sortBy=secret", "from=not-a-date", "licenseId=0", "page=1&page=2" }) Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Path + "?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Path + "?from=2026-01-01T00:00:00%2B07:00")).StatusCode); // HTTP query binder normalizes offsets to UTC.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/software-licenses/9223372036854775807/assignments")).StatusCode);
        var expired = await LicenseAsync(admin);
        using var change = await admin.PutAsJsonAsync($"/api/v1/software-licenses/{expired.Id}", new { softwareId = expired.Software.Id, licenseType = expired.LicenseType, totalQuantity = 1, expiresAt = DateTime.UtcNow.AddDays(-1), rowVersion = expired.RowVersion }); Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        using var backdate = await admin.PostAsJsonAsync(Path, new { softwareLicenseId = expired.Id, userId = reader.Id, assignedAt = DateTime.UtcNow.AddDays(-2) });
        Assert.Equal(HttpStatusCode.Conflict, backdate.StatusCode); Assert.Contains("LICENSE_EXPIRED", await backdate.Content.ReadAsStringAsync());
    }
    private sealed class FailingAudit(AppDbContext db, IActor actor) : IAuditWriter {
        private int calls;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null) {
            new AuditWriter(db, actor).Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
            if (action.StartsWith("license-assignments.") && ++calls == 2) throw new InvalidOperationException("Synthetic transfer audit failure");
        }
    }
    [LocalPostgresFact]
    public async Task Transfer_audit_failure_rolls_back_close_insert_capacity_and_audits()
    {
        await using var baseline = Factory(); await LocalAssignmentCompletionTests.SeedAsync(baseline); using var admin = await LocalAssignmentCompletionTests.LoginAsync(baseline);
        var license = await LicenseAsync(admin); var refs = await LocalAssignmentCompletionTests.References(baseline); var first = await AssignAsync(admin, license.Id, refs.Admin);
        var target = await LocalAssignmentCompletionTests.ActorAsync(baseline, "SYSTEM_MANAGER");
        await using var failing = baseline.WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<IAuditWriter>(); services.AddScoped<IAuditWriter, FailingAudit>(); }));
        using var writer = await LocalAssignmentCompletionTests.LoginAsync(failing);
        using var transfer = await writer.PostAsJsonAsync(Path + $"/{first.Id}/transfer", new TransferLicenseAssignmentRequest { UserId = target.Id, RowVersion = first.RowVersion }); Assert.Equal(HttpStatusCode.InternalServerError, transfer.StatusCode);
        var actual = (await admin.GetFromJsonAsync<LicenseAssignmentDto>(Path + "/" + first.Id))!; Assert.Null(actual.RevokedAtUtc); Assert.Equal(first.RowVersion, actual.RowVersion);
        var items = (await admin.GetFromJsonAsync<PagedResponse<LicenseAssignmentDto>>(Path + "?licenseId=" + license.Id))!.Items; Assert.Single(items);
        await using var scope = baseline.Services.CreateAsyncScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AuditLog>().CountAsync(x => x.EntityType == nameof(LicenseAssignment) && x.EntityId == first.Id.ToString()));
    }
}
