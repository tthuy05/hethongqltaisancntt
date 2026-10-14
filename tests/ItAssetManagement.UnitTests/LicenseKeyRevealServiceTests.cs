using System.Text.Json;
using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseKeyRevealServiceTests
{
    private const string Secret = "SYNTHETIC-REVEAL-ONLY-ABCD";
    private sealed class Actor(bool allowed = true) : IActor
    {
        public long? UserId => 7; public int? TokenVersion => 3;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST"; public string? Path => "/fixture/reveal";
        public bool Has(string permission) => allowed && permission is Permissions.LicenseRead or Permissions.LicenseKeyReveal;
    }
    private sealed class Protector : ILicenseKeyProtector
    {
        public int Calls;
        public LicenseKeyEnvelope Protect(long licenseId, string plaintext) => throw new NotSupportedException();
        public string Reveal(long licenseId, LicenseKeyEnvelope envelope) { Calls++; return Secret; }
    }
    private sealed class Repository : IRepository
    {
        public readonly User User = new() { Id = 7, IsActive = true, IsEmailConfirmed = true, TokenVersion = 3 };
        public readonly Role Role = new() { Id = 1, Code = "ADMIN_IT", IsActive = true };
        public readonly List<Permission> PermissionsList = [new() { Id = 1, Code = Permissions.LicenseRead, IsActive = true },
            new() { Id = 2, Code = Permissions.LicenseKeyReveal, IsActive = true }];
        public readonly SoftwareLicense License = new() { Id = 4, LicenseKeyCiphertext = [1], LicenseKeyLast4 = "ABCD", KeyVersion = "v1" };
        public bool MissingLicense, FailSave;
        public readonly List<string> Operations = [];
        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            Operations.Add("query:" + typeof(T).Name);
            IEnumerable<T> rows = typeof(T) == typeof(User) ? new[] { User }.Cast<T>() :
                typeof(T) == typeof(Role) ? new[] { Role }.Cast<T>() :
                typeof(T) == typeof(UserRole) ? new[] { new UserRole { UserId = 7, RoleId = 1 } }.Cast<T>() :
                typeof(T) == typeof(Permission) ? PermissionsList.Cast<T>() :
                typeof(T) == typeof(RolePermission) ? new[] { new RolePermission { RoleId = 1, PermissionId = 1 },
                    new RolePermission { RoleId = 1, PermissionId = 2 } }.Cast<T>() :
                typeof(T) == typeof(SoftwareLicense) ? (MissingLicense ? [] : new[] { License }).Cast<T>() : throw new NotSupportedException();
            return rows.AsQueryable();
        }
        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public Task LockAsync(string resource, CancellationToken ct) { Operations.Add("lock:" + resource); return Task.CompletedTask; }
        public Task SaveAsync(CancellationToken ct) { Operations.Add("save"); return FailSave ? Task.FromException(new InvalidOperationException("fixture save failure")) : Task.CompletedTask; }
        public void Add<T>(T entity) where T : class => throw new NotSupportedException();
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }
    private sealed class UnitOfWork(bool failCommit = false) : IUnitOfWork
    {
        public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
        { var result = await work(); if (failCommit) throw new InvalidOperationException("fixture commit failure"); return result; }
    }
    private sealed class Writer : IAuditWriter
    {
        public object? Snapshot; public string? Action; public bool Fail;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null)
        { if (Fail) throw new InvalidOperationException("fixture audit failure"); Action = action; Snapshot = after; }
    }
    [Fact]
    public async Task Missing_claim_permission_is_denied_before_repository_or_crypto_access()
    {
        var protector = new Protector();
        var service = new LicenseKeyRevealService(null!, null!, null!, new Actor(false), protector);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.RevealAsync(4, "reason", default))).Status);
        Assert.Equal(0, protector.Calls);
    }
    [Theory]
    [InlineData("manager")][InlineData("inactive-role")][InlineData("inactive-user")][InlineData("unconfirmed")]
    [InlineData("admin-locked")][InlineData("lockout")][InlineData("stale-token")][InlineData("revoked-grant")]
    public async Task Database_checks_under_identity_lock_reject_stale_or_misgranted_claims(string mode)
    {
        var repo = new Repository(); var protector = new Protector(); var audit = new Writer();
        switch (mode)
        {
            case "manager": repo.Role.Code = "SYSTEM_MANAGER"; break;
            case "inactive-role": repo.Role.IsActive = false; break;
            case "inactive-user": repo.User.IsActive = false; break;
            case "unconfirmed": repo.User.IsEmailConfirmed = false; break;
            case "admin-locked": repo.User.IsAdminLocked = true; break;
            case "lockout": repo.User.LockoutEndUtc = DateTime.UtcNow.AddMinutes(10); break;
            case "stale-token": repo.User.TokenVersion = 4; break;
            case "revoked-grant": repo.PermissionsList[1].IsActive = false; break;
        }
        var service = new LicenseKeyRevealService(repo, new UnitOfWork(), audit, new Actor(), protector);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.RevealAsync(4, "reason", default))).Status);
        Assert.Equal("lock:users-identity", repo.Operations[0]); Assert.DoesNotContain("query:SoftwareLicense", repo.Operations);
        Assert.Equal(0, protector.Calls); Assert.Null(audit.Snapshot);
    }
    [Fact]
    public async Task Successful_reveal_audits_only_metadata_and_never_prints_the_key()
    {
        var repo = new Repository(); var protector = new Protector(); var audit = new Writer();
        var response = await new LicenseKeyRevealService(repo, new UnitOfWork(), audit, new Actor(), protector).RevealAsync(4, "private access reason", default);
        Assert.Equal(Secret, response.LicenseKey); Assert.Equal(4, response.LicenseId); Assert.Equal(DateTimeKind.Utc, response.RevealedAt.Kind);
        Assert.Equal(1, protector.Calls); Assert.Equal("license.key.reveal", audit.Action); Assert.Equal("save", repo.Operations[^1]);
        var json = JsonSerializer.Serialize(audit.Snapshot); Assert.DoesNotContain(Secret, json); Assert.DoesNotContain("private access reason", json);
        var safe = AuditSnapshot.Read(json, false); Assert.False(safe.Redacted); Assert.True(safe.Values!["keyRevealed"].GetBoolean());
        Assert.DoesNotContain(Secret, response.ToString());
    }
    [Theory]
    [InlineData("audit")][InlineData("save")][InlineData("commit")]
    public async Task Audit_save_or_commit_failure_does_not_return_a_plaintext_response(string mode)
    {
        var repo = new Repository { FailSave = mode == "save" }; var writer = new Writer { Fail = mode == "audit" };
        var service = new LicenseKeyRevealService(repo, new UnitOfWork(mode == "commit"), writer, new Actor(), new Protector());
        LicenseKeyRevealResponse? response = null;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => response = await service.RevealAsync(4, "reason", default));
        Assert.Null(response);
    }
    [Theory]
    [InlineData("no-license", 404)][InlineData("no-key", 404)][InlineData("partial-tuple", 500)][InlineData("no-reason", 400)]
    public async Task Missing_data_or_reason_does_not_call_decryption(string mode, int status)
    {
        var repo = new Repository(); var protector = new Protector();
        if (mode == "no-license") repo.MissingLicense = true;
        if (mode == "no-key") { repo.License.LicenseKeyCiphertext = null; repo.License.LicenseKeyLast4 = null; repo.License.KeyVersion = null; }
        if (mode == "partial-tuple") repo.License.KeyVersion = null;
        var service = new LicenseKeyRevealService(repo, new UnitOfWork(), new Writer(), new Actor(), protector);
        Assert.Equal(status, (await Assert.ThrowsAsync<BusinessException>(() => service.RevealAsync(4, mode == "no-reason" ? " " : "reason", default))).Status);
        Assert.Equal(0, protector.Calls);
    }
}
