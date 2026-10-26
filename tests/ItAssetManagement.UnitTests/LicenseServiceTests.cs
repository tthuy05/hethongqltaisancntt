using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseServiceTests
{
    private sealed class Repo : IRepository
    {
        public List<SoftwareLicense> Licenses = [];
        public List<Software> SoftwareItems = [];
        public List<LicenseAssignment> Assignments = [];
        public List<User> Users = [];
        public List<Role> Roles = [];
        public List<UserRole> UserRoles = [];
        public List<Permission> PermissionsList = [];
        public List<RolePermission> RolePermissions = [];
        public List<UserPermission> UserPermissions = [];
        public List<string> Operations = [];

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            if (typeof(T) == typeof(SoftwareLicense)) return Licenses.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(Software)) return SoftwareItems.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(LicenseAssignment)) return Assignments.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(User)) return Users.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(Role)) return Roles.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(UserRole)) return UserRoles.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(Permission)) return PermissionsList.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(RolePermission)) return RolePermissions.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(UserPermission)) return UserPermissions.Cast<T>().AsQueryable();
            throw new NotSupportedException(typeof(T).Name);
        }

        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());

        public void Add<T>(T entity) where T : class
        {
            if (entity is SoftwareLicense lic)
            {
                lic.Id = Licenses.Count + 1;
                lic.RowVersion = new byte[16];
                Licenses.Add(lic);
            }
        }

        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) { Operations.Add("save"); return Task.CompletedTask; }
        public Task LockAsync(string resource, CancellationToken ct) { Operations.Add("lock:" + resource); return Task.CompletedTask; }
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) => throw new NotSupportedException();
    }

    private sealed class Persistence : ILicensePersistence
    {
        public List<long> Locked = [];
        public Task LockAsync(long id, CancellationToken ct) { Locked.Add(id); return Task.CompletedTask; }
    }

    private sealed class Uow : IUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work();
    }

    private sealed class Actor(long? id = 7, params string[] grants) : IActor
    {
        public long? UserId => id;
        public int? TokenVersion => 1;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/api/v1/software-licenses";
        public bool Has(string code) => grants.Contains(code);
    }

    private sealed class Audit : IAuditWriter
    {
        public List<string> Actions = [];
        public List<object?> Snapshots = [];
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null)
        {
            Actions.Add(action);
            Snapshots.Add(after);
        }
    }

    private sealed class Codes : ILicenseCodeGenerator
    {
        private int _counter;
        public Task<string> NextAsync(CancellationToken ct) => Task.FromResult($"BQ{++_counter:D6}");
    }

    private sealed class Protector : ILicenseKeyProtector
    {
        public LicenseKeyEnvelope Protect(long licenseId, string plaintext) =>
            new([1, 2, 3], plaintext[^4..], "v1");
        public string Reveal(long licenseId, LicenseKeyEnvelope envelope) => "REAL-KEY-" + envelope.Last4;
    }

    private static (LicenseService Service, Repo Repo, Persistence Persistence, Audit Audit) Setup(Actor actor)
    {
        var repo = new Repo();
        // Seed default Admin IT user and role for AuthorizeAsync
        repo.Users.Add(new User { Id = 7, IsActive = true, IsEmailConfirmed = true, TokenVersion = 1 });
        repo.Roles.Add(new Role { Id = 1, Code = "ADMIN_IT", IsActive = true });
        repo.UserRoles.Add(new UserRole { UserId = 7, RoleId = 1 });

        foreach (var p in LicensePermissions.All.Concat([Permissions.AssetCost, Permissions.ReportCost]))
        {
            var perm = new Permission { Id = repo.PermissionsList.Count + 1, Code = p, IsActive = true };
            repo.PermissionsList.Add(perm);
            repo.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = perm.Id });
        }

        // Active software item
        repo.SoftwareItems.Add(new Software { Id = 2, Code = "PM000002", Name = "SQL Server", IsActive = true, RowVersion = new byte[16] });

        var persistence = new Persistence();
        var audit = new Audit();
        var protector = new Protector();
        var userGrants = LicensePermissions.All.Concat([Permissions.AssetCost, Permissions.ReportCost]).Where(p => actor.Has(p)).ToArray();
        var currentUser = new CurrentUser(actor.UserId ?? 7, "Admin IT", "admin@example.com", null,
            actor.UserId.HasValue && actor.UserId.Value == 7 ? ["ADMIN_IT"] : ["TECHNICAL_SUPPORT"],
            userGrants);
        var keys = new LicenseKeyAccessPolicy(currentUser);
        var service = new LicenseService(repo, persistence, new Uow(), actor, audit, new Codes(), keys, protector, TimeProvider.System);
        return (service, repo, persistence, audit);
    }

    private static Actor FullAdmin() => new(7,
        Permissions.LicenseRead, LicensePermissions.Create, LicensePermissions.Update,
        LicensePermissions.Archive, Permissions.LicenseKeyManage, Permissions.LicenseKeyReveal,
        Permissions.AssetCost);

    [Fact]
    public async Task Create_validates_required_fields()
    {
        var (service, _, _, _) = Setup(FullAdmin());

        // SoftwareId <= 0
        await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(null, new() { SoftwareId = 0, LicenseType = "VOLUME", TotalQuantity = 1 }, default));

        // Invalid LicenseType
        await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(null, new() { SoftwareId = 2, LicenseType = "INVALID", TotalQuantity = 1 }, default));

        // TotalQuantity <= 0
        await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(null, new() { SoftwareId = 2, LicenseType = "VOLUME", TotalQuantity = 0 }, default));

        // Non-UTC dates
        var localDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(null, new() { SoftwareId = 2, LicenseType = "VOLUME", TotalQuantity = 1, StartsAt = localDate }, default));

        // StartsAt >= ExpiresAt
        var start = DateTime.SpecifyKind(new DateTime(2026, 6, 1), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(new DateTime(2026, 5, 1), DateTimeKind.Utc);
        await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(null, new() { SoftwareId = 2, LicenseType = "VOLUME", TotalQuantity = 1, StartsAt = start, ExpiresAt = end }, default));

        // Negative cost
        await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(null, new() { SoftwareId = 2, LicenseType = "VOLUME", TotalQuantity = 1, PurchaseCost = -500 }, default));
    }

    [Fact]
    public async Task Create_generates_code_encrypts_key_and_records_audit()
    {
        var (service, repo, _, audit) = Setup(FullAdmin());

        var request = new LicenseRequest
        {
            SoftwareId = 2,
            LicenseType = "VOLUME",
            TotalQuantity = 20,
            Vendor = "Microsoft Corp",
            PurchaseCost = 15000000,
            LicenseKey = "ABCDE-12345-WXYZ"
        };

        var dto = await service.WriteAsync(null, request, default);

        Assert.Equal("BQ000001", dto.LicenseCode);
        Assert.Equal("SQL Server", dto.Software.Name);
        Assert.Equal(20, dto.TotalQuantity);
        Assert.Equal(0, dto.UsedQuantity);
        Assert.Equal(20, dto.AvailableQuantity);
        Assert.Equal("****-WXYZ", dto.MaskedKey);
        Assert.Equal(15000000, dto.PurchaseCost);

        // Repo entity assertions
        var created = repo.Licenses.Single();
        Assert.Equal("BQ000001", created.LicenseCode);
        Assert.Equal("WXYZ", created.LicenseKeyLast4);
        Assert.NotNull(created.LicenseKeyCiphertext);

        // Audit assertion: no plaintext key or ciphertext leaked in audit log
        Assert.Contains("licenses.create", audit.Actions);
        var auditText = System.Text.Json.JsonSerializer.Serialize(audit.Snapshots[0]);
        Assert.DoesNotContain("ABCDE-12345-WXYZ", auditText);
        Assert.DoesNotContain("Ciphertext", auditText);
    }

    [Fact]
    public async Task Update_checks_row_version_concurrency()
    {
        var (service, repo, _, _) = Setup(FullAdmin());

        var rowVersion = new byte[16];
        rowVersion[0] = 1;
        var existing = new SoftwareLicense
        {
            Id = 4,
            SoftwareId = 2,
            LicenseCode = "BQ000004",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            IsActive = true,
            RowVersion = rowVersion
        };
        repo.Licenses.Add(existing);

        var staleVersion = Convert.ToBase64String(new byte[16]); // all zeroes
        var request = new LicenseRequest
        {
            SoftwareId = 2,
            LicenseType = "VOLUME",
            TotalQuantity = 15,
            RowVersion = staleVersion
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(4, request, default));
        Assert.Equal(409, ex.Status);
        Assert.Equal("CONCURRENCY_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task Update_software_is_immutable()
    {
        var (service, repo, _, _) = Setup(FullAdmin());

        var rowVersion = new byte[16];
        var existing = new SoftwareLicense
        {
            Id = 4,
            SoftwareId = 2,
            LicenseCode = "BQ000004",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            IsActive = true,
            RowVersion = rowVersion
        };
        repo.Licenses.Add(existing);
        repo.SoftwareItems.Add(new Software { Id = 3, Code = "PM000003", Name = "Visual Studio", IsActive = true, RowVersion = new byte[16] });

        var request = new LicenseRequest
        {
            SoftwareId = 3, // Changed softwareId
            LicenseType = "VOLUME",
            TotalQuantity = 15,
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(4, request, default));
        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task Capacity_floor_prevents_lowering_quantity_below_active_allocations()
    {
        var (service, repo, _, _) = Setup(FullAdmin());

        var rowVersion = new byte[16];
        var existing = new SoftwareLicense
        {
            Id = 4,
            SoftwareId = 2,
            LicenseCode = "BQ000004",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            IsActive = true,
            RowVersion = rowVersion
        };
        repo.Licenses.Add(existing);

        // Add 5 active assignments
        for (int i = 1; i <= 5; i++)
        {
            repo.Assignments.Add(new LicenseAssignment
            {
                Id = i,
                SoftwareLicenseId = 4,
                RevokedAtUtc = null,
                IsArchived = false
            });
        }

        // Attempting to lower total quantity to 3 (below 5 active)
        var request = new LicenseRequest
        {
            SoftwareId = 2,
            LicenseType = "VOLUME",
            TotalQuantity = 3,
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.WriteAsync(4, request, default));
        Assert.Equal(409, ex.Status);
        Assert.Equal("QUANTITY_BELOW_ACTIVE_ALLOCATIONS", ex.Code);
    }

    [Fact]
    public async Task Archive_is_blocked_when_active_allocations_exist()
    {
        var (service, repo, _, _) = Setup(FullAdmin());

        var rowVersion = new byte[16];
        var existing = new SoftwareLicense
        {
            Id = 4,
            SoftwareId = 2,
            LicenseCode = "BQ000004",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            IsActive = true,
            RowVersion = rowVersion
        };
        repo.Licenses.Add(existing);

        repo.Assignments.Add(new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 4,
            RevokedAtUtc = null,
            IsArchived = false
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ArchiveAsync(4, rowVersion, default));
        Assert.Equal(409, ex.Status);
        Assert.Equal("LICENSE_HAS_ACTIVE_ALLOCATIONS", ex.Code);
    }

    [Fact]
    public async Task Archive_succeeds_when_no_active_allocations()
    {
        var (service, repo, persistence, audit) = Setup(FullAdmin());

        var rowVersion = new byte[16];
        var existing = new SoftwareLicense
        {
            Id = 4,
            SoftwareId = 2,
            LicenseCode = "BQ000004",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            IsActive = true,
            RowVersion = rowVersion
        };
        repo.Licenses.Add(existing);

        await service.ArchiveAsync(4, rowVersion, default);

        Assert.False(existing.IsActive);
        Assert.Contains("licenses.archive", audit.Actions);
        Assert.Contains(4, persistence.Locked);
    }

    [Fact]
    public async Task Reader_without_cost_permission_does_not_see_purchase_cost()
    {
        var readerActor = new Actor(8, Permissions.LicenseRead);
        var (service, repo, _, _) = Setup(readerActor);

        var existing = new SoftwareLicense
        {
            Id = 4,
            SoftwareId = 2,
            LicenseCode = "BQ000004",
            LicenseType = "VOLUME",
            TotalQuantity = 10,
            PurchaseCost = 99000000,
            IsActive = true,
            RowVersion = new byte[16]
        };
        repo.Licenses.Add(existing);

        var dto = await service.GetAsync(4, default);
        Assert.Null(dto.PurchaseCost);
    }

    [Fact]
    public async Task Operations_require_explicit_permission_without_inheritance()
    {
        // Actor with NO permissions
        var (noPermsService, _, _, _) = Setup(new Actor(9));
        await Assert.ThrowsAsync<BusinessException>(() => noPermsService.ListAsync(new(), default));
        await Assert.ThrowsAsync<BusinessException>(() => noPermsService.GetAsync(1, default));
        await Assert.ThrowsAsync<BusinessException>(() => noPermsService.WriteAsync(null, new(), default));
        await Assert.ThrowsAsync<BusinessException>(() => noPermsService.ArchiveAsync(1, new byte[16], default));

        // Actor with only Read cannot create, update, or archive
        var (readOnlyService, _, _, _) = Setup(new Actor(9, Permissions.LicenseRead));
        await Assert.ThrowsAsync<BusinessException>(() => readOnlyService.WriteAsync(null, new() { SoftwareId = 2, LicenseType = "VOLUME", TotalQuantity = 1 }, default));
        await Assert.ThrowsAsync<BusinessException>(() => readOnlyService.ArchiveAsync(1, new byte[16], default));
    }
}
