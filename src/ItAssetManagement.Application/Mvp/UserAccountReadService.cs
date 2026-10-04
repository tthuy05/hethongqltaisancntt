using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public sealed class UserAccountStateDto
{
    public long Id { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdminLocked { get; set; }
    public long[] RoleIds { get; set; } = [];
    public string RowVersion { get; set; } = "";
}
public sealed class UserAccountReadService(IRepository repo, IActor actor)
{
    public async Task<UserAccountStateDto> GetAsync(long id, CancellationToken ct)
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.UserRead))
            throw new BusinessException(403, "FORBIDDEN", "Không có quyền xem trạng thái tài khoản.");
        if (id <= 0) throw Validation.Invalid("userId", "ID phải > 0.");
        var memberships = repo.Query<UserRole>();
        // One correlated projection keeps membership and rowVersion in the same database read.
        return await repo.FirstAsync(repo.Query<User>().Where(x => x.Id == id).Select(x => new UserAccountStateDto
        {
            Id = x.Id, IsActive = x.IsActive, IsAdminLocked = x.IsAdminLocked, RowVersion = Convert.ToBase64String(x.RowVersion),
            RoleIds = memberships.Where(link => link.UserId == x.Id).OrderBy(link => link.RoleId).Select(link => link.RoleId).ToArray()
        }), ct) ?? throw new BusinessException(404, "USER_NOT_FOUND", "Không tìm thấy người dùng.");
    }
}
