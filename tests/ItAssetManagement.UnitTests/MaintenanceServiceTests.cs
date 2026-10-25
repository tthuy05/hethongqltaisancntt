using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class MaintenanceServiceTests
{
    private sealed class TicketCodes : IMaintenanceTicketCodeGenerator
    { public Task<string> NextAsync(CancellationToken ct) => Task.FromResult("BT000001"); }
    private static readonly DateTime StartedAt = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Version = Enumerable.Repeat((byte)5, 16).ToArray();
    private static string EncodedVersion => Convert.ToBase64String(Version);

    private sealed class Repository : IRepository
    {
        public readonly List<Asset> Assets = [new() { Id = 1, CurrentStatus = "IN_STOCK", RowVersion = Version.ToArray() }];
        public readonly List<MaintenanceTicket> Tickets = [];
        public readonly List<AssetStatusHistory> Histories = [];
        public readonly List<MaintenanceHistory> TicketHistories = [];
        public readonly List<AssetAssignment> Assignments = [];
        public readonly List<User> Users = [new() { Id = 9, IsActive = true, IsEmailConfirmed = true }];
        public readonly List<Role> Roles = [new() { Id = 91, Code = "TECHNICAL_SUPPORT", IsActive = true }];
        public readonly List<UserRole> Memberships = [new() { UserId = 9, RoleId = 91 }];
        public readonly List<string> Operations = [];
        public int Saves { get; private set; }
        public int ExpectedVersions { get; private set; }

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            if (tracking) Operations.Add("track:" + typeof(T).Name);
            IEnumerable<T> values = typeof(T) == typeof(Asset) ? Assets.Cast<T>() :
                typeof(T) == typeof(User) ? Users.Cast<T>() : typeof(T) == typeof(Role) ? Roles.Cast<T>() : typeof(T) == typeof(UserRole) ? Memberships.Cast<T>() :
                typeof(T) == typeof(MaintenanceTicket) ? Tickets.Cast<T>() :
                typeof(T) == typeof(MaintenanceHistory) ? TicketHistories.Cast<T>() :
                typeof(T) == typeof(AssetAssignment) ? Assignments.Cast<T>() :
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
            else if (entity is MaintenanceHistory ticketHistory)
            { ticketHistory.Id = TicketHistories.Count + 1; TicketHistories.Add(ticketHistory); }
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
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => Task.FromResult(
            Tickets.Any(t => t.AssetId == assetId && !t.IsArchived && t.Status is "PENDING" or "IN_PROGRESS") ||
            Assignments.Any(a => a.AssetId == assetId && a.ReturnedAtUtc == null && !a.IsArchived));
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
        public CurrentUser Current(string[] roles) => new(id ?? 0, "Fixture", "fixture@test.invalid", null, roles, permissions);
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

        public Fixture(Actor? actor = null, string[]? roles = null)
        {
            actor ??= new(9, Permissions.Maintenance);
            Service = new(Repo, Uow, actor, Audit, new MaintenanceAccessPolicy(actor.Current(roles ?? ["ADMIN_IT"])), new TicketCodes());
        }

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
                AssignedToUserId = 9,
                OpenedAtUtc = StartedAt,
                StartedAtUtc = StartedAt,
                RowVersion = Version.ToArray()
            };
            Repo.Tickets.Add(ticket);
            return ticket;
        }
    }

    [Fact]
    public async Task CreateAsync_WhenAssetInStock_OpensPendingWithoutStartingWork()
    {
        var f = new Fixture();
        var req = new CreateMaintenanceRequest(1, "Thay bàn phím hỏng", 300000m);

        var result = await f.Service.CreateAsync(req, CancellationToken.None);

        Assert.Equal(1, result.Id);
        Assert.Equal("PENDING", result.Status);
        Assert.Null(result.StartedAtUtc);
        Assert.Equal("IN_STOCK", f.Repo.Assets[0].CurrentStatus);
        Assert.Contains("asset:1", f.Repo.Operations);
        Assert.Single(f.Repo.Tickets);
        Assert.Empty(f.Repo.Histories);
        var history = Assert.Single(f.Repo.TicketHistories);
        Assert.Equal("CREATED", history.EventType);
        Assert.Equal("PENDING", history.ToStatus);
        Assert.Equal(2, f.Audit.Events.Count);
        Assert.All(f.Audit.Events, e => Assert.Equal("maintenance.create", e.Action));
    }

    [Theory]
    [InlineData("RETIRED")]
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

        Assert.Equal("RESOLVED", result.Status);
        Assert.Equal("IN_STOCK", f.Repo.Assets[0].CurrentStatus);
        Assert.Equal(450000m, f.Repo.Tickets[0].ActualCost);
        Assert.NotNull(f.Repo.Tickets[0].ResolvedAtUtc);
        Assert.Contains("asset:1", f.Repo.Operations);
        Assert.Single(f.Repo.Histories);

        var history = f.Repo.Histories[0];
        Assert.Equal("MAINTENANCE", history.FromStatus);
        Assert.Equal("IN_STOCK", history.ToStatus);
        Assert.Equal("MAINTENANCE", history.Source);

        Assert.Equal("RESOLVED", Assert.Single(f.Repo.TicketHistories).EventType);
        Assert.Equal(3, f.Audit.Events.Count);
        Assert.Equal(1, f.Repo.ExpectedVersions);
        Assert.True(f.Repo.Operations.IndexOf("asset:1") < f.Repo.Operations.IndexOf("track:MaintenanceTicket"));
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
        ticket.Status = "RESOLVED";
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

    [Fact]
    public async Task List_and_detail_scope_technician_and_mask_costs()
    {
        var f = new Fixture(new Actor(9, Permissions.MaintenanceRead), ["TECHNICAL_SUPPORT"]);
        var ticket = f.ActiveTicket(); ticket.ActualCost = 500m; ticket.EstimatedCost = 600m;
        f.Repo.Tickets.Add(new() { Id = 2, AssetId = 1, AssignedToUserId = 10, Status = "PENDING", RowVersion = Version });
        var page = await f.Service.ListAsync(null, 1, 20, default);
        Assert.Single(page.Items); Assert.Null(page.Items[0].ActualCost); Assert.Null(page.Items[0].EstimatedCost);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.GetAsync(2, default));
        Assert.Equal(404, ex.Status);
    }

    [Theory]
    [InlineData("{\"assetId\":1,\"description\":\"test\",\"estimatedCost\":null}")]
    [InlineData("{\"assetId\":1,\"description\":\"test\",\"estimatedCost\":0}")]
    public async Task Create_rejects_explicit_cost_even_null_or_zero_without_financial_grant(string json)
    {
        var request = System.Text.Json.JsonSerializer.Deserialize<CreateMaintenanceRequest>(json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var f = new Fixture(new Actor(9, Permissions.MaintenanceRead, Permissions.MaintenanceCreate), ["TECHNICAL_SUPPORT"]);
        Assert.True(request.HasEstimatedCost);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(request, default));
        Assert.Equal(403, ex.Status); Assert.Equal(0, f.Uow.Calls);
    }

    [Fact]
    public async Task Technician_create_returns_only_created_pending_ticket_without_unscoping_reads()
    {
        var f = new Fixture(new Actor(9, Permissions.MaintenanceRead, Permissions.MaintenanceCreate), ["TECHNICAL_SUPPORT"]);
        var result = await f.Service.CreateAsync(new(1, "Báo sự cố", null), default);
        Assert.Equal("PENDING", result.Status);
        Assert.Empty((await f.Service.ListAsync(null, 1, 20, default)).Items);
    }

    [Theory]
    [InlineData("PENDING", 9)]
    [InlineData("IN_PROGRESS", 0)]
    public async Task Complete_rejects_unstarted_or_unassigned_ticket_without_mutation(string status, int assignee)
    {
        var f = new Fixture(); var ticket = f.ActiveTicket(); ticket.Status = status;
        ticket.AssignedToUserId = assignee == 0 ? null : assignee;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CompleteAsync(1, new("Xong", null, EncodedVersion), default));
        Assert.Equal("MAINTENANCE_NOT_STARTED", ex.Code);
        Assert.Equal(status, ticket.Status); Assert.Empty(f.Audit.Events); Assert.Equal(0, f.Repo.Saves);
    }

    [Fact]
    public async Task Complete_with_active_assignment_restores_in_use_not_in_stock()
    {
        var f = new Fixture(); f.ActiveTicket();
        f.Repo.Assignments.Add(new() { AssetId = 1, ReturnedAtUtc = null });
        await f.Service.CompleteAsync(1, new("Xong", null, EncodedVersion), default);
        Assert.Equal("IN_USE", f.Repo.Assets[0].CurrentStatus);
    }

    [Fact]
    public async Task Complete_without_cost_field_preserves_existing_cost()
    {
        var f = new Fixture(new Actor(9, Permissions.MaintenanceRead, Permissions.MaintenanceResolve), ["TECHNICAL_SUPPORT"]);
        f.ActiveTicket().ActualCost = 123m;
        var result = await f.Service.CompleteAsync(1, new("Xong", null, EncodedVersion), default);
        Assert.Equal(123m, f.Repo.Tickets[0].ActualCost); Assert.Null(result.ActualCost);
    }

    [Fact]
    public async Task Complete_explicit_null_cost_requires_financial_permission()
    {
        var f = new Fixture(new Actor(9, Permissions.MaintenanceRead, Permissions.MaintenanceResolve), ["TECHNICAL_SUPPORT"]);
        f.ActiveTicket();
        var request = new CompleteMaintenanceRequest("Xong", null, EncodedVersion) { ActualCost = null };
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CompleteAsync(1, request, default));
        Assert.Equal(403, ex.Status); Assert.Equal(0, f.Uow.Calls);
    }

    [Fact]
    public async Task Create_blocks_existing_pending_workflow()
    {
        var f = new Fixture(); f.Repo.Tickets.Add(new() { AssetId = 1, Status = "PENDING" });
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(new(1, "Sự cố", null), default));
        Assert.Equal("ASSET_ACTIVE_WORKFLOW", ex.Code); Assert.Equal(0, f.Repo.Saves);
    }

    [Fact]
    public async Task Paging_overflow_is_validation_error()
    {
        var f = new Fixture();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.ListAsync(null, int.MaxValue, 100, default));
        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public async Task Assigned_asset_can_be_opened_assigned_started_and_resolved_without_erasing_assignment()
    {
        var f = new Fixture(); f.Repo.Assets[0].CurrentStatus = "IN_USE";
        f.Repo.Assignments.Add(new() { AssetId = 1, AssignedUserId = 9 });
        var ticket = await f.Service.CreateAsync(new(1, "Repair while assigned", null), default);
        Assert.Equal("IN_USE", f.Repo.Assets[0].CurrentStatus);
        var assigned = await f.Service.AssignAsync(ticket.Id, new() { TechnicianId = 9, Note = "Assign", RowVersion = ticket.RowVersion }, default);
        var started = await f.Service.StartAsync(ticket.Id, new() { Note = "Start", RowVersion = assigned.RowVersion }, default);
        Assert.Equal("MAINTENANCE", f.Repo.Assets[0].CurrentStatus); Assert.Equal("IN_PROGRESS", started.Status);
        var result = await f.Service.CompleteAsync(ticket.Id, new("Repaired", null, started.RowVersion), default);
        Assert.Equal("RESOLVED", result.Status); Assert.Equal("IN_USE", f.Repo.Assets[0].CurrentStatus);
        Assert.Null(f.Repo.Assignments[0].ReturnedAtUtc);
        Assert.Equal(new[] { "CREATED", "ASSIGNED", "STATUS_CHANGED", "RESOLVED" }, f.Repo.TicketHistories.Select(x => x.EventType));
        Assert.Equal(2, f.Repo.Histories.Count);
    }

    [Theory]
    [InlineData("IN_STOCK")] [InlineData("BROKEN")]
    public async Task Cancel_after_start_restores_prior_broken_or_available_state(string original)
    {
        var f = new Fixture(); f.Repo.Assets[0].CurrentStatus = original;
        var ticket = await f.Service.CreateAsync(new(1, "Pending repair", null), default);
        var assigned = await f.Service.AssignAsync(ticket.Id, new() { TechnicianId = 9, Note = "Assign", RowVersion = ticket.RowVersion }, default);
        var started = await f.Service.StartAsync(ticket.Id, new() { Note = "Start", RowVersion = assigned.RowVersion }, default);
        var result = await f.Service.CancelAsync(ticket.Id, new() { Reason = "Stop safely", RowVersion = started.RowVersion }, default);
        Assert.Equal("CANCELLED", result.Status); Assert.Equal(original, f.Repo.Assets[0].CurrentStatus);
        Assert.Equal("CANCELLED", f.Repo.TicketHistories[^1].EventType);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.StartAsync(ticket.Id, new() { Note = "Reopen", RowVersion = result.RowVersion }, default));
    }

    [Fact]
    public async Task Fail_sets_broken_and_cost_correction_is_a_new_event_without_mutating_old_history()
    {
        var f = new Fixture(); var ticket = f.ActiveTicket();
        var failed = await f.Service.FailAsync(ticket.Id, new() { Reason = "Unrepairable", Cost = 12.25m, RowVersion = EncodedVersion }, default);
        Assert.Equal("FAILED", failed.Status); Assert.Equal("BROKEN", f.Repo.Assets[0].CurrentStatus);
        var old = f.Repo.TicketHistories[0]; Assert.Equal(12.25m, old.Cost);
        var updated = await f.Service.UpdateAsync(ticket.Id, new() { ActualCost = 0m, Note = "Correct invoice", RowVersion = EncodedVersion }, default);
        Assert.Equal(0m, updated.ActualCost); Assert.Equal(12.25m, old.Cost);
        Assert.Equal("COST_UPDATED", f.Repo.TicketHistories[^1].EventType);
        var history = await f.Service.HistoryAsync(ticket.Id, 1, 20, null, null, "COST_UPDATED", default);
        Assert.Equal(0m, Assert.Single(history.Items).Cost);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.UpdateAsync(ticket.Id, new() { Title = "Cannot rewrite terminal", Note = "Change", RowVersion = EncodedVersion }, default));
    }

    [Fact]
    public async Task Technician_is_revalidated_and_missing_or_future_start_never_changes_asset()
    {
        var f = new Fixture(); var ticket = await f.Service.CreateAsync(new(1, "Repair", null), default);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.StartAsync(ticket.Id, new() { Note = "No technician", RowVersion = ticket.RowVersion }, default));
        f.Repo.Users[0].IsActive = false;
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.AssignAsync(ticket.Id, new() { TechnicianId = 9, Note = "Inactive", RowVersion = ticket.RowVersion }, default));
        f.Repo.Users[0].IsActive = true;
        await f.Service.AssignAsync(ticket.Id, new() { TechnicianId = 9, Note = "Assign", RowVersion = ticket.RowVersion }, default);
        await Assert.ThrowsAsync<BusinessException>(() => f.Service.StartAsync(ticket.Id, new() { Note = "Future", StartedAtUtc = DateTime.UtcNow.AddDays(1), RowVersion = ticket.RowVersion }, default));
        Assert.Equal("PENDING", f.Repo.Tickets[0].Status); Assert.Equal("IN_STOCK", f.Repo.Assets[0].CurrentStatus);
        Assert.Empty(f.Repo.Histories);
    }

    [Fact]
    public async Task Support_scopes_history_and_cannot_write_even_explicit_null_financial_fields()
    {
        var f = new Fixture(); var ticket = f.ActiveTicket();
        var actor = new Actor(9, Permissions.Maintenance);
        var support = new MaintenanceService(f.Repo, f.Uow, actor, f.Audit, new(actor.Current(["TECHNICAL_SUPPORT"])), new TicketCodes());
        await Assert.ThrowsAsync<BusinessException>(() => support.UpdateAsync(ticket.Id, new() { ActualCost = null, Note = "No financial access", RowVersion = EncodedVersion }, default));
        var error = await Assert.ThrowsAsync<BusinessException>(() => support.AssignAsync(ticket.Id, new() { TechnicianId = 9, Note = "Cannot claim", RowVersion = EncodedVersion }, default));
        Assert.Equal(403, error.Status);
        ticket.AssignedToUserId = 10;
        Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => support.HistoryAsync(ticket.Id, 1, 20, null, null, null, default))).Status);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public async Task Create_rejects_out_of_schema_money(string text)
    {
        var f = new Fixture(); var cost = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.CreateAsync(new(1, "Sự cố", cost), default));
        Assert.Equal(400, ex.Status); Assert.Equal(0, f.Uow.Calls);
    }
}
