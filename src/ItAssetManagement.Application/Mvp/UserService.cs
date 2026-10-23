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
    public string? Password { get; set; }
    public long[] RoleIds { get; set; } = [];
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
    public string? DepartmentName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public string InvitationEmailStatus { get; set; } = "";
    public DateTime? ActivationExpiresAt { get; set; }
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
        if (request.Password != null) throw Validation.Invalid("password", "Người dùng tự đặt mật khẩu qua email kích hoạt; Admin không nhập mật khẩu.");
        if(request.RoleIds==null || request.RoleIds.Length is <1 or >50 || request.RoleIds.Any(x=>x<=0) || request.RoleIds.Distinct().Count()!=request.RoleIds.Length)
            throw Validation.Invalid("roleIds", "Chọn ít nhất một nhóm hợp lệ, tối đa 50 nhóm không trùng.");
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

public sealed class UserService(IRepository repo, IUnitOfWork uow, IAuditWriter audit, IActor actor, UserInvitationService? invitations = null, IEmployeeCodeGenerator? employeeCodes = null)
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
        DepartmentId = x.DepartmentId, DepartmentName = repo.Query<Department>().Where(d=>d.Id==x.DepartmentId).Select(d=>d.Name).FirstOrDefault(),
        Phone = x.Phone, IsActive = x.IsActive, IsEmailConfirmed=x.IsEmailConfirmed,InvitationEmailStatus=x.InvitationEmailStatus,ActivationExpiresAt=x.ActivationExpiresAtUtc,CreatedAtUtc = x.CreatedAtUtc,
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
                          where role.Id == query.RoleId && role.IsActive select link.UserId;
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
        return (invitations ?? throw new InvalidOperationException("Invitation service required.")).CreateAsync(request, ct);
    }
    public Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct)
    {
        Require(Permissions.UserUpdate); PositiveId(id); UserValidation.Profile(request);
        return WriteAsync(id, request, Validation.Version(request.RowVersion), ct);
    }
    private Task<UserDto> WriteAsync(long id, UserProfileRequest request, byte[] version, CancellationToken ct) =>
        uow.RunAsync(async () =>
        {
            // Same lock order as master writes: an inactive department cannot become a new reference mid-write.
            await repo.LockAsync("master-data", ct);
            await repo.LockAsync("users-identity", ct);
            var user = await repo.FirstAsync(repo.Query<User>(true).Where(x => x.Id == id), ct) ?? throw Missing();
            var before = new { user.DepartmentId };
            repo.ExpectVersion(user, version);
            var email = request.Email.ToUpperInvariant(); var username = request.Username.ToUpperInvariant();
            if (await repo.AnyAsync(repo.Query<User>().Where(x => x.Id != id && x.NormalizedEmail == email), ct))
                throw Conflict("USER_EMAIL_CONFLICT");
            if (await repo.AnyAsync(repo.Query<User>().Where(x => x.Id != id && x.NormalizedUsername == username), ct))
                throw Conflict("USER_USERNAME_CONFLICT");
            if (!string.IsNullOrEmpty(request.EmployeeCode) &&
                !string.Equals(request.EmployeeCode, user.EmployeeCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(400, "EMPLOYEE_CODE_IMMUTABLE", "Mã nhân viên không thể thay đổi sau khi tạo.");
            }
            var employeeCodeChanged = false;
            if (string.IsNullOrEmpty(user.EmployeeCode) && employeeCodes != null)
            {
                user.EmployeeCode = await employeeCodes.NextAsync(ct);
                employeeCodeChanged = true;
            }
            if (request.DepartmentId != null && request.DepartmentId != user.DepartmentId)
            {
                if (!await repo.AnyAsync(repo.Query<Department>().Where(x => x.Id == request.DepartmentId && x.IsActive), ct))
                    throw Validation.Invalid("departmentId", "Phòng ban phải tồn tại và đang hoạt động.");
            }
            var emailChanged = user.NormalizedEmail != email;
            var usernameChanged = user.NormalizedUsername != username;
            var displayNameChanged = user.FullName != request.DisplayName;
            var phoneChanged = user.Phone != request.Phone;
            if (emailChanged || usernameChanged)
            {
                if (user.TokenVersion == int.MaxValue) throw Conflict("USER_TOKEN_VERSION_EXHAUSTED");
                user.TokenVersion++; // Old JWTs must not retain a previous login identity.
            }
            if(emailChanged && !user.IsEmailConfirmed)
            { user.ActivationTokenHash=null;user.ActivationExpiresAtUtc=null;user.InvitationEmailStatus="NotConfigured"; }
            user.Username = request.Username; user.NormalizedUsername = username; user.Email = request.Email; user.NormalizedEmail = email;
            user.FullName = request.DisplayName; user.Phone = request.Phone; user.DepartmentId = request.DepartmentId;
            user.UpdatedByUserId = actor.UserId;
            await repo.SaveAsync(ct);
            audit.Record("users.update", user, actor.UserId, "USER", before: before,
                after: new { user.DepartmentId, user.IsActive, emailChanged, usernameChanged, displayNameChanged, employeeCodeChanged, phoneChanged });
            await repo.SaveAsync(ct);
            return await repo.FirstAsync(Query().Where(x => x.Id == user.Id), ct) ?? throw Missing();
        }, ct);
    private static BusinessException Missing() => new(404, "USER_NOT_FOUND", "Không tìm thấy người dùng.");
    private static BusinessException Conflict(string code) => new(409, code, "Dữ liệu người dùng xung đột.");
}
