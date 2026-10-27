using ItAssetManagement.Application.Contracts.Security;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using Xunit;

namespace ItAssetManagement.UnitTests;

public sealed class LicenseAssignmentServiceTests
{
    private sealed class Repo : IRepository
    {
        public List<SoftwareLicense> Licenses = [];
        public List<Software> SoftwareItems = [];
        public List<LicenseAssignment> Assignments = [];
        public List<User> Users = [];
        public List<Asset> Assets = [];
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
            if (typeof(T) == typeof(Asset)) return Assets.Cast<T>().AsQueryable();
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
            if (entity is LicenseAssignment a)
            {
                if (a.Id == 0) a.Id = Assignments.Count + 1;
                a.RowVersion = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
                Assignments.Add(a);
            }
        }

        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) { Operations.Add("save"); return Task.CompletedTask; }
        public Task LockAsync(string resource, CancellationToken ct) { Operations.Add("lock:" + resource); return Task.CompletedTask; }
        public Task LockAssetAsync(long id, CancellationToken ct) { Operations.Add("asset:" + id); return Task.CompletedTask; }
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) => throw new NotSupportedException();
    }

    private sealed class Persistence : ILicensePersistence
    {
        public List<long> Locked = [];
        public List<long> LockedAssignments = [];
        public Task LockAsync(long id, CancellationToken ct) { Locked.Add(id); return Task.CompletedTask; }
        public Task LockAssignmentAsync(long id, CancellationToken ct) { LockedAssignments.Add(id); return Task.CompletedTask; }
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
        public string? Path => "/api/v1/license-assignments";
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

    private static (LicenseAssignmentService service, Repo repo, Persistence persistence, Audit audit) CreateFixture(params string[] actorGrants)
    {
        var repo = new Repo();
        var persistence = new Persistence();
        var audit = new Audit();
        var grants = actorGrants.Length > 0 ? actorGrants : [
            LicensePermissions.AssignmentRead,
            LicensePermissions.AssignmentAssign,
            LicensePermissions.AssignmentRevoke,
            LicensePermissions.AssignmentTransfer,
            Permissions.LicenseRead
        ];
        var actor = new Actor(7, grants);

        // Standard RBAC setup
        var role = new Role { Id = 1, Code = "ADMIN_IT", Name = "Admin IT", IsActive = true };
        repo.Roles.Add(role);
        repo.UserRoles.Add(new UserRole { UserId = 7, RoleId = 1 });

        foreach (var code in LicensePermissions.All.Concat([Permissions.LicenseRead]))
        {
            var p = new Permission { Id = repo.PermissionsList.Count + 1, Code = code, Name = code, IsActive = true };
            repo.PermissionsList.Add(p);
            repo.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionId = p.Id });
        }

        var software = new Software { Id = 10, Code = "PM000001", Name = "Office 365", IsActive = true, RowVersion = new byte[16] };
        repo.SoftwareItems.Add(software);

        var license = new SoftwareLicense
        {
            Id = 100,
            LicenseCode = "BQ000001",
            SoftwareId = 10,
            LicenseType = "PER_USER",
            TotalQuantity = 5,
            IsActive = true,
            RowVersion = new byte[16],
            CreatedAtUtc = DateTime.UtcNow
        };
        repo.Licenses.Add(license);

        var adminUser = new User { Id = 7, Username = "admin", FullName = "Admin IT", Email = "admin@example.com", IsActive = true, IsEmailConfirmed = true, TokenVersion = 1 };
        var user = new User { Id = 20, Username = "dev.user", FullName = "Nguyễn Văn A", Email = "a@example.com", IsActive = true, IsEmailConfirmed = true, TokenVersion = 1 };
        var user2 = new User { Id = 21, Username = "dev.user2", FullName = "Trần Thị B", Email = "b@example.com", IsActive = true, IsEmailConfirmed = true, TokenVersion = 1 };
        repo.Users.AddRange([adminUser, user, user2]);

        var asset = new Asset { Id = 50, AssetCode = "AST-000001", Name = "Laptop Dell XPS", CurrentStatus = "IN_USE", IsArchived = false };
        var asset2 = new Asset { Id = 51, AssetCode = "AST-000002", Name = "Desktop HP", CurrentStatus = "IN_USE", IsArchived = false };
        repo.Assets.AddRange([asset, asset2]);

        var service = new LicenseAssignmentService(repo, persistence, new Uow(), actor, audit, TimeProvider.System);
        return (service, repo, persistence, audit);
    }

    [Fact]
    public async Task AssignAsync_WhenValidUser_CreatesAssignment_And_RecordsAudit()
    {
        var (service, repo, persistence, audit) = CreateFixture();
        var request = new AssignLicenseRequest
        {
            SoftwareLicenseId = 100,
            UserId = 20,
            Notes = "Cấp cho nhân viên mới"
        };

        var result = await service.AssignAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(100, result.SoftwareLicenseId);
        Assert.Equal("BQ000001", result.LicenseCode);
        Assert.Equal("Office 365", result.SoftwareName);
        Assert.Equal(20, result.AssignedUserId);
        Assert.Equal("Nguyễn Văn A", result.AssignedUserFullName);
        Assert.Null(result.AssignedAssetId);
        Assert.Contains(100, persistence.Locked);
        Assert.Single(repo.Assignments);
        Assert.Contains(audit.Actions, a => a == "license-assignments.assign");
    }

    [Fact]
    public async Task AssignAsync_WhenValidAsset_CreatesAssignment_And_RecordsAudit()
    {
        var (service, repo, persistence, audit) = CreateFixture();
        repo.Licenses[0].LicenseType = "PER_DEVICE";
        var request = new AssignLicenseRequest
        {
            SoftwareLicenseId = 100,
            AssetId = 50,
            Notes = "Cấp cho laptop phòng IT"
        };

        var result = await service.AssignAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(50, result.AssignedAssetId);
        Assert.Equal("AST-000001", result.AssignedAssetCode);
        Assert.Equal("Laptop Dell XPS", result.AssignedAssetName);
        Assert.Null(result.AssignedUserId);
        Assert.Single(repo.Assignments);
        Assert.Contains(audit.Actions, a => a == "license-assignments.assign");
    }

    [Fact]
    public async Task AssignAsync_WhenTargetXorViolated_ThrowsValidation()
    {
        var (service, _, _, _) = CreateFixture();

        // Both provided
        var req1 = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20, AssetId = 50 };
        var ex1 = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(req1, CancellationToken.None));
        Assert.Equal(400, ex1.Status);

        // Neither provided
        var req2 = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = null, AssetId = null };
        var ex2 = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(req2, CancellationToken.None));
        Assert.Equal(400, ex2.Status);
    }

    [Fact]
    public async Task AssignAsync_WhenCapacityExhausted_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Licenses[0].TotalQuantity = 1;
        repo.Assignments.Add(new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 21,
            AssignedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[16]
        });

        var request = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20 };
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("LICENSE_CAPACITY_EXHAUSTED", ex.Code);
    }

    [Fact]
    public async Task AssignAsync_WhenLicenseExpired_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Licenses[0].ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);

        var request = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20 };
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("LICENSE_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task AssignAsync_WhenLicenseInactive_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Licenses[0].IsActive = false;

        var request = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20 };
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("LICENSE_INACTIVE", ex.Code);
    }

    [Fact]
    public async Task AssignAsync_WhenDuplicateTarget_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Assignments.Add(new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 20,
            AssignedAtUtc = DateTime.UtcNow,
            RowVersion = new byte[16]
        });

        var request = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20 };
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("DUPLICATE_ASSIGNMENT", ex.Code);
    }

    [Fact]
    public async Task AssignAsync_WhenUserInactive_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Users.Single(u => u.Id == 20).IsActive = false;

        var request = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20 };
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("USER_NOT_ELIGIBLE", ex.Code);
    }

    [Fact]
    public async Task RevokeAsync_WhenValid_SetsRevokedAtAndRevokedBy()
    {
        var (service, repo, persistence, audit) = CreateFixture();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var assignment = new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 20,
            AssignedAtUtc = DateTime.UtcNow.AddDays(-10),
            RowVersion = rowVersion
        };
        repo.Assignments.Add(assignment);

        var request = new RevokeLicenseAssignmentRequest
        {
            Reason = "Nhân viên nghỉ việc",
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var result = await service.RevokeAsync(1, request, CancellationToken.None);

        Assert.NotNull(result.RevokedAtUtc);
        Assert.Equal(7, result.RevokedByUserId);
        Assert.Contains(1, persistence.LockedAssignments);
        Assert.Contains("Nhân viên nghỉ việc", result.Notes);
        Assert.Contains(audit.Actions, a => a == "license-assignments.revoke");
    }

    [Fact]
    public async Task RevokeAsync_WhenAlreadyRevoked_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        repo.Assignments.Add(new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 20,
            AssignedAtUtc = DateTime.UtcNow.AddDays(-10),
            RevokedAtUtc = DateTime.UtcNow.AddDays(-1),
            RowVersion = rowVersion
        });

        var request = new RevokeLicenseAssignmentRequest
        {
            Reason = "Thu hồi lại",
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RevokeAsync(1, request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("ALREADY_REVOKED", ex.Code);
    }

    [Fact]
    public async Task RevokeAsync_WhenVersionConflict_Throws409()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Assignments.Add(new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 20,
            AssignedAtUtc = DateTime.UtcNow.AddDays(-10),
            RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }
        });

        var staleVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 });
        var request = new RevokeLicenseAssignmentRequest { Reason = "test", RowVersion = staleVersion };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RevokeAsync(1, request, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("CONCURRENCY_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task TransferAsync_WhenValid_RevokesOldAndCreatesNewAtomic()
    {
        var (service, repo, persistence, audit) = CreateFixture();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var old = new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 20,
            AssignedAtUtc = DateTime.UtcNow.AddDays(-10),
            RowVersion = rowVersion
        };
        repo.Assignments.Add(old);

        var request = new TransferLicenseAssignmentRequest
        {
            UserId = 21,
            Reason = "Chuyển giao công việc",
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var result = await service.TransferAsync(1, request, CancellationToken.None);

        Assert.NotNull(result.Previous.RevokedAtUtc);
        Assert.Equal(20, result.Previous.AssignedUserId);
        Assert.Null(result.Current.RevokedAtUtc);
        Assert.Equal(21, result.Current.AssignedUserId);
        Assert.Equal(2, repo.Assignments.Count);
        Assert.Contains(audit.Actions, a => a == "license-assignments.revoke");
        Assert.Contains(audit.Actions, a => a == "license-assignments.assign");
    }

    [Fact]
    public async Task TransferAsync_WhenSameTarget_Throws400()
    {
        var (service, repo, _, _) = CreateFixture();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        repo.Assignments.Add(new LicenseAssignment
        {
            Id = 1,
            SoftwareLicenseId = 100,
            AssignedUserId = 20,
            AssignedAtUtc = DateTime.UtcNow.AddDays(-10),
            RowVersion = rowVersion
        });

        var request = new TransferLicenseAssignmentRequest
        {
            UserId = 20,
            Reason = "Cùng người dùng",
            RowVersion = Convert.ToBase64String(rowVersion)
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.TransferAsync(1, request, CancellationToken.None));
        Assert.Equal(400, ex.Status);
        Assert.Equal("SAME_TARGET", ex.Code);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusAndTarget()
    {
        var (service, repo, _, _) = CreateFixture();
        repo.Assignments.AddRange([
            new LicenseAssignment { Id = 1, SoftwareLicenseId = 100, AssignedUserId = 20, AssignedAtUtc = DateTime.UtcNow.AddDays(-5), RowVersion = new byte[16] },
            new LicenseAssignment { Id = 2, SoftwareLicenseId = 100, AssignedAssetId = 50, AssignedAtUtc = DateTime.UtcNow.AddDays(-4), RevokedAtUtc = DateTime.UtcNow.AddDays(-1), RowVersion = new byte[16] },
            new LicenseAssignment { Id = 3, SoftwareLicenseId = 100, AssignedUserId = 21, AssignedAtUtc = DateTime.UtcNow.AddDays(-2), RowVersion = new byte[16] }
        ]);

        var activeOnly = await service.ListAsync(new LicenseAssignmentQuery { Status = "Active" }, CancellationToken.None);
        Assert.Equal(2, activeOnly.TotalItems);

        var revokedOnly = await service.ListAsync(new LicenseAssignmentQuery { Status = "Revoked" }, CancellationToken.None);
        Assert.Equal(1, revokedOnly.TotalItems);

        var userTargetOnly = await service.ListAsync(new LicenseAssignmentQuery { TargetType = "user" }, CancellationToken.None);
        Assert.Equal(2, userTargetOnly.TotalItems);
    }

    [Fact]
    public async Task Require_WhenActorLacksPermission_Throws403()
    {
        var (service, _, _, _) = CreateFixture("some.other.permission");
        var req = new AssignLicenseRequest { SoftwareLicenseId = 100, UserId = 20 };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AssignAsync(req, CancellationToken.None));
        Assert.Equal(403, ex.Status);
    }
}
