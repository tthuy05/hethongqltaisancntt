using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class AssetServiceTests
{
    private static readonly byte[] Version = Enumerable.Repeat((byte)7, 16).ToArray();
    private static string EncodedVersion => Convert.ToBase64String(Version);

    private sealed class Repository : IRepository
    {
        public readonly List<Asset> Assets = [];
        public readonly List<AssetType> AssetTypes = [];
        public readonly List<Department> Departments = [];
        public readonly List<AssetStatusHistory> Histories = [];
        public readonly List<string> Operations = [];
        public bool WorkflowActive { get; set; }

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            if (tracking) Operations.Add("track:" + typeof(T).Name);
            IEnumerable<T> values = typeof(T) == typeof(Asset) ? Assets.Cast<T>() :
                typeof(T) == typeof(AssetType) ? AssetTypes.Cast<T>() :
                typeof(T) == typeof(Department) ? Departments.Cast<T>() :
                typeof(T) == typeof(AssetStatusHistory) ? Histories.Cast<T>() : throw new NotSupportedException();
            return values.AsQueryable();
        }

        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());

        public void Add<T>(T entity) where T : class
        {
            if (entity is Asset asset)
            {
                asset.Id = Assets.Count + 1;
                asset.RowVersion = Version.ToArray();
                Assets.Add(asset);
            }
            else if (entity is AssetStatusHistory history)
            {
                Histories.Add(history);
            }
            else throw new NotSupportedException();
        }

        public void ExpectVersion<T>(T entity, byte[] version) where T : class
        {
            Assert.IsType<Asset>(entity);
            if (!Version.SequenceEqual(version))
                throw new BusinessException(409, "CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi bởi người khác.");
        }

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) { Operations.Add("lock:" + resource); return Task.CompletedTask; }
        public Task LockAssetAsync(long id, CancellationToken ct) { Operations.Add("asset:" + id); return Task.CompletedTask; }
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => Task.FromResult(WorkflowActive);
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) =>
            query.Where(x => x.AssetCode.Contains(keyword) || x.Name.Contains(keyword));
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work();
    }

    private sealed class Actor(long? id = 1, params string[] permissions) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId => Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");
        public string? Method => "POST";
        public string? Path => "/api/v1/assets";
        public bool Has(string permission) => permissions.Contains(permission);
    }

    private sealed class Audit : IAuditWriter
    {
        public readonly List<string> Actions = [];
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null) => Actions.Add(action);
    }

    private sealed class Fixture
    {
        public readonly Repository Repo = new();
        public readonly UnitOfWork Uow = new();
        public readonly Audit Audit = new();
        public AssetService Service { get; }

        public Fixture(Actor? actor = null)
        {
            Repo.AssetTypes.Add(new() { Id = 1, Code = "LAPTOP", Name = "Laptop", IsActive = true });
            Repo.Departments.Add(new() { Id = 1, Code = "IT", Name = "IT Department", IsActive = true });
            Service = new(Repo, Uow, Audit, actor ?? new(1, Permissions.AssetCost));
        }

        public Asset AddStandardAsset(string status = "IN_STOCK")
        {
            var asset = new Asset
            {
                Id = 1,
                AssetCode = "AST-001",
                Name = "Dell Latitude 5420",
                AssetTypeId = 1,
                OwningDepartmentId = 1,
                CurrentStatus = status,
                IsArchived = false,
                RowVersion = Version.ToArray()
            };
            Repo.Assets.Add(asset);
            return asset;
        }
    }

    [Fact]
    public async Task ArchiveAsync_WhenAssetHasActiveWorkflow_ThrowsConflict()
    {
        var f = new Fixture();
        f.AddStandardAsset();
        f.Repo.WorkflowActive = true;

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            f.Service.ArchiveAsync(1, Version, CancellationToken.None));

        Assert.Equal(409, ex.Status);
        Assert.Equal("ASSET_ACTIVE_WORKFLOW", ex.Code);
        Assert.False(f.Repo.Assets[0].IsArchived);
    }

    [Fact]
    public async Task ArchiveAsync_WhenNoActiveWorkflow_SucceedsAndLocksAsset()
    {
        var f = new Fixture();
        f.AddStandardAsset();
        f.Repo.WorkflowActive = false;

        await f.Service.ArchiveAsync(1, Version, CancellationToken.None);

        Assert.True(f.Repo.Assets[0].IsArchived);
        Assert.NotNull(f.Repo.Assets[0].ArchivedAtUtc);
        Assert.Contains("asset:1", f.Repo.Operations);
        Assert.Contains("assets.archive", f.Audit.Actions);
    }

    [Fact]
    public async Task StatusAsync_Retire_WhenActiveWorkflow_ThrowsConflict()
    {
        var f = new Fixture();
        f.AddStandardAsset("BROKEN");
        f.Repo.WorkflowActive = true;
        var req = new StatusRequest("Retired", "Thanh lý thiết bị hỏng", EncodedVersion);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            f.Service.StatusAsync(1, req, CancellationToken.None));

        Assert.Equal(409, ex.Status);
        Assert.Equal("ASSET_ACTIVE_WORKFLOW", ex.Code);
        Assert.Equal("BROKEN", f.Repo.Assets[0].CurrentStatus);
    }

    [Fact]
    public async Task StatusAsync_Retire_FromInvalidStatus_ThrowsConflict()
    {
        var f = new Fixture();
        f.AddStandardAsset("IN_USE");
        var req = new StatusRequest("Retired", "Cố tình thanh lý máy đang dùng", EncodedVersion);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            f.Service.StatusAsync(1, req, CancellationToken.None));

        Assert.Equal(409, ex.Status);
        Assert.Equal("ASSET_STATUS_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task StatusAsync_Retire_WhenValid_TransitionsAndRecordsHistory()
    {
        var f = new Fixture();
        f.AddStandardAsset("IN_STOCK");
        f.Repo.WorkflowActive = false;
        var req = new StatusRequest("Retired", "Hết hạn sử dụng khấu hao", EncodedVersion);

        var result = await f.Service.StatusAsync(1, req, CancellationToken.None);

        Assert.Equal("RETIRED", f.Repo.Assets[0].CurrentStatus);
        Assert.Contains("asset:1", f.Repo.Operations);
        Assert.Single(f.Repo.Histories);
        var history = f.Repo.Histories[0];
        Assert.Equal("IN_STOCK", history.FromStatus);
        Assert.Equal("RETIRED", history.ToStatus);
        Assert.Equal("Hết hạn sử dụng khấu hao", history.Reason);
        Assert.Contains("assets.status.change", f.Audit.Actions);
    }

    [Fact]
    public async Task ArchiveAsync_WithStaleVersion_ThrowsConcurrencyConflict()
    {
        var f = new Fixture();
        f.AddStandardAsset();
        var staleVersion = Enumerable.Repeat((byte)99, 16).ToArray();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            f.Service.ArchiveAsync(1, staleVersion, CancellationToken.None));

        Assert.Equal(409, ex.Status);
        Assert.Equal("CONCURRENCY_CONFLICT", ex.Code);
    }
}