using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

public interface IRoleGrantPersistence { void RemoveGrant(RolePermission grant); }
public interface IPermissionAvailability { bool IsAvailable(string code); }
public sealed record PermissionOption(long Id, string Code, string Name, string Module, string Action, bool Available, string? Requires)
{ public bool AdminOnly => Code == Permissions.AuditRead; }
public sealed class RoleWriteRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? RowVersion { get; set; }
}
public sealed class RolePermissionsRequest
{
    public long[] PermissionIds { get; set; } = [];
    public string RowVersion { get; set; } = "";
}
public static class PermissionContract
{
    public static string? Requires(string code) => code switch
    {
        Permissions.RoleAssign or Permissions.RolePermissionsRead => Permissions.RoleRead,
        Permissions.UserCreate or Permissions.UserUpdate or Permissions.UserStatus => Permissions.UserRead,
        _ when code.StartsWith("assets.") && code != Permissions.AssetRead => Permissions.AssetRead,
        _ when code.StartsWith("departments.") && code != Permissions.DepartmentRead => Permissions.DepartmentRead,
        _ when code.StartsWith("asset-types.") && code != Permissions.TypeRead => Permissions.TypeRead,
        Permissions.AssignmentAssign or Permissions.AssignmentReturn => Permissions.AssignmentRead,
        _ => null
    };
    public static string Action(string code) => code switch
    {
        Permissions.AssetCost => "Xem chi phí", Permissions.AssetHistory => "Xem lịch sử",
        Permissions.UserLookup => "Tra cứu", Permissions.UserStatus or Permissions.AssetStatus => "Trạng thái",
        Permissions.RoleAssign => "Gán nhóm", Permissions.RolePermissionsRead => "Xem quyền",
        Permissions.AssignmentAssign => "Cấp phát", Permissions.AssignmentReturn => "Thu hồi",
        _ => code.Split('.').Last() switch { "read" => "Xem", "create" => "Thêm", "update" => "Sửa", "archive" => "Lưu trữ", _ => code }
    };
}

