using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

// Proposed persistence protocol only. This does not implement or certify the
// owner's allocation API, permissions, type/effective-date or target validation.
[Collection("isolated-neon")]
public sealed class LicenseCapacityProtocolTests(MvpFixture fixture)
{
    private sealed class Actor(long id) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/isolated-fixture/license-capacity";
        public bool Has(string permission) => false;
    }
    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
        db.Database.SetCommandTimeout(15);
    }
    private async Task<(long License, long User, long Asset)> PrepareAsync()
    {
        using var client = await fixture.ClientAsync();
        var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        using var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        var asset = (await response.Content.ReadFromJsonAsync<AssetDto>())!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var actor = new Actor(user.Id); var writer = new AuditWriter(db, actor);
        var code = "CAP" + Guid.NewGuid().ToString("N");
        var software = new Software { Code = code, Name = "Synthetic capacity fixture", CreatedAtUtc = DateTime.UtcNow };
        var license = new SoftwareLicense { LicenseCode = code, LicenseType = "VOLUME", TotalQuantity = 1, CreatedAtUtc = DateTime.UtcNow };
        await new AuditedUnitOfWork(db).RunAsync(async () =>
        {
            db.Add(software); writer.Record("fixture.capacity", software, actor.UserId, "USER"); await db.SaveChangesAsync();
            license.SoftwareId = software.Id; db.Add(license); writer.Record("fixture.capacity", license, actor.UserId, "USER");
            await db.SaveChangesAsync(); return true;
        });
        return (license.Id, user.Id, asset.Id);
    }
    private static IQueryable<LicenseAssignment> Active(AppDbContext db, long license) =>
        db.Set<LicenseAssignment>().Where(a => a.SoftwareLicenseId == license && a.RevokedAtUtc == null && !a.IsArchived);
    private static async Task<LicenseAssignment> InsertAsync(AppDbContext db, long licenseId, Actor actor,
        long? user, long? asset, CancellationToken ct)
    {
        var license = await db.Set<SoftwareLicense>().AsNoTracking().SingleAsync(l => l.Id == licenseId, ct);
        if (await Active(db, licenseId).CountAsync(ct) >= license.TotalQuantity)
            throw new BusinessException(409, "FIXTURE_LICENSE_FULL", "Fixture capacity exhausted.");
        var row = new LicenseAssignment { SoftwareLicenseId = licenseId, AssignedUserId = user, AssignedAssetId = asset,
            AssignedByUserId = actor.UserId!.Value, AssignedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow };
        db.Add(row); new AuditWriter(db, actor).Record("fixture.capacity.assign", row, actor.UserId, "USER");
        await db.SaveChangesAsync(ct); return row;
    }

    [NeonFact]
    public async Task Two_connections_contend_for_last_seat_and_reread_after_lock_prevents_mixed_target_overallocation()
    {
        var ids = await PrepareAsync(); var actor = new Actor(ids.User);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)); var ct = timeout.Token;
        await using var firstScope = fixture.Factory.Services.CreateAsyncScope();
        await using var secondScope = fixture.Factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(firstDb);
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(secondDb);
        Assert.NotSame(firstDb.Database.GetDbConnection(), secondDb.Database.GetDbConnection());
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new AuditedUnitOfWork(firstDb).RunAsync(async () =>
        {
            await firstDb.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.software_licenses WHERE id={ids.License} FOR UPDATE", ct);
            await InsertAsync(firstDb, ids.License, actor, ids.User, null, ct);
            saved.TrySetResult(); await release.Task.WaitAsync(ct); return true;
        }, ct);
        try
        {
            await saved.Task.WaitAsync(ct);
            // NOWAIT proves actual lock contention while the first transaction is
            // held, rather than inferring a race from Task.Delay or task timing.
            var conflict = await Assert.ThrowsAsync<PostgresException>(() => new AuditedUnitOfWork(secondDb).RunAsync(async () =>
            {
                await secondDb.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.software_licenses WHERE id={ids.License} FOR UPDATE NOWAIT", ct);
                return true;
            }, ct));
            Assert.Equal("55P03", conflict.SqlState);
        }
        finally { release.TrySetResult(); await first; }
        var full = await Assert.ThrowsAsync<BusinessException>(() => new AuditedUnitOfWork(secondDb).RunAsync(async () =>
        {
            await secondDb.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.software_licenses WHERE id={ids.License} FOR UPDATE", ct);
            return await InsertAsync(secondDb, ids.License, actor, null, ids.Asset, ct);
        }, ct));
        Assert.Equal(409, full.Status); Assert.Equal("FIXTURE_LICENSE_FULL", full.Code);
        Assert.Equal(1, await Active(secondDb, ids.License).CountAsync(ct));
        Assert.False(await Active(secondDb, ids.License).AnyAsync(a => a.AssignedAssetId == ids.Asset, ct));
        Assert.Equal(1, await secondDb.Set<AuditLog>().CountAsync(a => a.CorrelationId == actor.CorrelationId && a.Action == "fixture.capacity.assign", ct));
    }

    [NeonFact]
    public async Task Transfer_at_full_capacity_rolls_back_close_insert_and_audits_then_commits_one_seat_with_history()
    {
        var ids = await PrepareAsync(); var actor = new Actor(ids.User);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var initial = await new AuditedUnitOfWork(db).RunAsync(() => InsertAsync(db, ids.License, actor, ids.User, null, default));
        var baselineAudits = await db.Set<AuditLog>().CountAsync(a => a.CorrelationId == actor.CorrelationId);
        async Task<long> Transfer(bool abort)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.software_licenses WHERE id={ids.License} FOR UPDATE");
            var old = await Active(db, ids.License).SingleAsync();
            old.RevokedAtUtc = DateTime.UtcNow; old.RevokedByUserId = ids.User;
            new AuditWriter(db, actor).Record("fixture.capacity.revoke", old, actor.UserId, "USER"); await db.SaveChangesAsync();
            var next = await InsertAsync(db, ids.License, actor, null, ids.Asset, default);
            if (abort) throw new InvalidOperationException("fixture transfer abort after save");
            return next.Id;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AuditedUnitOfWork(db).RunAsync(() => Transfer(true)));
        Assert.Equal(initial.Id, (await Active(db, ids.License).SingleAsync()).Id);
        Assert.Equal(1, await db.Set<LicenseAssignment>().CountAsync(a => a.SoftwareLicenseId == ids.License));
        Assert.Equal(baselineAudits, await db.Set<AuditLog>().CountAsync(a => a.CorrelationId == actor.CorrelationId));
        var nextId = await new AuditedUnitOfWork(db).RunAsync(() => Transfer(false));
        Assert.Equal(nextId, (await Active(db, ids.License).SingleAsync()).Id);
        Assert.Equal(2, await db.Set<LicenseAssignment>().CountAsync(a => a.SoftwareLicenseId == ids.License));
        Assert.Equal(baselineAudits + 2, await db.Set<AuditLog>().CountAsync(a => a.CorrelationId == actor.CorrelationId));
    }
}
