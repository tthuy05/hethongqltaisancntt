using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Text.Json.Serialization;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public class UserProfileRequest
{
    [Required] public string Username { get; set; } = "";
    [Required] public string Email { get; set; } = "";
    [Required] public string DisplayName { get; set; } = "";
    public string? EmployeeCode { get; set; }
    public long? DepartmentId { get; set; }
    public string? Phone { get; set; }
}
public sealed class CreateUserRequest : UserProfileRequest
{
    [Required] public string Password { get; set; } = "";
}
public sealed class UpdateUserRequest : UserProfileRequest
{
    [Required] public string RowVersion { get; set; } = "";
}
public sealed class UserListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public long? DepartmentId { get; set; }
    public long? RoleId { get; set; }
    public string SortBy { get; set; } = "displayName";
    public string SortDirection { get; set; } = "asc";
}
// Admin projection only; never return persistence entities or security state.
public sealed class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? EmployeeCode { get; set; }
    public long? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAtUtc { get; set; }
    [JsonPropertyName("updatedAt")] public DateTime? UpdatedAtUtc { get; set; }
    public string RowVersion { get; set; } = "";
}

public static class UserValidation
{
    public static void Profile(UserProfileRequest request)
    {
        request.Username = Validation.Required(request.Username, 100, "username");
        request.Email = Validation.Required(request.Email, 320, "email");
        request.DisplayName = Validation.Required(request.DisplayName, 200, "displayName");
        if (!MailAddress.TryCreate(request.Email, out var parsed) || parsed.Address != request.Email)
            throw Validation.Invalid("email", "Email không hợp lệ.");
        Validation.Required(request.Username.ToUpperInvariant(), 100, "username");
        Validation.Required(request.Email.ToUpperInvariant(), 320, "email");
        request.EmployeeCode = Validation.Optional(request.EmployeeCode, 50, "employeeCode");
        request.Phone = Validation.Optional(request.Phone, 30, "phone");
        if (request.DepartmentId <= 0) throw Validation.Invalid("departmentId", "ID phải > 0 hoặc null.");
    }
    public static void Create(CreateUserRequest request)
    {
        Profile(request);
        // Password whitespace is significant. Never trim or normalize it.
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 12 or > 256)
            throw Validation.Invalid("password", "Bắt buộc 12..256 ký tự; không được chỉ gồm khoảng trắng.");
    }
    public static void List(UserListQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw Validation.Invalid("page", "page >= 1, pageSize 1..100, offset hợp lệ.");
        query.Keyword = Validation.Optional(query.Keyword, 200, "keyword");
        if (query.DepartmentId <= 0 || query.RoleId <= 0) throw Validation.Invalid("filter", "ID phải > 0.");
        if (query.Status is not (null or "Active" or "Inactive")) throw Validation.Invalid("status", "Active hoặc Inactive.");
        if (query.SortBy is not ("id" or "username" or "email" or "displayName" or "createdAt" or "updatedAt") ||
            query.SortDirection is not ("asc" or "desc")) throw Validation.Invalid("sortBy", "Sắp xếp không hỗ trợ.");
    }
}

