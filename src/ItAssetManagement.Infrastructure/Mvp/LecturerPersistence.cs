using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed class RoleGrantPersistence(AppDbContext db) : IRoleGrantPersistence
{
    public void RemoveGrant(RolePermission grant) => db.RemoveRoleGrant(grant);
}
