using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class UserLookupTests
{
    [Theory]
    [InlineData("page", "0")]
    [InlineData("page", "-1")]
    [InlineData("page", "2147483647")]
    [InlineData("pageSize", "0")]
    [InlineData("pageSize", "101")]
    [InlineData("departmentId", "0")]
    [InlineData("departmentId", "-1")]
    [InlineData("status", "Inactive")]
    [InlineData("status", "active")]
    [InlineData("sortBy", "email")]
    [InlineData("sortBy", "passwordHash")]
    [InlineData("sortDirection", "DESC")]
    public void Invalid_queries_are_rejected(string field, string value)
    {
        var query = new UserLookupQuery();
        switch (field)
        {
            case "page": query.Page = int.Parse(value); break;
            case "pageSize": query.PageSize = int.Parse(value); break;
            case "departmentId": query.DepartmentId = long.Parse(value); break;
            case "status": query.Status = value; break;
            case "sortBy": query.SortBy = value; break;
            case "sortDirection": query.SortDirection = value; break;
        }
        var error = Assert.Throws<BusinessException>(() => UserLookupService.Validate(query));
        Assert.Equal(400, error.Status); Assert.Equal("VALIDATION_ERROR", error.Code);
    }

    [Fact]
    public void Keyword_is_trimmed_and_length_bounded()
    {
        var query = new UserLookupQuery { Keyword = "   Thủy   ", Status = "Active" };
        UserLookupService.Validate(query); Assert.Equal("Thủy", query.Keyword);
        query.Keyword = new string('x', 201);
        Assert.Throws<BusinessException>(() => UserLookupService.Validate(query));
        query.Keyword = "   "; UserLookupService.Validate(query); Assert.Null(query.Keyword);
    }

    [Fact]
    public async Task Permission_is_checked_before_querying_the_repository()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => new UserLookupService(new Repository(), new Actor(false)).ListAsync(new(), default));
        Assert.Equal(403, error.Status); Assert.Equal("FORBIDDEN", error.Code);
    }

    [Fact]
    public async Task Active_users_have_minimal_projection_and_stable_paging()
    {
        var repo = new Repository([
            new() { Id = 3, FullName = "Same", DepartmentId = 2 },
            new() { Id = 1, FullName = "Same", DepartmentId = 2 },
            new() { Id = 2, FullName = "Same", DepartmentId = 2 },
            new() { Id = 4, FullName = "Same", DepartmentId = 2, IsActive = false },
            new() { Id = 5, FullName = "Same", DepartmentId = null },
            new() { Id = 6, FullName = "Same", DepartmentId = 2, IsAdminLocked = true }
        ]);
        var service = new UserLookupService(repo, new Actor(true));
        var page = await service.ListAsync(new() { Keyword = " sAMe ", DepartmentId = 2, PageSize = 2, SortDirection = "desc" }, default);
        Assert.Equal(4, page.TotalItems); Assert.Equal(2, page.TotalPages);
        Assert.Equal([1L, 2L], page.Items.Select(x => x.Id));
        var second = await service.ListAsync(new() { DepartmentId = 2, PageSize = 2, Page = 2, SortBy = "id", SortDirection = "desc" }, default);
        Assert.Equal([2L, 1L], second.Items.Select(x => x.Id));
        var past = await service.ListAsync(new() { Page = 99 }, default);
        Assert.Empty(past.Items); Assert.Equal(5, past.TotalItems);
        Assert.Equal(["DepartmentId", "DisplayName", "Id"], typeof(UserLookupDto).GetProperties().Select(x => x.Name).Order());
    }

    private sealed class Actor(bool allowed) : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId => Guid.Empty;
        public string? Method => "GET";
        public string? Path => "/api/v1/users/lookup";
        public bool Has(string permission) => allowed && permission == Permissions.UserLookup;
    }
    // Read-only fake, not evidence of SQL translation. Real queries are tested on isolated Neon.
    private sealed class Repository(List<User>? users = null) : IRepository
    {
        public IQueryable<T> Query<T>(bool tracking = false) where T : class =>
            users?.Cast<T>().AsQueryable() ?? throw new InvalidOperationException("Query was not expected.");
        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => throw new NotSupportedException();
        public void Add<T>(T entity) where T : class => throw new NotSupportedException();
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task LockAsync(string resource, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long assetId, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }
}
