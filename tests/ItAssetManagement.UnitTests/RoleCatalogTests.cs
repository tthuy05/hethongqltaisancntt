using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.UnitTests;

public sealed class RoleCatalogTests
{
    private static Repository Data() => new(
        new Role[] { new() { Id = 31, Code = "ADMIN_IT", Name = "Same" }, new() { Id = 7, Code = "SYSTEM_MANAGER", Name = "Same" },
            new() { Id = 52, Code = "TECHNICAL_SUPPORT", Name = "Support%_", IsActive = false }, new() { Id = 99, Code = "CUSTOM", Name = "Custom" } },
        new Permission[] { new() { Id = 2, Code = Permissions.UserRead, Name = "Users read", Module = "users" },
            new() { Id = 4, Code = Permissions.RoleRead, Name = "Roles read", Module = "roles", IsActive = false },
            new() { Id = 9, Code = "custom.secret", Name = "Hidden", Module = "custom" } },
        new RolePermission[] { new() { Id = 1, RoleId = 31, PermissionId = 2 }, new() { Id = 2, RoleId = 31, PermissionId = 4 } },
        new User[] { new() { Id = 8, IsActive = false, IsAdminLocked = true, RowVersion = new byte[16] } },
        new UserRole[] { new() { Id = 1, UserId = 8, RoleId = 31 }, new() { Id = 2, UserId = 8, RoleId = 7 } });
    [Theory]
    [InlineData(0, 20, null, "name", "asc")] [InlineData(1, 101, null, "name", "asc")]
    [InlineData(int.MaxValue, 100, null, "name", "asc")] [InlineData(1, 0, null, "name", "asc")]
    [InlineData(1, 20, "active", "name", "asc")] [InlineData(1, 20, null, "code", "asc")]
    [InlineData(1, 20, null, "description", "asc")] [InlineData(1, 20, null, "name", "DESC")]
    public void Invalid_list_query_is_rejected(int page, int pageSize, string? status, string sort, string direction) =>
        Assert.Equal(400, Assert.Throws<BusinessException>(() => RoleCatalogService.Validate(new()
            { Page = page, PageSize = pageSize, Status = status, SortBy = sort, SortDirection = direction })).Status);
    [Fact]
    public void Keyword_is_trimmed_bounded_and_can_be_empty()
    {
        var query = new RoleListQuery { Keyword = "  label  " }; RoleCatalogService.Validate(query); Assert.Equal("label", query.Keyword);
        query.Keyword = " "; RoleCatalogService.Validate(query); Assert.Null(query.Keyword);
        query.Keyword = new string('x', 201); Assert.Throws<BusinessException>(() => RoleCatalogService.Validate(query));
    }
    [Fact]
    public async Task Denial_happens_before_persistence_for_every_read()
    {
        var roles = new RoleCatalogService(null!, new Actor());
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => roles.ListAsync(new(), default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => roles.PermissionsAsync(null, null, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => roles.GetAsync(1, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => new UserAccountReadService(null!, new Actor()).GetAsync(1, default))).Status);
    }
    [Fact]
    public async Task Manager_projection_contains_only_id_and_name_with_no_hidden_code_search()
    {
        var service = new RoleCatalogService(Data(), new Actor(Permissions.RoleRead));
        var page = await service.ListAsync(new() { Status = "Active", PageSize = 1, SortDirection = "desc" }, default);
        Assert.Equal(2, page.TotalItems); Assert.Equal(7, page.Items[0].Id);
        var json = JsonSerializer.SerializeToElement(page.Items[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(["id", "name"], json.EnumerateObject().Select(x => x.Name).Order());
        Assert.Empty((await service.ListAsync(new() { Keyword = "ADMIN_IT" }, default)).Items);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(31, default))).Status);
    }
    [Fact]
    public async Task Admin_fixed_catalog_supports_literal_search_empty_pages_and_inactive_status()
    {
        var service = new RoleCatalogService(Data(), new Actor(Permissions.RoleRead, Permissions.RolePermissionsRead));
        var result = await service.ListAsync(new() { Keyword = "%_", Status = "Inactive" }, default);
        Assert.Equal("TECHNICAL_SUPPORT", Assert.Single(result.Items).Code); Assert.False(result.Items[0].IsActive);
        var outside = await service.ListAsync(new() { Page = 99 }, default); Assert.Empty(outside.Items); Assert.Equal(3, outside.TotalItems);
    }
    [Fact]
    public async Task Permission_mapping_is_allowlisted_and_retains_inactive_flags()
    {
        var service = new RoleCatalogService(Data(), new Actor(Permissions.RolePermissionsRead));
        var result = await service.GetAsync(31, default); Assert.Equal([4L, 2L], result.Permissions.Select(x => x.Id));
        Assert.False(result.Permissions[0].IsActive);
        Assert.Equal(4, Assert.Single(await service.PermissionsAsync("roles", " rEaD ", default)).Id);
        Assert.Empty(await service.PermissionsAsync("unknown", null, default));
        Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(99, default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(0, default))).Status);
        Assert.Throws<BusinessException>(() => Validation.Optional(new string('x', 101), 100, "module"));
    }
    [Fact]
    public async Task Account_projection_contains_only_required_flags_ids_and_version()
    {
        var service = new UserAccountReadService(Data(), new Actor(Permissions.UserRead));
        var result = await service.GetAsync(8, default);
        Assert.False(result.IsActive); Assert.True(result.IsAdminLocked); Assert.Equal([7L, 31L], result.RoleIds);
        Assert.Equal(16, Convert.FromBase64String(result.RowVersion).Length);
        Assert.Equal(["Id", "IsActive", "IsAdminLocked", "RoleIds", "RowVersion"], typeof(UserAccountStateDto).GetProperties().Select(x => x.Name).Order());
        Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(999, default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(0, default))).Status);
    }
    [Fact]
    public void Catalog_grants_are_separate_from_support_and_admin_write_policies()
    {
        Assert.Contains(Permissions.RoleRead, Permissions.Operate); Assert.DoesNotContain(Permissions.RoleRead, Permissions.Read);
        Assert.Contains(Permissions.RolePermissionsRead, Permissions.Admin); Assert.DoesNotContain(Permissions.RolePermissionsRead, Permissions.Operate);
        Assert.Equal(28, Permissions.All.Distinct().Count()); Assert.Equal(Permissions.All.Length, Permissions.All.Distinct().Count());
    }
    private sealed class Actor(params string[] permissions) : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId => Guid.Empty;
        public string? Method => "GET";
        public string? Path => "/api/v1/roles";
        public bool Has(string permission) => permissions.Contains(permission);
    }
    private sealed class Repository(params Array[] data) : IRepository
    {
        public IQueryable<T> Query<T>(bool tracking = false) where T : class
        { Assert.False(tracking); return data.OfType<T[]>().Single().AsQueryable(); }
        public Task<List<T>> ListAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.ToList());
        public Task<int> CountAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Count());
        public Task<T?> FirstAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.FirstOrDefault());
        public Task<bool> AnyAsync<T>(IQueryable<T> q, CancellationToken ct) => Task.FromResult(q.Any());
        public void Add<T>(T entity) where T : class => throw new NotSupportedException();
        public void ExpectVersion<T>(T entity, byte[] version) where T : class => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task LockAsync(string resource, CancellationToken ct) => throw new NotSupportedException();
        public Task LockAssetAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveWorkflowAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public IQueryable<Asset> SearchAssets(IQueryable<Asset> q, string keyword) => throw new NotSupportedException();
    }
}
