using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

// In-memory contract fakes only: real FK/transaction/concurrency tests stay in isolated PostgreSQL.
public sealed class MasterServiceTests
{
    private sealed class Repository : IRepository
    {
        public readonly List<Department> Departments = [];
        public readonly List<AssetType> Types = [];
        public IQueryable<T> Query<T>(bool tracking = false) where T : class =>
            (typeof(T) == typeof(Department) ? Departments.Cast<T>() : Types.Cast<T>()).AsQueryable();
        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public void Add<T>(T entity) where T : class
        {
            if (entity is Department d) { d.Id = Departments.Count + 1; d.RowVersion = new byte[16]; Departments.Add(d); }
            else if (entity is AssetType t) { t.Id = Types.Count + 1; t.RowVersion = new byte[16]; Types.Add(t); }
            else throw new NotSupportedException();
        }
        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }
    private sealed class UnitOfWork : IUnitOfWork { public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work(); }
    private sealed class Actor : IActor
    { public long? UserId => 1; public Guid CorrelationId => Guid.Empty; public string? Method => null; public string? Path => null; public bool Has(string p) => true; }
    private sealed class Audit : IAuditWriter
    {
        public readonly List<string> Actions = [];
        public void Record(string a, object? e, long? u, string t, string o = "SUCCESS", object? before = null, object? after = null, string? failureCode = null) => Actions.Add(a);
    }
    private static MasterService Service(Repository? repo = null, Audit? audit = null) =>
        new(repo ?? new(), new UnitOfWork(), audit ?? new(), new Actor());

    [Theory]
    [InlineData(true, "", "IT")]
    [InlineData(true, "IT", " ")]
    [InlineData(false, "", "Laptop")]
    [InlineData(false, "LAPTOP", "")]
    public async Task Master_code_and_name_are_required(bool department, string code, string name)
    { var e = await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(department, null, new() { Code = code, Name = name }, default)); Assert.Equal(400, e.Status); }

    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public async Task Asset_type_useful_life_must_be_positive(int months)
    { await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(false, null, new() { Code = "TYPE", Name = "Type", DefaultUsefulLifeMonths = months }, default)); }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Master_rejects_fields_owned_by_the_other_resource(bool department)
    { await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(department, null, new() { Code = "CODE", Name = "Name", ParentDepartmentId = department ? null : 1, DefaultUsefulLifeMonths = department ? 12 : null }, default)); }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Duplicate_master_code_is_case_insensitive(bool department)
    {
        var repo = new Repository();
        if (department) repo.Departments.Add(new() { Id = 1, Code = "IT", Name = "IT", RowVersion = new byte[16] });
        else repo.Types.Add(new() { Id = 1, Code = "IT", Name = "IT", RowVersion = new byte[16] });
        var e = await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(department, null, new() { Code = "it", Name = "Duplicate" }, default));
        Assert.Equal(409, e.Status);
    }

    [Fact]
    public async Task Department_parent_cannot_be_self_or_form_a_cycle()
    {
        var repo = new Repository(); repo.Departments.Add(new() { Id = 1, Code = "IT", Name = "IT", RowVersion = new byte[16] });
        var e = await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(true, 1,
            new() { Code = "IT", Name = "IT", ParentDepartmentId = 1, RowVersion = Convert.ToBase64String(new byte[16]) }, default));
        Assert.True(e.Errors!.ContainsKey("parentDepartmentId"));
    }

    [Fact]
    public async Task Department_parent_must_exist_and_be_active()
    {
        var repo = new Repository(); repo.Departments.Add(new() { Id = 1, Code = "OLD", Name = "Old", IsActive = false });
        foreach (var id in new[] { 1L, 99L })
            await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(true, null, new() { Code = "NEW", Name = "New", ParentDepartmentId = id }, default));
    }

    [Theory]
    [InlineData("Inactive", "")]
    [InlineData("Retired", "Reason")]
    public async Task Master_status_requires_reason_and_valid_status(string status, string reason)
    { await Assert.ThrowsAsync<BusinessException>(() => Service().StatusAsync(true, 1, new(status, reason, Convert.ToBase64String(new byte[16])), default)); }

    [Fact]
    public async Task Department_code_cannot_be_changed_on_update()
    {
        var repo = new Repository();
        repo.Departments.Add(new() { Id = 1, Code = "HR", Name = "Human Resources", RowVersion = new byte[16] });
        var e = await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(true, 1,
            new() { Code = "HR_NEW", Name = "HR Renamed", RowVersion = Convert.ToBase64String(new byte[16]) }, default));
        Assert.Equal(400, e.Status);
        Assert.True(e.Errors!.ContainsKey("code"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Department_parent_id_must_be_positive(long parentId)
    {
        var e = await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(true, null,
            new() { Code = "SUB", Name = "Sub Department", ParentDepartmentId = parentId }, default));
        Assert.Equal(400, e.Status);
        Assert.True(e.Errors!.ContainsKey("parentDepartmentId"));
    }

    [Fact]
    public async Task Master_write_rejects_version_on_create_and_requires_it_on_update()
    {
        var validVersion = Convert.ToBase64String(new byte[16]);
        // Tạo mới có truyền RowVersion -> lỗi 400
        var createEx = await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(true, null,
            new() { Code = "ACC", Name = "Accounting", RowVersion = validVersion }, default));
        Assert.Equal(400, createEx.Status);

        // Cập nhật không truyền RowVersion -> lỗi 400
        var repo = new Repository();
        repo.Departments.Add(new() { Id = 1, Code = "ACC", Name = "Accounting", RowVersion = new byte[16] });
        var updateEx = await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(true, 1,
            new() { Code = "ACC", Name = "Accounting Updated", RowVersion = null }, default));
        Assert.Equal(400, updateEx.Status);
    }

    [Fact]
    public async Task Master_status_success_toggles_active_state_and_writes_audit()
    {
        var repo = new Repository();
        var audit = new Audit();
        repo.Departments.Add(new() { Id = 1, Code = "IT", Name = "IT Dept", IsActive = true, RowVersion = new byte[16] });

        var result = await Service(repo, audit).StatusAsync(true, 1,
            new("Inactive", "Cơ cấu lại tổ chức", Convert.ToBase64String(new byte[16])), default);

        Assert.False(result.IsActive);
        Assert.False(repo.Departments[0].IsActive);
        Assert.Contains("departments.status.change", audit.Actions);
    }
}