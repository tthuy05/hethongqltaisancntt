using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class AuditLogTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void Permission_is_admin_only_without_duplicate_catalog_entries()
    {
        Assert.Equal("audit-logs.read", Permissions.AuditRead); Assert.Contains(Permissions.AuditRead, Permissions.Admin);
        Assert.DoesNotContain(Permissions.AuditRead, Permissions.Read); Assert.DoesNotContain(Permissions.AuditRead, Permissions.Operate);
        Assert.Equal(25, Permissions.All.Distinct().Count()); Assert.Equal(Permissions.All.Length, Permissions.All.Distinct().Count());
    }
    [Fact]
    public void Date_defaults_and_explicit_offsets_use_half_open_utc_window()
    {
        var defaults = new AuditLogListQuery(); defaults.Validate(Now);
        Assert.Equal(Now, defaults.ToUtc); Assert.Equal(Now.AddDays(-7), defaults.FromUtc);
        var explicitOffset = new AuditLogListQuery { From = "2026-10-04T07:00:00+07:00", To = "2026-10-05T00:00Z" };
        explicitOffset.Validate(Now); Assert.Equal(Now.AddDays(-1), explicitOffset.FromUtc); Assert.Equal(Now, explicitOffset.ToUtc);
        var onlyTo = new AuditLogListQuery { To = "2026-10-04T00:00:00Z" }; onlyTo.Validate(Now);
        Assert.Equal(Now.AddDays(-8), onlyTo.FromUtc);
    }
    [Theory]
    [InlineData("", "2026-10-05T00:00:00Z")]
    [InlineData(" ", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-04", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-04T00:00:00", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-04 00:00:00Z", "2026-10-05T00:00:00Z")]
    [InlineData("2026-02-30T00:00:00Z", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-05T00:00:00Z", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-06T00:00:00Z", "2026-10-05T00:00:00Z")]
    [InlineData("2026-09-03T00:00:00Z", "2026-10-05T00:00:00Z")]
    [InlineData("2026-10-04T00:00:00Z", "")]
    [InlineData(null, "0001-01-01T00:00:00Z")]
    public void Invalid_time_windows_fail_without_culture_dependent_parsing(string? from, string to) =>
        Assert.Equal(400, Assert.Throws<BusinessException>(() => new AuditLogListQuery { From = from, To = to }.Validate(Now)).Status);
    [Theory]
    [InlineData(0, 20, "occurredAt", "desc", null)] [InlineData(1, 0, "occurredAt", "desc", null)]
    [InlineData(1, 101, "occurredAt", "desc", null)] [InlineData(int.MaxValue, 100, "occurredAt", "desc", null)]
    [InlineData(1, 20, "action", "desc", null)] [InlineData(1, 20, "occurredAt", "DESC", null)]
    [InlineData(1, 20, "occurredAt", "desc", "success")] [InlineData(1, 20, "occurredAt", "desc", "")]
    public void Invalid_paging_sort_and_outcome_fail(int page, int size, string sort, string direction, string? outcome) =>
        Assert.Equal(400, Assert.Throws<BusinessException>(() => new AuditLogListQuery
            { Page = page, PageSize = size, SortBy = sort, SortDirection = direction, Outcome = outcome }.Validate(Now)).Status);
    [Fact]
    public void Ids_and_exact_text_filters_are_bounded_and_trimmed()
    {
        Assert.Throws<BusinessException>(() => new AuditLogListQuery { UserId = 0 }.Validate(Now));
        Assert.Throws<BusinessException>(() => new AuditLogListQuery { CorrelationId = Guid.Empty }.Validate(Now));
        Assert.Throws<BusinessException>(() => new AuditLogListQuery { Action = " " }.Validate(Now));
        Assert.Throws<BusinessException>(() => new AuditLogListQuery { Action = new string('x', 151) }.Validate(Now));
        Assert.Throws<BusinessException>(() => new AuditLogListQuery { EntityType = new string('x', 101) }.Validate(Now));
        Assert.Throws<BusinessException>(() => new AuditLogListQuery { EntityId = new string('x', 101) }.Validate(Now));
        var query = new AuditLogListQuery { Action = " asset.update ", EntityType = " Asset ", EntityId = " 12 " };
        query.Validate(Now); Assert.Equal("asset.update", query.Action); Assert.Equal("Asset", query.EntityType); Assert.Equal("12", query.EntityId);
    }
    [Fact]
    public async Task Missing_permission_is_denied_before_accessing_repository_or_unit_of_work()
    {
        var service = new AuditLogService(null!, new Actor(), null!, null!);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(new(), default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(1, default))).Status);
    }
    [Theory]
    [InlineData("SYSTEM_MANAGER", true)] [InlineData("TECHNICAL_SUPPORT", true)] [InlineData("ADMIN_IT", false)]
    public async Task Misgranted_permission_cannot_bypass_active_admin_membership(string roleCode, bool active)
    {
        var repo = new Repository(roleCode, active); var audit = new Writer(repo); var service = Service(repo, audit);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(Window(), default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(1, default))).Status);
        Assert.Empty(audit.Events); Assert.Equal(0, repo.Saves);
    }
    [Theory]
    [InlineData("occurredAt", "asc", 1, 2)] [InlineData("occurredAt", "desc", 3, 2)]
    [InlineData("id", "asc", 1, 2)] [InlineData("id", "desc", 3, 2)]
    public async Task Half_open_paging_is_stable_and_view_marker_is_not_in_returned_page(string sort, string direction, long first, long second)
    {
        var repo = Data(); var writer = new Writer(repo); var query = Window(); query.PageSize = 2; query.SortBy = sort; query.SortDirection = direction;
        var page = await Service(repo, writer).ListAsync(query, default);
        Assert.Equal(3, page.TotalItems); Assert.Equal(2, page.TotalPages); Assert.Equal(new[] { first, second }, page.Items.Select(x => x.Id));
        Assert.DoesNotContain(page.Items, x => x.Action == "audit.view");
        Assert.Equal("audit.view", Assert.Single(writer.Events)); Assert.Equal(1, repo.Saves);
        var marker = repo.Logs.Last(); Assert.Equal(1, marker.ActorUserId); Assert.Equal("USER", marker.ActorType);
        var fields = JsonSerializer.SerializeToElement(writer.After);
        Assert.Equal(new[] { "page", "pageSize", "returnedCount" }, fields.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(2, fields.GetProperty("returnedCount").GetInt32()); Assert.False(fields.TryGetProperty("items", out _));
    }
    [Fact]
    public async Task Filters_are_combined_exactly_and_summary_never_contains_snapshot_or_request_fields()
    {
        var repo = Data(); var writer = new Writer(repo); var query = Window(); query.UserId = 1; query.Action = " asset.update ";
        query.EntityType = "Asset"; query.EntityId = "12"; query.Outcome = "SUCCESS"; query.CorrelationId = repo.Logs[1].CorrelationId;
        var summary = Assert.Single((await Service(repo, writer).ListAsync(query, default)).Items); Assert.Equal(2, summary.Id);
        var json = JsonSerializer.SerializeToElement(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "action", "actorType", "actorUserId", "correlationId", "entityId", "entityType", "id", "occurredAt", "outcome" },
            json.EnumerateObject().Select(x => x.Name).Order());
        query.Action = "Asset.Update"; Assert.Empty((await Service(repo, writer).ListAsync(query, default)).Items);
    }
    [Fact]
    public async Task Out_of_range_page_is_empty_and_successful_read_is_still_audited()
    {
        var repo = Data(); var writer = new Writer(repo); var query = Window(); query.Page = 99;
        var result = await Service(repo, writer).ListAsync(query, default);
        Assert.Empty(result.Items); Assert.Equal(3, result.TotalItems); Assert.Single(writer.Events);
    }
    [Fact]
    public async Task Detail_masks_cost_and_historic_unsafe_fields_and_appends_one_view()
    {
        var repo = Data(); var writer = new Writer(repo);
        repo.Logs[0].OldValuesJson = "{\"CurrentStatus\":\"IN_STOCK\",\"PurchaseCost\":125.50,\"Name\":\"hidden\",\"Password\":\"hidden\"}";
        repo.Logs[0].NewValuesJson = "{\"currentStatus\":\"IN_USE\",\"owningDepartmentId\":3}";
        var detail = await Service(repo, writer).GetAsync(1, default);
        Assert.True(detail.SnapshotsRedacted); Assert.Equal(new[] { "currentStatus" }, detail.OldValues!.Keys);
        Assert.Equal("IN_USE", detail.NewValues!["currentStatus"].GetString()); Assert.Single(writer.Events); Assert.Equal(1, repo.Saves);
        Assert.Equal(1, JsonSerializer.SerializeToElement(writer.After).GetProperty("auditLogId").GetInt64());
        var json = JsonSerializer.SerializeToElement(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(json.TryGetProperty("requestPath", out _)); Assert.False(json.TryGetProperty("metadataJson", out _));
        Assert.False(json.TryGetProperty("failureReasonCode", out _)); Assert.Equal(12, json.EnumerateObject().Count());
    }
    [Fact]
    public async Task Invalid_or_missing_detail_does_not_append_a_success_marker()
    {
        var repo = Data(); var writer = new Writer(repo); var service = Service(repo, writer);
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(0, default))).Status);
        Assert.Equal("AUDIT_LOG_NOT_FOUND", (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(999, default))).Code);
        var query = Window(); query.PageSize = 0;
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(query, default))).Status);
        Assert.Empty(writer.Events); Assert.Equal(0, repo.Saves);
    }
    [Fact]
    public async Task Audit_save_failure_prevents_a_successful_list_or_detail_response()
    {
        var repo = Data(); repo.FailSave = true; var writer = new Writer(repo); var service = Service(repo, writer);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync(Window(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(1, default));
    }
    [Theory]
    [InlineData("[]")] [InlineData("null")] [InlineData("{broken}")]
    [InlineData("{\"isActive\":true,\"IsActive\":false}")]
    [InlineData("{\"unknown\":1,\"UNKNOWN\":2,\"isActive\":true}")]
    public void Invalid_or_ambiguous_snapshot_fails_closed(string json)
    {
        var result = AuditSnapshot.Read(json, true); Assert.True(result.Redacted); Assert.Empty(result.Values!);
    }
    [Fact]
    public void Null_and_oversize_utf8_snapshot_are_distinguished()
    {
        var missing = AuditSnapshot.Read(null, true); Assert.Null(missing.Values); Assert.False(missing.Redacted);
        var large = AuditSnapshot.Read("{\"name\":\"" + new string('界', 6000) + "\",\"isActive\":true}", true);
        Assert.Empty(large.Values!); Assert.True(large.Redacted);
    }
    [Fact]
    public void Safe_snapshot_is_typed_canonical_and_preserves_exact_numeric_cost()
    {
        const string json = "{\"AssetTypeId\":2,\"parentDepartmentId\":null,\"defaultUsefulLifeMonths\":null,\"roleCount\":3,\"IsActive\":true,\"currentStatus\":\"IN_STOCK\",\"PurchaseCost\":9999999999999999.99,\"page\":1,\"pageSize\":100,\"returnedCount\":0,\"auditLogId\":11}";
        var allowed = AuditSnapshot.Read(json, true); Assert.False(allowed.Redacted); Assert.Equal(11, allowed.Values!.Count);
        Assert.Equal(9999999999999999.99m, allowed.Values["purchaseCost"].GetDecimal()); Assert.True(allowed.Values["isActive"].GetBoolean());
        var hidden = AuditSnapshot.Read(json, false); Assert.True(hidden.Redacted); Assert.DoesNotContain("purchaseCost", hidden.Values!.Keys);
    }
    [Theory]
    [InlineData("\"assetTypeId\":0")] [InlineData("\"userId\":null")] [InlineData("\"departmentId\":-1")]
    [InlineData("\"roleCount\":4")] [InlineData("\"isActive\":\"true\"")] [InlineData("\"defaultUsefulLifeMonths\":0")]
    [InlineData("\"purchaseCost\":1.001")] [InlineData("\"purchaseCost\":-1")] [InlineData("\"purchaseCost\":10000000000000000")]
    [InlineData("\"currentStatus\":\"InStock\"")] [InlineData("\"pageSize\":101")] [InlineData("\"returnedCount\":-1")]
    [InlineData("\"isArchived\":[]")] [InlineData("\"isAdminLocked\":{\"password\":\"hidden\"}")]
    [InlineData("\"metadata\":\"hidden\"")] [InlineData("\"serialNumber\":\"hidden\"")]
    public void Unsafe_snapshot_property_is_omitted_not_stringified(string property)
    {
        var result = AuditSnapshot.Read("{" + property + ",\"reasonProvided\":true}", true);
        Assert.True(result.Redacted); Assert.Equal(new[] { "reasonProvided" }, result.Values!.Keys);
    }

    private static AuditLogListQuery Window() => new() { From = "2026-10-04T00:00:00Z", To = "2026-10-05T00:00:00Z" };
    private static AuditLogService Service(Repository repo, Writer writer) => new(repo, new Actor(Permissions.AuditRead), new UnitOfWork(), writer);
    private static Repository Data()
    {
        var repo = new Repository();
        for (var id = 1L; id <= 4; id++) repo.Logs.Add(new AuditLog { Id = id, OccurredAtUtc = id == 4 ? Now : Now.AddDays(-1),
            ActorUserId = id == 3 ? 2 : 1, ActorType = "USER", Action = id == 1 ? "asset.create" : "asset.update", EntityType = "Asset",
            EntityId = "12", Outcome = "SUCCESS", CorrelationId = Guid.NewGuid(), RequestPath = "/private", MetadataJson = "{}" });
        return repo;
    }
    private sealed class Actor(params string[] permissions) : IActor
    {
        public long? UserId => 1; public Guid CorrelationId => Guid.Empty; public string? Method => "GET";
        public string? Path => "/api/v1/audit-logs"; public bool Has(string permission) => permissions.Contains(permission);
    }
    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work();
    }
    private sealed class Writer(Repository repo) : IAuditWriter
    {
        public List<string> Events { get; } = []; public object? After { get; private set; }
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null,
            object? after = null, string? failureCode = null)
        {
            Assert.Null(entity); Assert.Null(before); Events.Add(action); After = after;
            repo.Logs.Add(new() { Id = 100 + Events.Count, OccurredAtUtc = Now.AddHours(1), Action = action, ActorUserId = actorId,
                ActorType = actorType, Outcome = outcome });
        }
    }
    private sealed class Repository(string roleCode = "ADMIN_IT", bool active = true) : IRepository
    {
        public List<AuditLog> Logs { get; } = []; public int Saves { get; private set; } public bool FailSave { get; set; }
        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        {
            Assert.False(tracking);
            if (typeof(T) == typeof(AuditLog)) return (IQueryable<T>)Logs.AsQueryable();
            if (typeof(T) == typeof(UserRole)) return (IQueryable<T>)new[] { new UserRole { UserId = 1, RoleId = 1 } }.AsQueryable();
            if (typeof(T) == typeof(Role)) return (IQueryable<T>)new[] { new Role { Id = 1, Code = roleCode, IsActive = active } }.AsQueryable();
            throw new NotSupportedException();
        }
        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task SaveAsync(CancellationToken ct) { Saves++; return FailSave ? Task.FromException(new InvalidOperationException("Audit write failed.")) : Task.CompletedTask; }
        public void Add<T>(T entity) where T : class => throw new NotSupportedException();
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task LockAsync(string resource, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) => throw new NotSupportedException();
    }
}
