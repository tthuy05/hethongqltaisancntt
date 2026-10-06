using ItAssetManagement.Application.Mvp;

namespace ItAssetManagement.UnitTests;

public sealed class UserManagementTests
{
    private static CreateUserRequest Valid() => new() { Username = " thuy ", Email = " Thuy@example.test ", DisplayName = " Thủy ", Password = " twelve characters " };

    [Fact]
    public void Profile_trims_identity_and_contact_but_preserves_password()
    {
        var request = Valid(); request.EmployeeCode = " E01 "; request.Phone = " 0123456789 ";
        UserValidation.Create(request);
        Assert.Equal("thuy", request.Username); Assert.Equal("Thuy@example.test", request.Email);
        Assert.Equal("Thủy", request.DisplayName); Assert.Equal("E01", request.EmployeeCode); Assert.Equal("0123456789", request.Phone);
        Assert.Equal(" twelve characters ", request.Password);
        request.EmployeeCode = "  "; request.Phone = " "; UserValidation.Profile(request);
        Assert.Null(request.EmployeeCode); Assert.Null(request.Phone); Assert.Null(request.DepartmentId);
    }

    [Theory]
    [InlineData("username", " ")]
    [InlineData("email", "not-an-email")]
    [InlineData("email", "Name <someone@example.test>")]
    [InlineData("displayName", "")]
    [InlineData("password", "short")]
    [InlineData("password", "            ")]
    [InlineData("departmentId", "0")]
    [InlineData("departmentId", "-1")]
    public void Invalid_profile_fields_are_400(string field, string value)
    {
        var request = Valid(); typeof(CreateUserRequest).GetProperty(char.ToUpperInvariant(field[0]) + field[1..])!
            .SetValue(request, field == "departmentId" ? (object)long.Parse(value) : value);
        Assert.Equal(400, Assert.Throws<BusinessException>(() => UserValidation.Create(request)).Status);
    }

    [Theory]
    [InlineData("Username", 101)] [InlineData("Email", 321)] [InlineData("DisplayName", 201)]
    [InlineData("EmployeeCode", 51)] [InlineData("Phone", 31)] [InlineData("Password", 257)]
    public void Physical_column_and_password_lengths_are_bounded(string field, int length)
    {
        var request = Valid(); typeof(CreateUserRequest).GetProperty(field)!.SetValue(request, new string('a', length));
        Assert.Equal(400, Assert.Throws<BusinessException>(() => UserValidation.Create(request)).Status);
    }

    [Theory]
    [InlineData("Page", 0)] [InlineData("Page", int.MaxValue)] [InlineData("PageSize", 0)] [InlineData("PageSize", 101)]
    public void Pagination_rejects_invalid_or_overflowing_offsets(string field, int value)
    {
        var query = new UserListQuery(); typeof(UserListQuery).GetProperty(field)!.SetValue(query, value);
        Assert.Equal(400, Assert.Throws<BusinessException>(() => UserValidation.List(query)).Status);
    }

    [Fact]
    public void Directory_has_explicit_query_allowlists_and_admin_only_grants()
    {
        foreach (var query in new UserListQuery[] { new() { RoleId = 0 }, new() { DepartmentId = -1 }, new() { Status = "Locked" },
            new() { Status = "active" }, new() { SortBy = "passwordHash" }, new() { SortDirection = "ASC" }, new() { Keyword = new string('a', 201) } })
            Assert.Throws<BusinessException>(() => UserValidation.List(query));
        foreach (var permission in new[] { Permissions.UserRead, Permissions.UserCreate, Permissions.UserUpdate })
        { Assert.Contains(permission, Permissions.Admin); Assert.DoesNotContain(permission, Permissions.Read); Assert.DoesNotContain(permission, Permissions.Operate); }
        Assert.Equal(25, Permissions.All.Distinct().Count());
        Assert.DoesNotContain("Password", typeof(UpdateUserRequest).GetProperties().Select(x => x.Name));
        foreach (var forbidden in new[] { "Password", "PasswordHash", "NormalizedEmail", "TokenVersion", "IsAdminLocked", "Roles", "LockoutEndUtc" })
            Assert.DoesNotContain(forbidden, typeof(UserDto).GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task Services_deny_before_touching_the_repository_or_password_service()
    {
        var service = new UserService(null!, null!, null!, null!, new DeniedActor());
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.ListAsync(new(), default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.GetAsync(1, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(Valid(), default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(1, new(), default))).Status);
    }
    private sealed class DeniedActor : IActor
    {
        public long? UserId => 1;
        public Guid CorrelationId => Guid.Empty;
        public string? Method => null;
        public string? Path => null;
        public bool Has(string permission) => false;
    }
}