public sealed class UserService(IRepository repo, IUnitOfWork uow, IPasswordService passwords, IAuditWriter audit, IActor actor)
{
    private void Require(string permission)
    {
        if (actor.UserId == null || !actor.Has(permission))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền quản trị người dùng.");
    }
    private static void PositiveId(long id)
    {
        if (id <= 0) throw Validation.Invalid("userId", "ID phải > 0.");
    }
    private IQueryable<UserDto> Query() => repo.Query<User>().Select(x => new UserDto
    {
        Id = x.Id, Username = x.Username, Email = x.Email, DisplayName = x.FullName, EmployeeCode = x.EmployeeCode,
        DepartmentId = x.DepartmentId, Phone = x.Phone, IsActive = x.IsActive, CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc, RowVersion = Convert.ToBase64String(x.RowVersion)
    });
    public async Task<UserDto> GetAsync(long id, CancellationToken ct)
    {
        Require(Permissions.UserRead); PositiveId(id);
        return await repo.FirstAsync(Query().Where(x => x.Id == id), ct) ?? throw Missing();
    }
    public async Task<PagedResponse<UserDto>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        Require(Permissions.UserRead); UserValidation.List(query);
        var users = Query();
        if (query.DepartmentId != null) users = users.Where(x => x.DepartmentId == query.DepartmentId);
        if (query.Status != null) users = users.Where(x => x.IsActive == (query.Status == "Active"));
        if (query.RoleId != null)
        {
            var members = from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
                          where role.Id == query.RoleId && role.IsActive && Permissions.Roles.Contains(role.Code) select link.UserId;
            users = users.Where(x => members.Contains(x.Id));
        }
        if (query.Keyword != null)
        {
            var key = query.Keyword.ToLowerInvariant();
            users = users.Where(x => x.DisplayName.ToLower().Contains(key) || x.Username.ToLower().Contains(key) ||
                x.Email.ToLower().Contains(key) || x.EmployeeCode != null && x.EmployeeCode.ToLower().Contains(key));
        }
        var count = await repo.CountAsync(users, ct);
        var order = (query.SortBy, query.SortDirection) switch
        {
            ("id", "asc") => users.OrderBy(x => x.Id), ("id", "desc") => users.OrderByDescending(x => x.Id),
            ("username", "asc") => users.OrderBy(x => x.Username), ("username", "desc") => users.OrderByDescending(x => x.Username),
            ("email", "asc") => users.OrderBy(x => x.Email), ("email", "desc") => users.OrderByDescending(x => x.Email),
            ("createdAt", "asc") => users.OrderBy(x => x.CreatedAtUtc), ("createdAt", "desc") => users.OrderByDescending(x => x.CreatedAtUtc),
            ("updatedAt", "asc") => users.OrderBy(x => x.UpdatedAtUtc), ("updatedAt", "desc") => users.OrderByDescending(x => x.UpdatedAtUtc),
            ("displayName", "desc") => users.OrderByDescending(x => x.DisplayName), _ => users.OrderBy(x => x.DisplayName)
        };
        var items = await repo.ListAsync(order.ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize), ct);
        return new(items, query.Page, query.PageSize, count, (int)Math.Ceiling(count / (double)query.PageSize));
    }
    public Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        Require(Permissions.UserCreate); UserValidation.Create(request);
        return WriteAsync(null, request, request.Password, null, ct);
    }
    public Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct)
    {
        Require(Permissions.UserUpdate); PositiveId(id); UserValidation.Profile(request);
        return WriteAsync(id, request, null, Validation.Version(request.RowVersion), ct);
    }
    private Task<UserDto> WriteAsync(long? id, UserProfileRequest request, string? password, byte[]? version, CancellationToken ct) =>
        uow.RunAsync(async () =>
        {
            // Same lock order as master writes: an inactive department cannot become a new reference mid-write.
            await repo.LockAsync("master-data", ct);
            await repo.LockAsync("users-identity", ct);
            var user = id == null ? new User { CreatedByUserId = actor.UserId } :
                await repo.FirstAsync(repo.Query<User>(true).Where(x => x.Id == id), ct) ?? throw Missing();
            var before = id == null ? null : new { user.DepartmentId };
            if (id != null) repo.ExpectVersion(user, version!);
            var email = request.Email.ToUpperInvariant(); var username = request.Username.ToUpperInvariant();
            if (await repo.AnyAsync(repo.Query<User>().Where(x => x.Id != id && x.NormalizedEmail == email), ct))
                throw Conflict("USER_EMAIL_CONFLICT");
            if (await repo.AnyAsync(repo.Query<User>().Where(x => x.Id != id && x.NormalizedUsername == username), ct))
                throw Conflict("USER_USERNAME_CONFLICT");
            if (request.EmployeeCode != null)
            {
                var employee = request.EmployeeCode.ToLowerInvariant();
                if (await repo.AnyAsync(repo.Query<User>().Where(x => x.Id != id && x.EmployeeCode != null && x.EmployeeCode.ToLower() == employee), ct))
                    throw Conflict("USER_EMPLOYEE_CODE_CONFLICT");
            }
            if (request.DepartmentId != null && (id == null || request.DepartmentId != user.DepartmentId))
            {
                if (!await repo.AnyAsync(repo.Query<Department>().Where(x => x.Id == request.DepartmentId && x.IsActive), ct))
                    throw Validation.Invalid("departmentId", "Phòng ban phải tồn tại và đang hoạt động.");
            }
            var emailChanged = id != null && user.NormalizedEmail != email;
            var usernameChanged = id != null && user.NormalizedUsername != username;
            var displayNameChanged = id != null && user.FullName != request.DisplayName;
            var employeeCodeChanged = id != null && user.EmployeeCode != request.EmployeeCode;
            var phoneChanged = id != null && user.Phone != request.Phone;
            if (emailChanged || usernameChanged)
            {
                if (user.TokenVersion == int.MaxValue) throw Conflict("USER_TOKEN_VERSION_EXHAUSTED");
                user.TokenVersion++; // Old JWTs must not retain a previous login identity.
            }
            user.Username = request.Username; user.NormalizedUsername = username; user.Email = request.Email; user.NormalizedEmail = email;
            user.FullName = request.DisplayName; user.EmployeeCode = request.EmployeeCode; user.Phone = request.Phone; user.DepartmentId = request.DepartmentId;
            if (id == null) { user.PasswordHash = passwords.Hash(user, password!); repo.Add(user); }
            else user.UpdatedByUserId = actor.UserId;
            await repo.SaveAsync(ct);
            audit.Record(id == null ? "users.create" : "users.update", user, actor.UserId, "USER", before: before,
                after: new { user.DepartmentId, user.IsActive, emailChanged, usernameChanged, displayNameChanged, employeeCodeChanged, phoneChanged });
            await repo.SaveAsync(ct);
            return await repo.FirstAsync(Query().Where(x => x.Id == user.Id), ct) ?? throw Missing();
        }, ct);
    private static BusinessException Missing() => new(404, "USER_NOT_FOUND", "Không tìm thấy người dùng.");
    private static BusinessException Conflict(string code) => new(409, code, "Dữ liệu người dùng xung đột.");
}
