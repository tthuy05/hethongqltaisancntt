using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class MaintenanceServiceTests
{
    private static readonly DateTime StartedAt = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Version = Enumerable.Repeat((byte)5, 16).ToArray();
    private static string EncodedVersion => Convert.ToBase64String(Version);

    private sealed class Repository : IRepository
    {
        public readonly List<Asset> Assets = [new() { Id = 1, CurrentStatus = "IN_STOCK", RowVersion = Version.ToArray() }];
        public readonly List<MaintenanceTicket> Tickets = [];
        public readonly List<AssetStatusHistory> Histories = [];
        public readonly List<string> Operations = [];
        public int Saves { get; private set; }
        public int ExpectedVersions { get; private set; }

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            if (tracking) Operations.Add("track:" + typeof(T).Name);
            IEnumerable<T> values = typeof(T) == typeof(Asset) ? Assets.Cast<T>() :
                typeof(T) == typeof(MaintenanceTicket) ? Tickets.Cast<T>() :
                typeof(T) == typeof(AssetStatusHistory) ? Histories.Cast<T>() : throw new NotSupportedException();
            return values.AsQueryable();
        }

        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());

        public void Add<T>(T entity) where T : class
        {
            if (entity is MaintenanceTicket ticket)
            {
                ticket.Id = Tickets.Count + 1;
                ticket.RowVersion = Version.ToArray();
                Tickets.Add(ticket);
            }
            else if (entity is AssetStatusHistory history)
            {
                Histories.Add(history);
            }
            else throw new NotSupportedException();
        }

        public void ExpectVersion<T>(T entity, byte[] version) where T : class
        {
            Assert.IsType<MaintenanceTicket>(entity);
            Assert.Equal(Version, version);
            ExpectedVersions++;
        }

        public Task SaveAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
        public Task LockAsync(string resource, CancellationToken ct) { Operations.Add("lock:" + resource); return Task.CompletedTask; }
        public Task LockAssetAsync(long id, CancellationToken ct) { Operations.Add("asset:" + id); return Task.CompletedTask; }
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) => throw new NotSupportedException();
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public int Calls { get; private set; }
        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) { Calls++; return work(); }
    }

    private sealed class Actor(long? id = 9, params string[] permissions) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId => Guid.Parse("f50bde27-9689-4f34-ae32-57fb3bfa7b3b");
        public string? Method => "POST";
        public string? Path => "/api/v1/asset-maintenance";
        public bool Has(string permission) => permissions.Contains(permission);
    }

    private sealed record Event(string Action, object? Entity, long? ActorId, string ActorType, object? Before, object? After);

    private sealed class Audit : IAuditWriter
    {
        public readonly List<Event> Events = [];
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null) =>
            Events.Add(new(action, entity, actorId, actorType, before, after));
    }

    private sealed class Fixture
    {
        public readonly Repository Repo = new();
        public readonly UnitOfWork Uow = new();
        public readonly Audit Audit = new();
        public MaintenanceService Service { get; }

        public Fixture(Actor? actor = null) => Service = new(Repo, Uow, actor ?? new(9,
            Permissions.MaintenanceRead, Permissions.MaintenanceCreate, Permissions.MaintenanceComplete), Audit);

        public MaintenanceTicket ActiveTicket()
        {
            Repo.Assets[0].CurrentStatus = "MAINTENANCE";
            var ticket = new MaintenanceTicket
            {
                Id = 1,
                AssetId = 1,
                TicketCode = "MT-1-1000",
                Title = "Sửa nguồn",
                Description = "Hỏng nguồn máy bàn",
                Status = "IN_PROGRESS",
                RequestedByUserId = 9,
                OpenedAtUtc = StartedAt,
                StartedAtUtc = StartedAt,
                RowVersion = Version.ToArray()
            };
            Repo.Tickets.Add(ticket);
            return ticket;
        }
    }

    [Fact]
    public async Task CreateAsync_WhenAssetInStock_TransitionsToMaintenanceAndLogsHistory()
    {
        var f = new Fixture();
        var req = new CreateMaintenanceRequest(1, "Thay bàn phím hỏng", 300000m);

        var result = await f.Service.CreateAsync(req, CancellationToken.None);

        Assert.Equal(1, result.Id);
        Assert.Equal("IN_PROGRESS", result.Status);
        Assert.Equal("MAINTENANCE", f.Repo.Assets[0].CurrentStatus);
        Assert.Contains("asset:1", f.Repo.Operations);
        Assert.Single(f.Repo.Tickets);
        Assert.Single(f.Repo.Histories);

        var history = f.Repo.Histories[0];
        Assert.Equal("IN_STOCK", history.FromStatus);
        Assert.Equal("MAINTENANCE", history.ToStatus);
        Assert.Equal("MAINTENANCE", history.Source);

        var evt = Assert.Single(f.Audit.Events);
        Assert.Equal("MAINTENANCE_CREATE", evt.Action);
    }

    [Theory]
    [InlineData("IN_USE")]
    [InlineData("MAINTENANCE")]
    public async Task CreateAsync_WhenAssetNotInStock_ThrowsConflict(string currentStatus)
    {
        var f = new Fixture();
        f.Repo.Assets[0].CurrentStatus = currentStatus;
        var req = new CreateMaintenanceRequest(1, "Bảo trì định kỳ", null);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(req, CancellationToken.None));
        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task CreateAsync_WithoutPermission_ThrowsForbidden()
    {
        var actor = new Actor(9, Permissions.MaintenanceRead); // Thiếu MaintenanceCreate
        var f = new Fixture(actor);
        var req = new CreateMaintenanceRequest(1, "Sửa màn hình", null);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(req, CancellationToken.None));
        Assert.Equal(403, ex.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenValid_TransitionsBackToInStockAndClosesTicket()
    {
        var f = new Fixture();
        f.ActiveTicket();
        var req = new CompleteMaintenanceRequest("Đã thay mới bộ nguồn", 450000m, EncodedVersion);

        var result = await f.Service.CompleteAsync(1, req, CancellationToken.None);

        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal("IN_STOCK", f.Repo.Assets[0].CurrentStatus);
        Assert.Equal(450000m, f.Repo.Tickets[0].ActualCost);
        Assert.NotNull(f.Repo.Tickets[0].ResolvedAtUtc);
        Assert.Contains("asset:1", f.Repo.Operations);
        Assert.Single(f.Repo.Histories);

        var history = f.Repo.Histories[0];
        Assert.Equal("MAINTENANCE", history.FromStatus);
        Assert.Equal("IN_STOCK", history.ToStatus);
        Assert.Equal("MAINTENANCE", history.Source);

        var evt = Assert.Single(f.Audit.Events);
        Assert.Equal("MAINTENANCE_COMPLETE", evt.Action);
    }

    [Fact]
    public async Task CompleteAsync_WhenStaleVersion_ThrowsConcurrencyConflict()
    {
        var f = new Fixture();
        f.ActiveTicket();
        var staleVersion = Convert.ToBase64String(Enumerable.Repeat((byte)99, 16).ToArray());
        var req = new CompleteMaintenanceRequest("Nghiệm thu", 100000m, staleVersion);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CompleteAsync(1, req, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("CONCURRENCY_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompleted_ThrowsConflict()
    {
        var f = new Fixture();
        var ticket = f.ActiveTicket();
        ticket.Status = "COMPLETED";
        ticket.ResolvedAtUtc = DateTime.UtcNow;

        var req = new CompleteMaintenanceRequest("Nghiệm thu lần 2", 100000m, EncodedVersion);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CompleteAsync(1, req, CancellationToken.None));
        Assert.Equal(409, ex.Status);
        Assert.Equal("TICKET_ALREADY_COMPLETED", ex.Code);
    }

    [Fact]
    public async Task ListAsync_FiltersByAssetId()
    {
        var f = new Fixture();
        f.ActiveTicket();
        f.Repo.Tickets.Add(new MaintenanceTicket
        {
            Id = 2,
            AssetId = 99,
            TicketCode = "MT-99",
            Title = "Khác",
            Description = "Khác",
            Status = "IN_PROGRESS",
            OpenedAtUtc = StartedAt,
            RowVersion = Version.ToArray()
        });

        var paged = await f.Service.ListAsync(1, 1, 10, CancellationToken.None);

        Assert.Equal(1, paged.TotalItems);
        Assert.Single(paged.Items);
        Assert.Equal(1, paged.Items[0].AssetId);
    }
}