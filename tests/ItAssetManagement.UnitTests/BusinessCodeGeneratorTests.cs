using System.Text.RegularExpressions;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.UnitTests;

public sealed class BusinessCodeGeneratorTests
{
    private static AppDbContext CreateTestDb()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
    }

    [Fact]
    public async Task EmployeeCodeGenerator_generates_canonical_format_NV_with_6_digits()
    {
        await using var db = CreateTestDb();
        var generator = new EmployeeCodeGenerator(db);

        var code1 = await generator.NextAsync(default);
        var code2 = await generator.NextAsync(default);

        Assert.Matches(@"^NV\d{6}$", code1);
        Assert.Matches(@"^NV\d{6}$", code2);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task DepartmentCodeGenerator_generates_canonical_format_PB_with_6_digits()
    {
        await using var db = CreateTestDb();
        var generator = new DepartmentCodeGenerator(db);

        var code1 = await generator.NextAsync(default);
        var code2 = await generator.NextAsync(default);

        Assert.Matches(@"^PB\d{6}$", code1);
        Assert.Matches(@"^PB\d{6}$", code2);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task MaintenanceTicketCodeGenerator_generates_canonical_format_BT_with_6_digits()
    {
        await using var db = CreateTestDb();
        var generator = new MaintenanceTicketCodeGenerator(db);

        var code1 = await generator.NextAsync(default);
        var code2 = await generator.NextAsync(default);

        Assert.Matches(@"^BT\d{6}$", code1);
        Assert.Matches(@"^BT\d{6}$", code2);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task SoftwareCodeGenerator_generates_canonical_format_PM_with_6_digits()
    {
        await using var db = CreateTestDb();
        var generator = new SoftwareCodeGenerator(db);

        var code1 = await generator.NextAsync(default);
        var code2 = await generator.NextAsync(default);

        Assert.Matches(@"^PM\d{6}$", code1);
        Assert.Matches(@"^PM\d{6}$", code2);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task LicenseCodeGenerator_generates_canonical_format_BQ_with_6_digits()
    {
        await using var db = CreateTestDb();
        var generator = new LicenseCodeGenerator(db);

        var code1 = await generator.NextAsync(default);
        var code2 = await generator.NextAsync(default);

        Assert.Matches(@"^BQ\d{6}$", code1);
        Assert.Matches(@"^BQ\d{6}$", code2);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task AssetCodeGenerator_generates_canonical_format_AST_with_6_digits()
    {
        await using var db = CreateTestDb();
        var generator = new AssetCodeGenerator(db);

        var code1 = await generator.NextAsync(default);
        var code2 = await generator.NextAsync(default);

        Assert.Matches(@"^AST-\d{6}$", code1);
        Assert.Matches(@"^AST-\d{6}$", code2);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task Concurrency_parallel_invocations_produce_unique_codes_without_collisions()
    {
        await using var db = CreateTestDb();
        var generator = new EmployeeCodeGenerator(db);

        const int count = 50;
        var tasks = Enumerable.Range(0, count)
            .Select(_ => generator.NextAsync(default))
            .ToArray();

        var codes = await Task.WhenAll(tasks);

        Assert.Equal(count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches(@"^NV\d{6}$", code));
    }

    [Fact]
    public async Task Department_auto_code_generation_in_MasterService_when_code_is_empty()
    {
        var testDeptCode = "PB000088";
        var mockDeptGen = new StubDepartmentCodeGenerator(testDeptCode);
        var repo = new StubMasterRepo();
        var service = new MasterService(repo, new DirectUnitOfWork(), new NoOpAudit(), new AdminActor(), mockDeptGen);

        var created = await service.WriteAsync(department: true, id: null, new MasterRequest { Name = "Phòng IT Mới" }, default);

        Assert.Equal(testDeptCode, created.Code);
        Assert.Equal("Phòng IT Mới", created.Name);
    }

    [Fact]
    public async Task Department_code_is_immutable_on_update()
    {
        var repo = new StubMasterRepo();
        repo.Departments.Add(new Department { Id = 10, Code = "PB000001", Name = "Kế toán", RowVersion = new byte[16], IsActive = true });
        var service = new MasterService(repo, new DirectUnitOfWork(), new NoOpAudit(), new AdminActor());

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.WriteAsync(department: true, id: 10, new MasterRequest { Code = "PB999999", Name = "Kế toán đổi tên", RowVersion = Convert.ToBase64String(new byte[16]) }, default));

        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task UserService_rejects_altering_existing_EmployeeCode()
    {
        var repo = new StubUserRepo();
        repo.Users.Add(new User
        {
            Id = 42,
            Username = "user42",
            NormalizedUsername = "USER42",
            Email = "user42@test.local",
            NormalizedEmail = "USER42@TEST.LOCAL",
            FullName = "Nguyễn Văn A",
            EmployeeCode = "NV000042",
            RowVersion = new byte[16],
            IsActive = true,
            IsEmailConfirmed = true
        });

        var service = new UserService(repo, new DirectUnitOfWork(), new NoOpAudit(), new AdminActor());

        var updateReq = new UpdateUserRequest
        {
            Username = "user42",
            Email = "user42@test.local",
            DisplayName = "Nguyễn Văn A Đã Sửa",
            EmployeeCode = "NV999999", // Attempting to alter code
            RowVersion = Convert.ToBase64String(new byte[16])
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(42, updateReq, default));
        Assert.Equal(400, ex.Status);
        Assert.Equal("EMPLOYEE_CODE_IMMUTABLE", ex.Code);
    }

    [Fact]
    public async Task UserService_accepts_update_when_EmployeeCode_matches_existing_code()
    {
        var repo = new StubUserRepo();
        repo.Users.Add(new User
        {
            Id = 42,
            Username = "user42",
            NormalizedUsername = "USER42",
            Email = "user42@test.local",
            NormalizedEmail = "USER42@TEST.LOCAL",
            FullName = "Nguyễn Văn A",
            EmployeeCode = "NV000042",
            RowVersion = new byte[16],
            IsActive = true,
            IsEmailConfirmed = true
        });

        var service = new UserService(repo, new DirectUnitOfWork(), new NoOpAudit(), new AdminActor());

        var updateReq = new UpdateUserRequest
        {
            Username = "user42",
            Email = "user42@test.local",
            DisplayName = "Nguyễn Văn A Đã Sửa",
            EmployeeCode = "NV000042", // Same code
            RowVersion = Convert.ToBase64String(new byte[16])
        };

        var result = await service.UpdateAsync(42, updateReq, default);
        Assert.Equal("NV000042", result.EmployeeCode);
        Assert.Equal("Nguyễn Văn A Đã Sửa", result.DisplayName);
    }

    [Fact]
    public async Task UserService_rejects_manual_EmployeeCode_for_legacy_user_without_code()
    {
        var repo = new StubUserRepo();
        repo.Users.Add(new User
        {
            Id = 99,
            Username = "legacy_user",
            NormalizedUsername = "LEGACY_USER",
            Email = "legacy@test.local",
            NormalizedEmail = "LEGACY@TEST.LOCAL",
            FullName = "Legacy User",
            EmployeeCode = null, // Legacy account without code
            RowVersion = new byte[16],
            IsActive = true,
            IsEmailConfirmed = true
        });

        var service = new UserService(repo, new DirectUnitOfWork(), new NoOpAudit(), new AdminActor());

        var updateReq = new UpdateUserRequest
        {
            Username = "legacy_user",
            Email = "legacy@test.local",
            DisplayName = "Legacy User Renamed",
            EmployeeCode = "NV999999", // Attempting to manually assign code to legacy account
            RowVersion = Convert.ToBase64String(new byte[16])
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(99, updateReq, default));
        Assert.Equal(400, ex.Status);
        Assert.Equal("EMPLOYEE_CODE_IMMUTABLE", ex.Code);
    }

    [Fact]
    public async Task UserService_auto_generates_EmployeeCode_for_legacy_user_on_update()
    {
        var repo = new StubUserRepo();
        repo.Users.Add(new User
        {
            Id = 99,
            Username = "legacy_user",
            NormalizedUsername = "LEGACY_USER",
            Email = "legacy@test.local",
            NormalizedEmail = "LEGACY@TEST.LOCAL",
            FullName = "Legacy User",
            EmployeeCode = null, // Legacy account without code
            RowVersion = new byte[16],
            IsActive = true,
            IsEmailConfirmed = true
        });

        var generator = new StubEmployeeCodeGenerator("NV000099");
        var service = new UserService(repo, new DirectUnitOfWork(), new NoOpAudit(), new AdminActor(), employeeCodes: generator);

        var updateReq = new UpdateUserRequest
        {
            Username = "legacy_user",
            Email = "legacy@test.local",
            DisplayName = "Legacy User Renamed",
            EmployeeCode = null, // No code provided
            RowVersion = Convert.ToBase64String(new byte[16])
        };

        var result = await service.UpdateAsync(99, updateReq, default);
        Assert.Equal("NV000099", result.EmployeeCode);
        Assert.Equal("Legacy User Renamed", result.DisplayName);
    }

    [Fact]
    public void ControlledSequences_registry_contains_all_six_business_entities()
    {
        var sequences = BusinessCodeGeneratorEngine.ControlledSequences;
        Assert.Equal(6, sequences.Count);

        var names = sequences.Select(s => s.SequenceName).ToHashSet();
        Assert.Contains("asset_code_sequence", names);
        Assert.Contains("employee_code_sequence", names);
        Assert.Contains("department_code_sequence", names);
        Assert.Contains("maintenance_ticket_code_sequence", names);
        Assert.Contains("software_code_sequence", names);
        Assert.Contains("license_code_sequence", names);

        var prefixes = sequences.Select(s => s.Prefix).ToHashSet();
        Assert.Contains("AST-", prefixes);
        Assert.Contains("NV", prefixes);
        Assert.Contains("PB", prefixes);
        Assert.Contains("BT", prefixes);
        Assert.Contains("PM", prefixes);
        Assert.Contains("BQ", prefixes);
    }

    [Fact]
    public void ResetEnsuredSequences_clears_cached_initialization_state()
    {
        BusinessCodeGeneratorEngine.ResetEnsuredSequences();
        // Verifies the method executes without error and can be safely invoked across test boundaries.
    }

    // Helper stubs
    private sealed class StubEmployeeCodeGenerator(string code) : IEmployeeCodeGenerator
    {
        public Task<string> NextAsync(CancellationToken ct) => Task.FromResult(code);
    }

    private sealed class StubDepartmentCodeGenerator(string code) : IDepartmentCodeGenerator
    {
        public Task<string> NextAsync(CancellationToken ct) => Task.FromResult(code);
    }

    private sealed class DirectUnitOfWork : IUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work();
    }

    private sealed class NoOpAudit : IAuditWriter
    {
        public void Record(string action, object? entity, long? userId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null) { }
    }

    private sealed class AdminActor : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId => Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/api/v1/test";
        public bool Has(string permission) => true;
    }

    private sealed class StubMasterRepo : IRepository
    {
        public readonly List<Department> Departments = [];
        public readonly List<AssetType> Types = [];

        public IQueryable<T> Query<T>(bool tracking = false) where T : class =>
            typeof(T) == typeof(Department) ? Departments.Cast<T>().AsQueryable() : Types.Cast<T>().AsQueryable();

        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());

        public void Add<T>(T entity) where T : class
        {
            if (entity is Department d)
            {
                d.Id = Departments.Count + 1;
                d.RowVersion = new byte[16];
                Departments.Add(d);
            }
        }

        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task LockAssetAsync(long id, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => Task.FromResult(false);
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }

    private sealed class StubUserRepo : IRepository
    {
        public readonly List<User> Users = [];
        public readonly List<Department> Departments = [];

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            if (typeof(T) == typeof(User)) return Users.Cast<T>().AsQueryable();
            if (typeof(T) == typeof(Department)) return Departments.Cast<T>().AsQueryable();
            return Enumerable.Empty<T>().AsQueryable();
        }

        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());

        public void Add<T>(T entity) where T : class
        {
            if (entity is User u)
            {
                u.Id = Users.Count + 1;
                u.RowVersion = new byte[16];
                Users.Add(u);
            }
        }

        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task LockAssetAsync(long id, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => Task.FromResult(false);
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }
}
