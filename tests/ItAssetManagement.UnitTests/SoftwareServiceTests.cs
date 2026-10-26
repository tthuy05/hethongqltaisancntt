using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class SoftwareServiceTests
{
    private sealed class Repo : IRepository
    {
        public List<Software> Items = [];
        public List<SoftwareLicense> Licenses = [];
        public IQueryable<T> Query<T>(bool tracking = false) where T : class =>
            (typeof(T) == typeof(Software) ? Items.Cast<T>() : Licenses.Cast<T>()).AsQueryable();
        public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.ToList());
        public Task<T?> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.FirstOrDefault());
        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => Task.FromResult(query.Any());
        public void Add<T>(T entity) where T : class { var item = (Software)(object)entity; item.Id = 1; item.RowVersion = new byte[16]; Items.Add(item); }
        public void ExpectVersion<T>(T entity, byte[] version) where T : class { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task LockAsync(string resource, CancellationToken ct) => Task.CompletedTask;
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> query, string keyword) => throw new NotSupportedException();
    }
    private sealed class Uow : IUnitOfWork { public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work(); }
    private sealed class Actor(params string[] grants) : IActor
    {
        public long? UserId => 1; public Guid CorrelationId => Guid.Empty; public string? Method => null; public string? Path => null;
        public bool Has(string code) => grants.Contains(code);
    }
    private sealed class Audit : IAuditWriter
    {
        public List<string> Actions = [];
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null) => Actions.Add(action);
    }
    private sealed class Codes : ISoftwareCodeGenerator { public Task<string> NextAsync(CancellationToken ct) => Task.FromResult("PM000001"); }
    private static SoftwareService Service(Repo? repo = null, Actor? actor = null, Audit? audit = null) => new(repo ?? new(), new Uow(), actor ?? new(SoftwarePermissions.All), audit ?? new(), new Codes());
    private static Software Item() => new() { Id = 1, Code = "PM000001", Name = "Office", RowVersion = new byte[16] };

    [Theory]
    [InlineData("")] [InlineData(" ")]
    public async Task Name_is_required(string name) => Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(null, new() { Name = name }, default))).Status);

    [Theory]
    [InlineData("name", 251)] [InlineData("publisher", 251)] [InlineData("version", 101)] [InlineData("description", 2001)]
    public async Task Semantic_lengths_are_preserved(string field, int length)
    {
        var request = new SoftwareRequest { Name = "Office" }; var value = new string('x', length);
        if (field == "name") request.Name = value; if (field == "publisher") request.Publisher = value;
        if (field == "version") request.Version = value; if (field == "description") request.Description = value;
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => Service().WriteAsync(null, request, default))).Status);
    }
    [Fact]
    public async Task Read_and_explicit_action_are_required_without_role_inheritance()
    {
        await Assert.ThrowsAsync<BusinessException>(() => Service(actor: new(SoftwarePermissions.Create)).WriteAsync(null, new() { Name = "Office" }, default));
        await Assert.ThrowsAsync<BusinessException>(() => Service(actor: new(SoftwarePermissions.Read)).WriteAsync(null, new() { Name = "Office" }, default));
    }
    [Theory]
    [InlineData(0, 20, null, "name")] [InlineData(1, 101, null, "name")] [InlineData(int.MaxValue, 100, null, "name")]
    [InlineData(1, 20, "Deleted", "name")] [InlineData(1, 20, null, "password")]
    public async Task Unsafe_paging_status_and_sort_are_rejected(int page, int size, string? status, string sort)
        => await Assert.ThrowsAsync<BusinessException>(() => Service().ListAsync(new() { Page = page, PageSize = size, Status = status, SortBy = sort }, default));

    [Fact]
    public async Task List_supports_case_insensitive_literal_search_and_vendor_filter()
    {
        var repo = new Repo(); repo.Items.Add(new() { Id = 1, Code = "PM000001", Name = "A%B", Publisher = "Acme", RowVersion = new byte[16] }); repo.Items.Add(new() { Id = 2, Code = "PM000002", Name = "AXXB", Publisher = "Acme", RowVersion = new byte[16] });
        var result = await Service(repo).ListAsync(new() { Keyword = "%", Vendor = "ACME", Status = "Active" }, default);
        Assert.Equal(1, result.TotalItems); Assert.Equal("A%B", Assert.Single(result.Items).Name);
    }
    [Fact]
    public async Task Create_uses_generator_and_trims_optional_fields()
    {
        var audit = new Audit(); var result = await Service(audit: audit).WriteAsync(null, new() { Name = " Office ", Publisher = " " }, default);
        Assert.Equal("PM000001", result.Code); Assert.Equal("Office", result.Name); Assert.Null(result.Publisher); Assert.Contains("software.create", audit.Actions);
    }
    [Fact]
    public async Task Stale_update_and_archive_cannot_mutate_entity()
    {
        var repo = new Repo(); repo.Items.Add(Item()); var stale = Enumerable.Repeat((byte)1, 16).ToArray();
        Assert.Equal("CONCURRENCY_CONFLICT", (await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(1, new() { Name = "Changed", RowVersion = Convert.ToBase64String(stale) }, default))).Code);
        await Assert.ThrowsAsync<BusinessException>(() => Service(repo).ArchiveAsync(1, stale, default)); Assert.True(repo.Items[0].IsActive); Assert.Equal("Office", repo.Items[0].Name);
    }
    [Fact]
    public async Task Active_license_blocks_archive_but_no_license_allows_soft_archive()
    {
        var repo = new Repo(); repo.Items.Add(Item()); repo.Licenses.Add(new() { SoftwareId = 1 });
        Assert.Equal("SOFTWARE_ACTIVE_LICENSES", (await Assert.ThrowsAsync<BusinessException>(() => Service(repo).ArchiveAsync(1, new byte[16], default))).Code);
        repo.Licenses.Clear(); await Service(repo).ArchiveAsync(1, new byte[16], default); Assert.False(repo.Items[0].IsActive); Assert.Single(repo.Items);
        Assert.Equal("SOFTWARE_INACTIVE", (await Assert.ThrowsAsync<BusinessException>(() => Service(repo).WriteAsync(1, new() { Name = "Changed", RowVersion = Convert.ToBase64String(new byte[16]) }, default))).Code);
    }
}
