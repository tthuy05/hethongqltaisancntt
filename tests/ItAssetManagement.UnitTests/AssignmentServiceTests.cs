using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

// These fakes exercise service decisions only. PostgreSQL atomicity/FK/concurrency
// require the separately isolated integration fixture, not the shared Neon database.
public sealed class AssignmentServiceTests
{
    private static readonly DateTime AssignedAt = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Version = Enumerable.Repeat((byte)7, 16).ToArray();
    private static string EncodedVersion => Convert.ToBase64String(Version);

    private sealed class Repository : IRepository
    {
        public readonly List<Asset> Assets = [new() { Id = 1, CurrentStatus = "IN_STOCK", RowVersion = Version.ToArray() }];
        public readonly List<User> Users = [new() { Id = 2, IsActive = true }];
        public readonly List<Department> Departments = [new() { Id = 3, IsActive = true }];
        public readonly List<AssetAssignment> Assignments = [];
        public readonly List<MaintenanceTicket> Tickets = [];
        public readonly List<AssetStatusHistory> Histories = [];
        public readonly List<string> Operations = [];
        public int Saves { get; private set; }
        public int ExpectedVersions { get; private set; }

        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            if (tracking) Operations.Add("track:" + typeof(T).Name);
            IEnumerable<T> values = typeof(T) == typeof(Asset) ? Assets.Cast<T>() :
                typeof(T) == typeof(User) ? Users.Cast<T>() :
                typeof(T) == typeof(Department) ? Departments.Cast<T>() :
                typeof(T) == typeof(AssetAssignment) ? Assignments.Cast<T>() :
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
            if (entity is AssetAssignment assignment)
            {
                assignment.Id = Assignments.Count + 1; assignment.RowVersion = Version.ToArray(); Assignments.Add(assignment);
            }
            else if (entity is AssetStatusHistory history) Histories.Add(history);
            else throw new NotSupportedException();
        }
        public void ExpectVersion<T>(T entity, byte[] version) where T : class
        {
            Assert.IsType<AssetAssignment>(entity); Assert.Equal(Version, version); ExpectedVersions++;
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
        public string? Path => "/api/v1/asset-assignments";
        public bool Has(string permission) => permissions.Contains(permission);
    }
    private sealed record Event(string Action, object? Entity, long? ActorId, string ActorType, object? Before, object? After);
    private sealed class Audit : IAuditWriter
    {
        public readonly List<Event> Events = [];
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS",
            object? before = null, object? after = null, string? failureCode = null) => Events.Add(new(action, entity, actorId, actorType, before, after));
    }
    private sealed class Fixture
    {
        public readonly Repository Repo = new();
        public readonly UnitOfWork Uow = new();
        public readonly Audit Audit = new();
        public AssignmentService Service { get; }
        public Fixture(Actor? actor = null) => Service = new(Repo, Uow, Audit, actor ?? new(9,
            Permissions.AssignmentRead, Permissions.AssignmentAssign, Permissions.AssignmentReturn));
        public AssetAssignment Active()
        {
            Repo.Assets[0].CurrentStatus = "IN_USE";
            var assignment = new AssetAssignment
            {
                Id = 1, AssetId = 1, AssignedUserId = 2, AssignedByUserId = 9,
                AssignedAtUtc = AssignedAt, RowVersion = Version.ToArray(), AssignmentNote = "Initial handoff",
            };
            Repo.Assignments.Add(assignment); return assignment;
        }
    }
    private static AssignAssetRequest Assign() => new() { AssetId = 1, UserId = 2, AssignedAtUtc = AssignedAt };
    private static ReturnAssetRequest Return() => new() { Note = "  Đã thu hồi  ", RowVersion = EncodedVersion, ReturnedAtUtc = AssignedAt.AddDays(1) };
    private static Task<AssignmentDto> ReturnAsync(Fixture fixture, ReturnAssetRequest? request = null) => fixture.Service.ReturnAsync(1, request ?? Return(), default);
    private static async Task<BusinessException> Error(Task task, int status, string? code = null)
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => task);
        Assert.Equal(status, error.Status); if (code != null) Assert.Equal(code, error.Code); return error;
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("assign")]
    [InlineData("return")]
    public async Task Every_operation_enforces_its_permission_before_repository_access(string operation)
    {
        var fixture = new Fixture(new(9));
        Task task = operation switch
        {
            "list" => fixture.Service.ListAsync(null, null, null, false, 1, 20, default),
            "get" => fixture.Service.GetAsync(1, default),
            "assign" => fixture.Service.AssignAsync(Assign(), default),
            _ => ReturnAsync(fixture),
        };
        await Error(task, 403, "FORBIDDEN"); Assert.Empty(fixture.Repo.Operations); Assert.Equal(0, fixture.Uow.Calls);
    }
    [Theory]
    [InlineData(null)] [InlineData(0L)] [InlineData(-1L)]
    public async Task A_permission_does_not_replace_a_valid_authenticated_actor(long? id)
    {
        var fixture = new Fixture(new(id, Permissions.AssignmentAssign));
        await Error(fixture.Service.AssignAsync(Assign(), default), 403, "FORBIDDEN"); Assert.Equal(0, fixture.Uow.Calls);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public async Task Assignment_detail_requires_positive_id(long id)
    { await Error(new Fixture().Service.GetAsync(id, default), 400, "VALIDATION_ERROR"); }
    [Fact]
    public async Task Detail_does_not_expose_archived_assignment()
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); assignment.ReturnedAtUtc = AssignedAt.AddDays(1); assignment.IsArchived = true;
        await Error(fixture.Service.GetAsync(1, default), 404, "ASSIGNMENT_NOT_FOUND");
    }
    [Theory]
    [InlineData(0, 20)] [InlineData(1, 0)] [InlineData(1, 101)] [InlineData(int.MaxValue, 100)]
    public async Task List_rejects_invalid_page_and_overflow(int page, int pageSize)
    { await Error(new Fixture().Service.ListAsync(null, null, null, false, page, pageSize, default), 400); }
    [Theory]
    [InlineData(0L, null, null)] [InlineData(null, 0L, null)] [InlineData(null, null, -1L)]
    public async Task List_filters_require_positive_ids(long? asset, long? user, long? department)
    { await Error(new Fixture().Service.ListAsync(asset, user, department, false, 1, 20, default), 400); }
    [Fact]
    public async Task List_filters_and_pages_stably_without_removing_closed_history()
    {
        var fixture = new Fixture(); fixture.Active();
        fixture.Repo.Assignments.Add(new() { Id = 2, AssetId = 1, AssignedUserId = 2, AssignedAtUtc = AssignedAt,
            ReturnedAtUtc = AssignedAt.AddDays(1), RowVersion = Version.ToArray() });
        fixture.Repo.Assignments.Add(new() { Id = 3, AssetId = 2, AssignedDepartmentId = 3, AssignedAtUtc = AssignedAt, RowVersion = Version.ToArray() });
        fixture.Repo.Assignments.Add(new() { Id = 4, AssetId = 1, AssignedUserId = 2, AssignedAtUtc = AssignedAt,
            ReturnedAtUtc = AssignedAt.AddDays(1), IsArchived = true, RowVersion = Version.ToArray() });
        var result = await fixture.Service.ListAsync(1, 2, null, false, 1, 1, default);
        Assert.Equal(2, result.TotalItems); Assert.Equal(2, result.TotalPages); Assert.Equal(2, Assert.Single(result.Items).Id);
        Assert.Equal(1, Assert.Single((await fixture.Service.ListAsync(1, 2, null, true, 1, 20, default)).Items).Id);
        Assert.Equal(3, Assert.Single((await fixture.Service.ListAsync(null, null, 3, false, 1, 20, default)).Items).Id);
    }
    [Theory]
    [InlineData(null, null)] [InlineData(2L, 3L)] [InlineData(0L, null)] [InlineData(null, -1L)]
    public async Task Assign_requires_one_positive_target(long? userId, long? departmentId)
    {
        var fixture = new Fixture(); var request = Assign(); request.UserId = userId; request.DepartmentId = departmentId;
        await Error(fixture.Service.AssignAsync(request, default), 400); Assert.Equal(0, fixture.Uow.Calls);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public async Task Assign_requires_positive_asset_id(long id)
    { var request = Assign(); request.AssetId = id; await Error(new Fixture().Service.AssignAsync(request, default), 400); }
    [Fact]
    public async Task Assign_rejects_oversized_note_before_write()
    {
        var fixture = new Fixture(); var request = Assign(); request.Note = new string('x', 1001);
        await Error(fixture.Service.AssignAsync(request, default), 400); Assert.Equal(0, fixture.Uow.Calls);
    }
    [Theory]
    [InlineData(DateTimeKind.Unspecified)] [InlineData(DateTimeKind.Local)]
    public async Task Commands_reject_non_UTC_business_timestamps(DateTimeKind kind)
    {
        var fixture = new Fixture(); fixture.Active(); var assign = Assign(); assign.AssignedAtUtc = DateTime.SpecifyKind(AssignedAt, kind);
        var returned = Return(); returned.ReturnedAtUtc = DateTime.SpecifyKind(AssignedAt, kind);
        await Error(fixture.Service.AssignAsync(assign, default), 400); await Error(ReturnAsync(fixture, returned), 400);
        Assert.Equal(0, fixture.Uow.Calls);
    }
    [Theory]
    [InlineData(true, false)] [InlineData(true, true)] [InlineData(false, false)] [InlineData(false, true)]
    public async Task Assign_checks_selected_target_exists_and_is_active(bool userTarget, bool missing)
    {
        var fixture = new Fixture(); var request = Assign();
        if (userTarget) { if (missing) fixture.Repo.Users.Clear(); else fixture.Repo.Users[0].IsActive = false; }
        else
        {
            request.UserId = null; request.DepartmentId = 3;
            if (missing) fixture.Repo.Departments.Clear(); else fixture.Repo.Departments[0].IsActive = false;
        }
        var error = await Error(fixture.Service.AssignAsync(request, default), 400);
        Assert.Contains(userTarget ? "userId" : "departmentId", error.Errors!.Keys);
        Assert.Empty(fixture.Repo.Assignments); Assert.Empty(fixture.Repo.Histories); Assert.Empty(fixture.Audit.Events);
        Assert.Equal("IN_STOCK", fixture.Repo.Assets[0].CurrentStatus);
    }
    [Theory]
    [InlineData("IN_USE")] [InlineData("MAINTENANCE")] [InlineData("BROKEN")] [InlineData("RETIRED")]
    public async Task Assign_rejects_non_stock_asset_without_side_effects(string status)
    {
        var fixture = new Fixture(); fixture.Repo.Assets[0].CurrentStatus = status;
        await Error(fixture.Service.AssignAsync(Assign(), default), 409, "ASSET_NOT_IN_STOCK");
        Assert.Empty(fixture.Repo.Assignments); Assert.Empty(fixture.Audit.Events); Assert.Empty(fixture.Repo.Histories);
    }
    [Fact]
    public async Task Assign_rejects_archived_and_missing_assets()
    {
        var fixture = new Fixture(); fixture.Repo.Assets[0].IsArchived = true;
        await Error(fixture.Service.AssignAsync(Assign(), default), 409, "ASSET_ARCHIVED");
        fixture.Repo.Assets.Clear(); await Error(fixture.Service.AssignAsync(Assign(), default), 404, "ASSET_NOT_FOUND");
    }
    [Fact]
    public async Task Assign_rejects_existing_open_assignment_even_if_asset_state_is_stock()
    {
        var fixture = new Fixture(); fixture.Active(); fixture.Repo.Assets[0].CurrentStatus = "IN_STOCK";
        await Error(fixture.Service.AssignAsync(Assign(), default), 409, "ASSET_ALREADY_ASSIGNED");
        Assert.Single(fixture.Repo.Assignments); Assert.Empty(fixture.Audit.Events);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Valid_assign_uses_parent_lock_history_and_audits_each_business_entity(bool userTarget)
    {
        var fixture = new Fixture(new(9, Permissions.AssignmentAssign)); var request = Assign(); request.Note = "  Handoff note  ";
        if (!userTarget) { request.UserId = null; request.DepartmentId = 3; }
        // A pending ticket does not invent a new assign prohibition.
        fixture.Repo.Tickets.Add(new() { Id = 1, AssetId = 1, Status = "PENDING" });
        var result = await fixture.Service.AssignAsync(request, default); var assignment = Assert.Single(fixture.Repo.Assignments);
        Assert.Equal(userTarget ? 2L : null, assignment.AssignedUserId); Assert.Equal(userTarget ? null : 3L, assignment.AssignedDepartmentId);
        Assert.Equal(9, assignment.AssignedByUserId); Assert.Equal(AssignedAt, result.AssignedAtUtc); Assert.Equal("Handoff note", result.AssignmentNote);
        Assert.Equal("IN_USE", fixture.Repo.Assets[0].CurrentStatus); Assert.Equal(9, fixture.Repo.Assets[0].UpdatedByUserId);
        Assert.Equal(new[] { "asset:1", "track:Asset", "lock:master-data", "lock:users-identity" }, fixture.Repo.Operations);
        var history = Assert.Single(fixture.Repo.Histories); Assert.Equal("IN_STOCK", history.FromStatus); Assert.Equal("IN_USE", history.ToStatus);
        Assert.Equal("ASSIGNMENT", history.Source); Assert.Equal("Handoff note", history.Reason); Assert.Equal(AssignedAt, history.ChangedAtUtc);
        Assert.NotEqual(Guid.Empty, history.CorrelationId); Assert.Equal(2, fixture.Repo.Saves); Assert.Equal(1, fixture.Uow.Calls);
        AssertAuditPair(fixture, "assignments.assign", assignment);
        Assert.DoesNotContain("Handoff note", JsonSerializer.Serialize(fixture.Audit.Events.Select(e => e.After)));
    }
    [Fact]
    public async Task Default_assign_time_is_UTC_and_blank_note_is_absent()
    {
        var fixture = new Fixture(); var request = Assign(); request.AssignedAtUtc = null; request.Note = "   ";
        var result = await fixture.Service.AssignAsync(request, default);
        Assert.Equal(DateTimeKind.Utc, result.AssignedAtUtc.Kind); Assert.Null(result.AssignmentNote);
        Assert.Equal("Cấp phát tài sản", Assert.Single(fixture.Repo.Histories).Reason);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("bad-base64")]
    public async Task Return_requires_a_canonical_16_byte_version(string? version)
    {
        var fixture = new Fixture(); fixture.Active(); var request = Return(); request.RowVersion = version;
        await Error(ReturnAsync(fixture, request), 400); Assert.Equal(0, fixture.Uow.Calls);
    }
    [Theory]
    [InlineData(null)] [InlineData(" ")]
    public async Task Return_requires_a_reason(string? reason)
    {
        var fixture = new Fixture(); fixture.Active(); var request = Return(); request.Note = reason;
        await Error(ReturnAsync(fixture, request), 400); Assert.Equal(0, fixture.Uow.Calls);
    }
    [Fact]
    public async Task Return_rejects_oversized_note()
    {
        var request = Return(); request.Note = new string('x', 1001);
        await Error(ReturnAsync(new Fixture(), request), 400);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public async Task Return_requires_positive_assignment_id(long id)
    { await Error(new Fixture().Service.ReturnAsync(id, Return(), default), 400); }
    [Fact]
    public async Task Return_rejects_missing_assignment_and_missing_parent()
    {
        var fixture = new Fixture(); await Error(ReturnAsync(fixture), 404, "ASSIGNMENT_NOT_FOUND");
        fixture.Active(); fixture.Repo.Assets.Clear(); await Error(ReturnAsync(fixture), 500, "DATA_INCONSISTENT");
        Assert.Empty(fixture.Audit.Events);
    }
    [Fact]
    public async Task Stale_return_is_rejected_before_touching_any_state()
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); var request = Return(); request.RowVersion = Convert.ToBase64String(new byte[16]);
        await Error(ReturnAsync(fixture, request), 409, "CONCURRENCY_CONFLICT");
        Assert.Null(assignment.ReturnedAtUtc); Assert.Equal("IN_USE", fixture.Repo.Assets[0].CurrentStatus);
        Assert.Equal(0, fixture.Repo.Saves); Assert.Equal(0, fixture.Repo.ExpectedVersions); Assert.Empty(fixture.Audit.Events); Assert.Empty(fixture.Repo.Histories);
    }
    [Fact]
    public async Task Closed_return_never_overwrites_history()
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); assignment.ReturnedAtUtc = AssignedAt.AddHours(1); assignment.ReturnNote = "Original reason";
        await Error(ReturnAsync(fixture), 409, "ASSIGNMENT_ALREADY_RETURNED");
        Assert.Equal("Original reason", assignment.ReturnNote); Assert.Equal(AssignedAt.AddHours(1), assignment.ReturnedAtUtc);
        Assert.Equal(0, fixture.Repo.Saves); Assert.Empty(fixture.Audit.Events);
    }
    [Fact]
    public async Task Return_date_cannot_precede_assignment()
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); var request = Return(); request.ReturnedAtUtc = AssignedAt.AddSeconds(-1);
        await Error(ReturnAsync(fixture, request), 400); Assert.Null(assignment.ReturnedAtUtc); Assert.Equal(0, fixture.Repo.ExpectedVersions);
    }
    [Theory]
    [InlineData("IN_STOCK")] [InlineData("RETIRED")]
    public async Task Return_rejects_inconsistent_asset_state(string status)
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); fixture.Repo.Assets[0].CurrentStatus = status;
        await Error(ReturnAsync(fixture), 409, "ASSET_STATUS_CONFLICT"); Assert.Null(assignment.ReturnedAtUtc); Assert.Equal(0, fixture.Repo.Saves);
    }
    [Fact]
    public async Task Return_rejects_archived_asset()
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); fixture.Repo.Assets[0].IsArchived = true;
        await Error(ReturnAsync(fixture), 409, "ASSET_STATUS_CONFLICT"); Assert.Null(assignment.ReturnedAtUtc);
    }
    [Theory]
    [InlineData("MAINTENANCE", "PENDING", false)]
    [InlineData("MAINTENANCE", "IN_PROGRESS", true)]
    [InlineData("IN_USE", "IN_PROGRESS", false)]
    public async Task Return_rejects_inconsistent_maintenance_state(string assetStatus, string ticketStatus, bool archived)
    {
        var fixture = new Fixture(); var assignment = fixture.Active(); fixture.Repo.Assets[0].CurrentStatus = assetStatus;
        fixture.Repo.Tickets.Add(new() { AssetId = 1, Status = ticketStatus, IsArchived = archived });
        await Error(ReturnAsync(fixture), 409, "ASSET_STATUS_CONFLICT"); Assert.Null(assignment.ReturnedAtUtc); Assert.Empty(fixture.Audit.Events);
    }
    [Theory]
    [InlineData("IN_USE", false, "IN_STOCK", 1)]
    [InlineData("MAINTENANCE", true, "MAINTENANCE", 0)]
    [InlineData("BROKEN", false, "BROKEN", 0)]
    public async Task Valid_return_closes_only_assignment_and_preserves_workflow_status(string status, bool inProgress, string expected, int historyCount)
    {
        var fixture = new Fixture(new(9, Permissions.AssignmentReturn)); var assignment = fixture.Active(); fixture.Repo.Assets[0].CurrentStatus = status;
        if (inProgress) fixture.Repo.Tickets.Add(new() { AssetId = 1, Status = "IN_PROGRESS" });
        var result = await ReturnAsync(fixture);
        Assert.Equal(AssignedAt.AddDays(1), result.ReturnedAtUtc); Assert.Equal("Đã thu hồi", result.ReturnNote);
        Assert.Equal(9, assignment.ReturnedByUserId); Assert.Equal(expected, fixture.Repo.Assets[0].CurrentStatus);
        Assert.Equal(historyCount, fixture.Repo.Histories.Count); Assert.Equal(1, fixture.Repo.ExpectedVersions); Assert.Equal(2, fixture.Repo.Saves);
        Assert.Equal("asset:1", fixture.Repo.Operations[0]); Assert.True(fixture.Repo.Operations.IndexOf("track:AssetAssignment") > fixture.Repo.Operations.IndexOf("asset:1"));
        if (historyCount == 1)
        {
            var history = Assert.Single(fixture.Repo.Histories); Assert.Equal(status, history.FromStatus); Assert.Equal(expected, history.ToStatus);
            Assert.Equal(result.ReturnedAtUtc, history.ChangedAtUtc); Assert.Equal("Đã thu hồi", history.Reason); Assert.Equal(9, fixture.Repo.Assets[0].UpdatedByUserId);
        }
        AssertAuditPair(fixture, "assignments.return", assignment);
        Assert.DoesNotContain("Đã thu hồi", JsonSerializer.Serialize(fixture.Audit.Events.Select(e => e.After)));
    }
    [Fact]
    public async Task Default_return_timestamp_is_UTC()
    {
        var fixture = new Fixture(); fixture.Active(); var request = Return(); request.ReturnedAtUtc = null;
        Assert.Equal(DateTimeKind.Utc, (await ReturnAsync(fixture, request)).ReturnedAtUtc!.Value.Kind);
    }
    [Fact]
    public void Business_timestamp_JSON_uses_the_documented_names()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var assign = JsonSerializer.Deserialize<AssignAssetRequest>("{\"assetId\":1,\"userId\":2,\"assignedAt\":\"2026-10-01T08:00:00Z\"}", options)!;
        var returned = JsonSerializer.Deserialize<ReturnAssetRequest>("{\"returnedAt\":\"2026-10-02T08:00:00Z\"}", options)!;
        Assert.Equal(AssignedAt, assign.AssignedAtUtc); Assert.Equal(DateTimeKind.Utc, returned.ReturnedAtUtc!.Value.Kind);
        var json = JsonSerializer.Serialize(new AssignmentDto { AssignedAtUtc = AssignedAt, ReturnedAtUtc = AssignedAt.AddDays(1) }, options);
        Assert.Contains("\"assignedAt\":", json); Assert.Contains("\"returnedAt\":", json); Assert.DoesNotContain("AtUtc", json);
    }
    private static void AssertAuditPair(Fixture fixture, string action, AssetAssignment assignment)
    {
        Assert.Equal(2, fixture.Audit.Events.Count);
        Assert.Contains(fixture.Audit.Events, e => ReferenceEquals(e.Entity, assignment));
        Assert.Contains(fixture.Audit.Events, e => ReferenceEquals(e.Entity, fixture.Repo.Assets[0]));
        Assert.All(fixture.Audit.Events, e => { Assert.Equal(action, e.Action); Assert.Equal(9, e.ActorId); Assert.Equal("USER", e.ActorType); });
    }
}
