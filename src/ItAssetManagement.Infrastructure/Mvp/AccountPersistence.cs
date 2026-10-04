using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed class AccountPersistence(AppDbContext db) : IAccountPersistence
{
    public void RemoveMembership(UserRole membership) => db.RemoveRoleMembership(membership);
    public async Task<string[]> AllocationWarningsAsync(CancellationToken ct)
    {
        // Neither allocation table exists in M1. If a later migration adds one, do not
        // pretend allocations were counted before the workflow-specific query is implemented.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT to_regclass('public.asset_assignments') IS NOT NULL OR to_regclass('public.license_assignments') IS NOT NULL";
        return (bool)(await command.ExecuteScalarAsync(ct))! ? ["ALLOCATION_REVIEW_REQUIRED"] : [];
    }
}
