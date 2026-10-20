using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed record SeedCredentials(string Email, string Password);
public sealed record SeedResult(int Roles, int Permissions, int RolePermissions, int Departments, int AssetTypes, int Users, int UserRoles, int Added);
public sealed class DevelopmentSeed(AppDbContext db, IRepository repo, IUnitOfWork uow, IPasswordService passwords, IAuditWriter audit)
{
    // Explicit Week 4 catalog/grants only; never reset accounts or restore unrelated grants.
    public Task<int> RunAssignmentPermissionsAsync(CancellationToken ct = default) => uow.RunAsync(async () =>
    {
        await repo.LockAsync("development-seed", ct);
        var created = new List<object>();
        var roles = new List<Role>();
        foreach (var (code, name) in new[] { ("ADMIN_IT", "Admin IT"), ("SYSTEM_MANAGER", "System Manager") })
        {
            var role = await db.Set<Role>().SingleOrDefaultAsync(x => x.Code == code, ct);
            if (role is { IsActive: false }) throw new InvalidOperationException("Inactive fixed role requires review.");
            if (role == null)
            {
                role = new Role { Code = code, Name = name, IsSystem = true };
                db.Add(role); created.Add(role); await db.SaveChangesAsync(ct);
            }
            roles.Add(role);
        }
        foreach (var code in new[] { Permissions.AssignmentRead, Permissions.AssignmentAssign, Permissions.AssignmentReturn })
        {
            var permission = await db.Set<Permission>().SingleOrDefaultAsync(x => x.Code == code, ct);
            if (permission is { IsActive: false }) throw new InvalidOperationException("Inactive assignment permission requires review.");
            if (permission == null)
            {
                permission = new Permission { Code = code, Name = code, Module = "assignments" };
                db.Add(permission); created.Add(permission); await db.SaveChangesAsync(ct);
            }
            foreach (var role in roles)
                if (!await db.Set<RolePermission>().AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, ct))
                {
                    var grant = new RolePermission { RoleId = role.Id, PermissionId = permission.Id, GrantedAtUtc = DateTime.UtcNow };
                    db.Add(grant); created.Add(grant);
                }
        }
        await db.SaveChangesAsync(ct);
        foreach (var entity in created) audit.Record("development.assignment-permissions.seed", entity, null, "SYSTEM");
        await db.SaveChangesAsync(ct); return created.Count;
    }, ct);

    // Explicit additive audit permission only; preserve existing accounts, roles and grants.
    public Task<int> RunAuditReadAsync(CancellationToken ct = default) => uow.RunAsync(async () =>
    {
        await repo.LockAsync("development-seed", ct);
        var admin = await db.Set<Role>().SingleOrDefaultAsync(x => x.Code == "ADMIN_IT" && x.IsActive, ct)
            ?? throw new InvalidOperationException("An existing active ADMIN_IT role is required.");
        var created = new List<object>();
        var permission = await db.Set<Permission>().SingleOrDefaultAsync(x => x.Code == Permissions.AuditRead, ct);
        if (permission == null)
        {
            permission = new Permission { Code = Permissions.AuditRead, Name = Permissions.AuditRead, Module = "audit-logs" };
            db.Add(permission); created.Add(permission); await db.SaveChangesAsync(ct);
        }
        if (!await db.Set<RolePermission>().AnyAsync(x => x.RoleId == admin.Id && x.PermissionId == permission.Id, ct))
        {
            var link = new RolePermission { RoleId = admin.Id, PermissionId = permission.Id, GrantedAtUtc = DateTime.UtcNow };
            db.Add(link); created.Add(link);
        }
        await db.SaveChangesAsync(ct);
        foreach (var entity in created) audit.Record("development.audit-read.seed", entity, null, "SYSTEM");
        await db.SaveChangesAsync(ct); return created.Count;
    }, ct);

    // Explicit narrow catalog update: never creates/resets users or restores unrelated grants.
    public Task<int> RunRoleCatalogAsync(CancellationToken ct = default) => uow.RunAsync(async () =>
    {
        await repo.LockAsync("development-seed", ct);
        var roles = await db.Set<Role>().Where(x => x.Code == "ADMIN_IT" || x.Code == "SYSTEM_MANAGER").ToListAsync(ct);
        if (roles.Count != 2) throw new InvalidOperationException("Existing fixed roles are required.");
        var created = new List<object>();
        foreach (var code in new[] { Permissions.RoleRead, Permissions.RolePermissionsRead })
        {
            var permission = await db.Set<Permission>().SingleOrDefaultAsync(x => x.Code == code, ct);
            if (permission == null)
            {
                permission = new Permission { Code = code, Name = code, Module = "roles" };
                db.Add(permission); created.Add(permission); await db.SaveChangesAsync(ct);
            }
            foreach (var role in roles.Where(x => x.Code == "ADMIN_IT" || code == Permissions.RoleRead))
                if (!await db.Set<RolePermission>().AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, ct))
                {
                    var link = new RolePermission { RoleId = role.Id, PermissionId = permission.Id, GrantedAtUtc = DateTime.UtcNow };
                    db.Add(link); created.Add(link);
                }
        }
        await db.SaveChangesAsync(ct);
        foreach (var entity in created) audit.Record("development.role-catalog.seed", entity, null, "SYSTEM");
        await db.SaveChangesAsync(ct); return created.Count;
    }, ct);

    public async Task<SeedResult> RunAsync(SeedCredentials credentials, CancellationToken ct = default)
    {
        var added = await uow.RunAsync(async () =>
        {
            await repo.LockAsync("development-seed", ct); await repo.LockAsync("master-data", ct);
            var created = new List<object>();
            var names = new[] { "Admin IT", "System Manager", "Technical Support" };
            for (var i = 0; i < Permissions.Roles.Length; i++)
            {
                var code = Permissions.Roles[i];
                if (!await db.Set<Role>().AnyAsync(x => x.Code == code, ct))
                { var role = new Role { Code = code, Name = names[i], IsSystem = true }; db.Add(role); created.Add(role); }
            }
            foreach (var code in Permissions.All)
                if (!await db.Set<Permission>().AnyAsync(x => x.Code == code, ct))
                { var p = new Permission { Code = code, Name = code, Module = code.Split('.')[0] }; db.Add(p); created.Add(p); }
            foreach (var (code, name) in new[] { ("IT", "IT"), ("ACCOUNTING", "Kế toán"), ("HR", "Nhân sự"), ("SALES", "Kinh doanh") })
                if (!await db.Set<Department>().AnyAsync(x => x.Code.ToLower() == code.ToLower(), ct))
                { var d = new Department { Code = code, Name = name }; db.Add(d); created.Add(d); }
            foreach (var name in new[] { "Laptop", "Desktop", "Monitor", "Printer", "Router", "Switch", "Server", "Other" })
            {
                var code = name.ToUpperInvariant();
                if (!await db.Set<AssetType>().AnyAsync(x => x.Code.ToLower() == code.ToLower(), ct))
                { var t = new AssetType { Code = code, Name = name }; db.Add(t); created.Add(t); }
            }
            await db.SaveChangesAsync(ct);
            var roles = await db.Set<Role>().ToListAsync(ct); var permissions = await db.Set<Permission>().ToListAsync(ct);
            foreach (var roleCode in Permissions.Roles)
            {
                var role = roles.Single(x => x.Code == roleCode);
                var grants = roleCode == "ADMIN_IT" ? Permissions.All : roleCode == "SYSTEM_MANAGER" ? [.. Permissions.Read, .. Permissions.Operate] : Permissions.Read;
                foreach (var permissionCode in grants)
                {
                    var permission = permissions.Single(x => x.Code == permissionCode);
                    if (!await db.Set<RolePermission>().AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, ct))
                    {
                        var grant = new RolePermission { RoleId = role.Id, PermissionId = permission.Id, GrantedAtUtc = DateTime.UtcNow };
                        db.Add(grant); created.Add(grant);
                    }
                }
            }
            var email = Validation.Required(credentials.Email, 320, "email"); var normalized = email.ToUpperInvariant();
            var user = await db.Set<User>().SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
            if (user == null)
            {
                user = new User { Username = "dev.admin", NormalizedUsername = "DEV.ADMIN", Email = email, NormalizedEmail = normalized,
                    FullName = "Development Admin", DepartmentId = (await db.Set<Department>().SingleAsync(x => x.Code == "IT", ct)).Id };
                user.PasswordHash = passwords.Hash(user, credentials.Password); db.Add(user); created.Add(user);
            }
            // Existing accounts/credentials are never reset or silently promoted.
            var newUser = created.Contains(user);
            await db.SaveChangesAsync(ct);
            var admin = roles.Single(x => x.Code == "ADMIN_IT");
            if (newUser && !await db.Set<UserRole>().AnyAsync(x => x.UserId == user.Id && x.RoleId == admin.Id, ct))
            { var link = new UserRole { UserId = user.Id, RoleId = admin.Id, AssignedAtUtc = DateTime.UtcNow }; db.Add(link); created.Add(link); }
            await db.SaveChangesAsync(ct);
            foreach (var entity in created) audit.Record("development.seed", entity, null, "SYSTEM");
            await db.SaveChangesAsync(ct); return created.Count;
        }, ct);
        return new(await db.Set<Role>().CountAsync(ct), await db.Set<Permission>().CountAsync(ct), await db.Set<RolePermission>().CountAsync(ct),
            await db.Set<Department>().CountAsync(ct), await db.Set<AssetType>().CountAsync(ct), await db.Set<User>().CountAsync(ct),
            await db.Set<UserRole>().CountAsync(ct), added);
    }
}