public sealed class RoleManagementService(IRepository repo, IRoleGrantPersistence persistence, IUnitOfWork uow,
    IAuditWriter audit, IActor actor, IPermissionAvailability availability)
{
    private void Require()
    {
        if (actor.UserId is not > 0 || !actor.Has(Permissions.RoleAssign) || !actor.Has(Permissions.RolePermissionsRead))
            throw Denied();
    }
    private async Task AdminAsync(CancellationToken ct)
    {
        await repo.LockAsync("users-identity", ct);
        var user = await repo.FirstAsync(repo.Query<User>().Where(x => x.Id == actor.UserId), ct);
        var roles = from membership in repo.Query<UserRole>() join role in repo.Query<Role>() on membership.RoleId equals role.Id
                    where membership.UserId == actor.UserId && role.IsActive && role.Code == "ADMIN_IT" select role.Id;
        if (user == null || !user.IsEmailConfirmed || !user.IsActive || user.IsAdminLocked || user.LockoutEndUtc > DateTime.UtcNow ||
            actor.TokenVersion != user.TokenVersion || !await repo.AnyAsync(roles, ct)) throw Denied();
        var grants = from roleId in roles join grant in repo.Query<RolePermission>() on roleId equals grant.RoleId
                     join definition in repo.Query<Permission>() on grant.PermissionId equals definition.Id
                     where definition.IsActive select definition.Code;
        var codes = await repo.ListAsync(grants, ct);
        if (!codes.Contains(Permissions.RoleAssign) || !codes.Contains(Permissions.RolePermissionsRead)) throw Denied();
    }
    public async Task<List<PermissionOption>> CatalogAsync(CancellationToken ct)
    {
        Require();
        // Even readers of the management matrix must be members of the protected Admin group.
        var isAdmin = from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
                      where link.UserId == actor.UserId && role.Code == "ADMIN_IT" && role.IsActive select link.Id;
        if (!await repo.AnyAsync(isAdmin, ct)) throw Denied();
        var definitions = await repo.ListAsync(repo.Query<Permission>().Where(x => x.IsActive && Permissions.All.Contains(x.Code)).OrderBy(x => x.Code), ct);
        return definitions.Select(x => new PermissionOption(x.Id, x.Code, x.Name, x.Module, PermissionContract.Action(x.Code),
            availability.IsAvailable(x.Code), PermissionContract.Requires(x.Code))).ToList();
    }
    public Task<RoleDetailDto> CreateAsync(RoleWriteRequest request, CancellationToken ct)
    {
        Require(); Validate(request, false);
        return uow.RunAsync(async () =>
        {
            await AdminAsync(ct);
            var role = new Role { Code = "CUSTOM_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = request.Name, Description = request.Description };
            repo.Add(role); await repo.SaveAsync(ct);
            audit.Record("roles.create", role, actor.UserId, "USER", after: new { role.Code, role.IsActive });
            await repo.SaveAsync(ct); return await DetailAsync(role, ct);
        }, ct);
    }
    public Task<RoleDetailDto> UpdateAsync(long id, RoleWriteRequest request, CancellationToken ct)
    {
        Require(); Validate(request, true);
        return uow.RunAsync(async () =>
        {
            await AdminAsync(ct); var role = await RoleAsync(id, request.RowVersion!, ct);
            if (role.IsSystem || Permissions.Roles.Contains(role.Code)) throw Protected();
            role.Name = request.Name; role.Description = request.Description;
            audit.Record("roles.update", role, actor.UserId, "USER", after: new { role.Code, role.IsActive });
            await repo.SaveAsync(ct); return await DetailAsync(role, ct);
        }, ct);
    }
    public Task<RoleDetailDto> StatusAsync(long id, StatusRequest request, CancellationToken ct)
    {
        Require(); if (request.Status is not ("Active" or "Inactive")) throw Validation.Invalid("status", "Active hoặc Inactive.");
        Validation.Required(request.Reason, 1000, "reason"); Validation.Version(request.RowVersion);
        return uow.RunAsync(async () =>
        {
            await AdminAsync(ct); var role = await RoleAsync(id, request.RowVersion, ct);
            if (role.IsSystem || Permissions.Roles.Contains(role.Code)) throw Protected();
            if (request.Status == "Inactive" && await repo.AnyAsync(repo.Query<UserRole>().Where(x => x.RoleId == id), ct))
                throw new BusinessException(409, "ROLE_IN_USE", "Bỏ gán nhóm khỏi mọi người dùng trước khi ngừng sử dụng.");
            role.IsActive = request.Status == "Active";
            audit.Record("roles.status.change", role, actor.UserId, "USER", after: new { role.IsActive, reasonProvided = true });
            await repo.SaveAsync(ct); return await DetailAsync(role, ct);
        }, ct);
    }
    public Task<RoleDetailDto> PermissionsAsync(long id, RolePermissionsRequest request, CancellationToken ct)
    {
        Require(); Validation.Version(request.RowVersion);
        if (request.PermissionIds == null || request.PermissionIds.Length > Permissions.All.Length || request.PermissionIds.Any(x => x <= 0) || request.PermissionIds.Distinct().Count()!=request.PermissionIds.Length)
            throw Validation.Invalid("permissionIds", "Chọn các quyền hợp lệ, không trùng.");
        return uow.RunAsync(async () =>
        {
            await AdminAsync(ct); var role = await RoleAsync(id, request.RowVersion, ct);
            if (!role.IsActive) throw new BusinessException(409, "ROLE_INACTIVE", "Nhóm đã ngừng hoạt động.");
            var definitions = await repo.ListAsync(repo.Query<Permission>().Where(x => request.PermissionIds.Contains(x.Id) && x.IsActive && Permissions.All.Contains(x.Code)), ct);
            if (definitions.Count != request.PermissionIds.Length) throw Validation.Invalid("permissionIds", "Có quyền không tồn tại hoặc chưa được hệ thống hỗ trợ.");
            var codes = definitions.Select(x => x.Code).ToHashSet();
            if (role.Code != "ADMIN_IT" && codes.Contains(Permissions.AuditRead))
                throw Validation.Invalid("permissionIds", "Nhật ký thao tác chỉ dành cho nhóm Admin IT.");
            if (definitions.Any(x => PermissionContract.Requires(x.Code) is { } parent && !codes.Contains(parent)))
                throw Validation.Invalid("permissionIds", "Quyền thao tác yêu cầu quyền xem tương ứng.");
            // The protected Admin group retains the complete supported baseline. This
            // guards the ability to recover identities even when there is only one Admin.
            if (role.Code == "ADMIN_IT" && Permissions.All.Any(code => !codes.Contains(code))) throw Protected();
            var existing = await repo.ListAsync(repo.Query<RolePermission>(true).Where(x => x.RoleId == id), ct);
            var existingIds = existing.Select(x => x.PermissionId).ToHashSet();
            var unavailable = await repo.ListAsync(repo.Query<Permission>().Where(x => existingIds.Contains(x.Id)), ct);
            // Closed modules keep their existing grants exactly; never seed/open a module here.
            if (definitions.Any(x => !availability.IsAvailable(x.Code) && !existingIds.Contains(x.Id)) ||
                unavailable.Any(x => !availability.IsAvailable(x.Code) && !request.PermissionIds.Contains(x.Id)))
                throw Validation.Invalid("permissionIds", "Quyền module chưa khả dụng được giữ nguyên, chưa được chỉnh sửa.");
            foreach (var grant in existing.Where(x => !request.PermissionIds.Contains(x.PermissionId)))
            {
                audit.Record("roles.permissions.remove", grant, actor.UserId, "USER", before: new { grant.RoleId, grant.PermissionId });
                persistence.RemoveGrant(grant);
            }
            foreach (var permissionId in request.PermissionIds.Where(x => !existingIds.Contains(x)))
            {
                var grant = new RolePermission { RoleId = id, PermissionId = permissionId, GrantedByUserId = actor.UserId, GrantedAtUtc = DateTime.UtcNow };
                repo.Add(grant); await repo.SaveAsync(ct);
                audit.Record("roles.permissions.add", grant, actor.UserId, "USER", after: new { grant.RoleId, grant.PermissionId });
            }
            role.UpdatedAtUtc = DateTime.UtcNow; // Regenerate the group's optimistic concurrency token.
            audit.Record("roles.permissions.replace", role, actor.UserId, "USER", before: new { permissionCount = existing.Count }, after: new { permissionCount = definitions.Count });
            await repo.SaveAsync(ct); return await DetailAsync(role, ct);
        }, ct);
    }
    private async Task<Role> RoleAsync(long id, string version, CancellationToken ct)
    {
        if(id<=0) throw Validation.Invalid("roleId", "Nhóm không hợp lệ.");
        var role=await repo.FirstAsync(repo.Query<Role>(true).Where(x=>x.Id==id),ct) ?? throw new BusinessException(404,"ROLE_NOT_FOUND","Không tìm thấy nhóm.");
        var bytes=Validation.Version(version);
        if(!role.RowVersion.SequenceEqual(bytes)) throw new BusinessException(409,"CONCURRENCY_CONFLICT","Nhóm đã thay đổi, hãy tải lại.");
        repo.ExpectVersion(role,bytes); return role;
    }
    private async Task<RoleDetailDto> DetailAsync(Role role,CancellationToken ct)
    {
        var grants=repo.Query<RolePermission>().Where(x=>x.RoleId==role.Id).Select(x=>x.PermissionId);
        var permissions=await repo.ListAsync(repo.Query<Permission>().Where(x=>grants.Contains(x.Id) && Permissions.All.Contains(x.Code)).Select(x=>new PermissionDto {Id=x.Id,Code=x.Code,Name=x.Name,Module=x.Module,IsActive=x.IsActive}),ct);
        return new(role.Id,role.Code,role.Name,role.IsActive,role.Description,permissions) {IsSystem=role.IsSystem,RowVersion=Convert.ToBase64String(role.RowVersion),MemberCount=await repo.CountAsync(repo.Query<UserRole>().Where(x=>x.RoleId==role.Id),ct)};
    }
    private static void Validate(RoleWriteRequest request,bool update)
    {request.Name=Validation.Required(request.Name,200,"name");request.Description=Validation.Optional(request.Description,1000,"description"); if(update)Validation.Version(request.RowVersion);}
    private static BusinessException Denied()=>new(403,"FORBIDDEN","Chỉ Admin IT có quyền quản lý nhóm và ma trận quyền.");
    private static BusinessException Protected()=>new(409,"SYSTEM_ROLE_PROTECTED","Không được vô hiệu nhóm mặc định hoặc làm mất quyền quản trị của Admin IT.");
}
