using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using ItAssetManagement.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LicenseKeyRevealPersistenceTests(MvpFixture fixture)
{
    private sealed class Actor(CurrentUser user) : IActor
    {
        public long? UserId => user.Id;
        public int? TokenVersion { get; set; }
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/isolated-fixture/key-reveal";
        public bool Has(string permission) => permission is Permissions.LicenseRead or Permissions.LicenseKeyReveal;
    }
    private sealed class AbortAfterWork(IUnitOfWork inner, Func<Task> prepare, Func<Task> verifySaved) : IUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => inner.RunAsync<T>(async () =>
        {
            await prepare(); await work(); await verifySaved();
            // Fault after SaveChanges, before commit. No response escapes the UoW.
            throw new InvalidOperationException("isolated abort after audit save");
        }, ct);
    }

    [NeonFact]
    public async Task Real_transaction_rolls_back_saved_reveal_audit_and_temporary_grants_before_returning_key()
    {
        using var client = await fixture.ClientAsync();
        var current = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
        var actor = new Actor(current) { TokenVersion = await db.Set<User>().Where(u => u.Id == current.Id).Select(u => u.TokenVersion).SingleAsync() };
        var repo = new EfRepository(db); var audit = new AuditWriter(db, actor);
        using var ring = new ConfiguredLicenseKeyRing("fixture", new Dictionary<string, byte[]> { ["fixture"] = new byte[32] });
        var protector = new AesGcmLicenseKeyProtector(ring);
        var code = "REVEAL" + Guid.NewGuid().ToString("N");
        var license = new SoftwareLicense { Id = Random.Shared.NextInt64(1_000_000_000, 2_000_000_000), LicenseCode = code, LicenseType = "PER_USER", TotalQuantity = 1, CreatedAtUtc = DateTime.UtcNow };
        var uow = new AbortAfterWork(new AuditedUnitOfWork(db), async () =>
        {
            await repo.LockAsync("users-identity", default);
            Assert.False(await db.Set<Permission>().AnyAsync(p => p.Code == Permissions.LicenseRead || p.Code == Permissions.LicenseKeyReveal));
            var adminRoleId = await db.Set<Role>().Where(r => r.Code == "ADMIN_IT").Select(r => r.Id).SingleAsync();
            foreach (var permissionCode in new[] { Permissions.LicenseRead, Permissions.LicenseKeyReveal })
            {
                var permission = new Permission { Code = permissionCode, Name = "Temporary isolated grant", Module = "license", IsActive = true };
                repo.Add(permission); audit.Record("fixture.reveal", permission, actor.UserId, "USER"); await repo.SaveAsync(default);
                var grant = new RolePermission { RoleId = adminRoleId, PermissionId = permission.Id };
                repo.Add(grant); audit.Record("fixture.reveal", grant, actor.UserId, "USER");
            }
            var software = new Software { Code = code, Name = "Synthetic reveal fixture", CreatedAtUtc = DateTime.UtcNow };
            repo.Add(software); audit.Record("fixture.reveal", software, actor.UserId, "USER"); await repo.SaveAsync(default);
            license.SoftwareId = software.Id; repo.Add(license); audit.Record("fixture.reveal", license, actor.UserId, "USER"); await repo.SaveAsync(default);
            var envelope = protector.Protect(license.Id, "SYNTHETIC-ISOLATED-ABCD");
            license.LicenseKeyCiphertext = envelope.Ciphertext; license.LicenseKeyLast4 = envelope.Last4; license.KeyVersion = envelope.KeyVersion;
            audit.Record("fixture.reveal", license, actor.UserId, "USER"); await repo.SaveAsync(default);
        }, async () =>
        {
            var saved = await db.Set<AuditLog>().Where(a => a.CorrelationId == actor.CorrelationId && a.Action == "license.key.reveal").SingleAsync();
            Assert.DoesNotContain("SYNTHETIC-ISOLATED", saved.NewValuesJson ?? "");
            var snapshot = AuditSnapshot.Read(saved.NewValuesJson, false);
            Assert.False(snapshot.Redacted); Assert.True(snapshot.Values!["keyRevealed"].GetBoolean());
            Assert.Equal(license.Id, snapshot.Values["licenseId"].GetInt64());
        });
        var service = new LicenseKeyRevealService(repo, uow, audit, actor, protector);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevealAsync(license.Id, "Synthetic verification", default));
        Assert.Equal("isolated abort after audit save", failure.Message);
        Assert.False(await db.Set<AuditLog>().AnyAsync(a => a.CorrelationId == actor.CorrelationId));
        Assert.False(await db.Set<SoftwareLicense>().AnyAsync(l => l.Id == license.Id));
        Assert.False(await db.Set<Software>().AnyAsync(s => s.Code == code));
        Assert.False(await db.Set<Permission>().AnyAsync(p => p.Code == Permissions.LicenseRead || p.Code == Permissions.LicenseKeyReveal));
    }
}
