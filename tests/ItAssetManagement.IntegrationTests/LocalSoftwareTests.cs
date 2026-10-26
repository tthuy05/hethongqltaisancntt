using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalSoftwareTests
{
    private const string Path = "/api/v1/software";
    [LocalPostgresFact]
    public async Task Competing_updates_have_one_winner_and_revocation_during_parent_lock_denies_write()
    {
        await using var app = LocalAssignmentCompletionTests.Factory(software: true); await LocalAssignmentCompletionTests.SeedAsync(app);
        using var admin = await LocalAssignmentCompletionTests.LoginAsync(app);
        using var created = await admin.PostAsJsonAsync(Path, new { name = "Concurrent " + Guid.NewGuid() }); var item = (await created.Content.ReadFromJsonAsync<SoftwareDto>())!;
        var writes = await Task.WhenAll(Enumerable.Range(1, 2).Select(n => admin.PutAsJsonAsync(Path + "/" + item.Id, new SoftwareRequest { Name = "Winner " + n, RowVersion = item.RowVersion })));
        try { Assert.Single(writes, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(writes, x => x.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in writes) response.Dispose(); }
        var fresh = (await admin.GetFromJsonAsync<SoftwareDto>(Path + "/" + item.Id))!;
        var actor = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER", SoftwarePermissions.Read, SoftwarePermissions.Update);
        using var writer = await LocalAssignmentCompletionTests.LoginAsync(app, actor.Email);
        await using var blocker = LocalSequenceAuthenticationTests.Database(); await using var tx = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"software:" + item.Id}, 0))");
        var pending = writer.PutAsJsonAsync(Path + "/" + item.Id, new SoftwareRequest { Name = "Denied after wait", RowVersion = fresh.RowVersion });
        try {
            await using var observer = LocalSequenceAuthenticationTests.Database(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await observer.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted) AS \"Value\"").SingleAsync(deadline.Token)) await Task.Delay(20, deadline.Token);
            await using var scope = app.Services.CreateAsyncScope(); var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>();
            await services.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
                await services.GetRequiredService<IRepository>().LockAsync("users-identity", default);
                var permission = await db.Set<Permission>().SingleAsync(x => x.Code == SoftwarePermissions.Update);
                var grant = await db.Set<UserPermission>().SingleAsync(x => x.UserId == actor.Id && x.PermissionId == permission.Id);
                services.GetRequiredService<IAccountPersistence>().RemoveUserPermission(grant); services.GetRequiredService<IAuditWriter>().Record("test.revoke", grant, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
            });
        } finally { await tx.RollbackAsync(); }
        using var denied = await pending; Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(fresh.Name, (await admin.GetFromJsonAsync<SoftwareDto>(Path + "/" + item.Id))!.Name);
        Assert.False(blocker.Database.HasPendingModelChanges());
    }
    [LocalPostgresFact]
    public async Task Real_JWT_CRUD_generated_code_filters_concurrency_and_immutable_mapping()
    {
        await using var app = LocalAssignmentCompletionTests.Factory(software: true); await LocalAssignmentCompletionTests.SeedAsync(app);
        using var client = await LocalAssignmentCompletionTests.LoginAsync(app);
        var name = "Software " + Guid.NewGuid();
        using var created = await client.PostAsJsonAsync(Path, new SoftwareRequest { Name = name, Publisher = "ACME", Version = "1.2", Description = "Isolated catalog" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var item = (await created.Content.ReadFromJsonAsync<SoftwareDto>())!;
        Assert.Matches("^PM[0-9]{6,}$", item.Code);
        using var injected = await client.PostAsJsonAsync(Path, new { name = "Injected", code = "PM999999" }); Assert.Equal(HttpStatusCode.BadRequest, injected.StatusCode);
        using var detail = await client.GetAsync(Path + "/" + item.Id); Assert.Equal('"' + item.RowVersion + '"', detail.Headers.ETag!.ToString());
        var results = await client.GetFromJsonAsync<PagedResponse<SoftwareDto>>(Path + "?vendor=acme&keyword=" + Uri.EscapeDataString(name.ToLowerInvariant()) + "&status=Active&sortBy=code&sortDirection=desc");
        Assert.Equal(item.Id, Assert.Single(results!.Items).Id);
        using var changed = await client.PutAsJsonAsync(Path + "/" + item.Id, new SoftwareRequest { Name = name + " updated", RowVersion = item.RowVersion });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode); var updated = (await changed.Content.ReadFromJsonAsync<SoftwareDto>())!;
        Assert.Equal(item.Code, updated.Code); Assert.NotEqual(item.RowVersion, updated.RowVersion);
        using var stale = await client.PutAsJsonAsync(Path + "/" + item.Id, new SoftwareRequest { Name = "Stale", RowVersion = item.RowVersion }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var missingVersion = await client.DeleteAsync(Path + "/" + item.Id); Assert.Equal((HttpStatusCode)428, missingVersion.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Set<Software>().SingleAsync(x => x.Id == item.Id); entity.Code = "PM999999";
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async () => { await db.SaveChangesAsync(); return true; })); db.ChangeTracker.Clear();
        using var delete = new HttpRequestMessage(HttpMethod.Delete, Path + "/" + item.Id); delete.Headers.TryAddWithoutValidation("If-Match", '"' + updated.RowVersion + '"');
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<SoftwareDto>(Path + "/" + item.Id))!.IsActive);
        Assert.Equal(1, await db.Set<AuditLog>().CountAsync(x => x.EntityId == item.Id.ToString() && x.Action == "software.archive"));
    }
    [LocalPostgresFact]
    public async Task Software_seed_is_additive_idempotent_and_excludes_support_custom_grants_are_respected()
    {
        await using var app = LocalAssignmentCompletionTests.Factory(software: true); await LocalAssignmentCompletionTests.SeedAsync(app);
        await using (var scope = app.Services.CreateAsyncScope()) {
            var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await services.GetRequiredService<DevelopmentSeed>().PrepareSoftwareCatalogAsync());
            Assert.Equal(0, await services.GetRequiredService<DevelopmentSeed>().PrepareSoftwareCatalogAsync());
            Assert.Equal(4, await db.Set<Permission>().CountAsync(x => SoftwarePermissions.All.Contains(x.Code)));
            var grants = from rp in db.Set<RolePermission>() join role in db.Set<Role>() on rp.RoleId equals role.Id join permission in db.Set<Permission>() on rp.PermissionId equals permission.Id where SoftwarePermissions.All.Contains(permission.Code) select role.Code;
            Assert.Equal(8, await grants.CountAsync()); Assert.DoesNotContain("TECHNICAL_SUPPORT", await grants.ToListAsync());
        }
        var reader = await LocalAssignmentCompletionTests.ActorAsync(app, "SYSTEM_MANAGER", SoftwarePermissions.Read);
        using var client = await LocalAssignmentCompletionTests.LoginAsync(app, reader.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Path, new { name = "Denied" })).StatusCode);
        var support = await LocalAssignmentCompletionTests.ActorAsync(app, "TECHNICAL_SUPPORT");
        using var restricted = await LocalAssignmentCompletionTests.LoginAsync(app, support.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await restricted.GetAsync(Path)).StatusCode);
    }
    [LocalPostgresFact]
    public async Task Active_license_prevents_archive_and_business_data_survives()
    {
        await using var app = LocalAssignmentCompletionTests.Factory(software: true); await LocalAssignmentCompletionTests.SeedAsync(app);
        using var client = await LocalAssignmentCompletionTests.LoginAsync(app);
        using var created = await client.PostAsJsonAsync(Path, new { name = "Licensed " + Guid.NewGuid() }); var item = (await created.Content.ReadFromJsonAsync<SoftwareDto>())!;
        await using var scope = app.Services.CreateAsyncScope(); var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>();
        await services.GetRequiredService<IUnitOfWork>().RunAsync(async () => {
            await services.GetRequiredService<IRepository>().LockAsync("software:" + item.Id, default);
            var license = new SoftwareLicense { SoftwareId = item.Id, LicenseCode = await services.GetRequiredService<ILicenseCodeGenerator>().NextAsync(default), LicenseType = "PER_USER", TotalQuantity = 1, CreatedAtUtc = DateTime.UtcNow };
            db.Add(license); await db.SaveChangesAsync(); services.GetRequiredService<IAuditWriter>().Record("test.fixture", license, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
        });
        using var delete = new HttpRequestMessage(HttpMethod.Delete, Path + "/" + item.Id); delete.Headers.TryAddWithoutValidation("If-Match", '"' + item.RowVersion + '"');
        using var response = await client.SendAsync(delete); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("SOFTWARE_ACTIVE_LICENSES", await response.Content.ReadAsStringAsync());
        Assert.True((await client.GetFromJsonAsync<SoftwareDto>(Path + "/" + item.Id))!.IsActive);
        Assert.True(await db.Set<SoftwareLicense>().AnyAsync(x => x.SoftwareId == item.Id && x.IsActive));
    }
    private sealed class FailingAudit(AppDbContext db, IActor actor) : IAuditWriter
    {
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null) {
            if (action.StartsWith("software.")) throw new InvalidOperationException("Synthetic software audit failure");
            new AuditWriter(db, actor).Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
        }
    }
    [LocalPostgresFact]
    public async Task Audit_failure_rolls_back_software_creation()
    {
        await using var baseline = LocalAssignmentCompletionTests.Factory(software: true); await LocalAssignmentCompletionTests.SeedAsync(baseline);
        await using var failing = baseline.WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<IAuditWriter>(); services.AddScoped<IAuditWriter, FailingAudit>(); }));
        using var client = await LocalAssignmentCompletionTests.LoginAsync(failing);
        var name = "Audit rollback " + Guid.NewGuid(); using var response = await client.PostAsJsonAsync(Path, new { name });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = baseline.Services.CreateScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Software>().AnyAsync(x => x.Name == name));
    }
}
