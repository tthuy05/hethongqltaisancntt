using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Used inside the identity transaction/lock, not the earlier request snapshot:
// concurrent revocation must not authorize a delegated operator's new grants.
public static class EffectivePermissionQuery
{
    public static IQueryable<string> ForUser(IRepository repository,long userId) =>
        (from link in repository.Query<UserRole>() join role in repository.Query<Role>() on link.RoleId equals role.Id
         join grant in repository.Query<RolePermission>() on role.Id equals grant.RoleId
         join permission in repository.Query<Permission>() on grant.PermissionId equals permission.Id
         where link.UserId==userId&&role.IsActive&&permission.IsActive&&Permissions.All.Contains(permission.Code)
         select permission.Code).Distinct();
}
