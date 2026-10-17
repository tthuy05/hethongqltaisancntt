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
public sealed class AssetCodeGenerator(AppDbContext db) : IAssetCodeGenerator
{
    public async Task<string> NextAsync(CancellationToken ct)
    {
        for (var attempt=0; attempt<10; attempt++)
        {
            await using var cmd=db.Database.GetDbConnection().CreateCommand();
            cmd.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
            cmd.CommandText="SELECT nextval('public.asset_code_sequence')";
            var value=Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            var code="AST-"+value.ToString("D6",System.Globalization.CultureInfo.InvariantCulture);
            if (!await db.Set<Asset>().AnyAsync(x=>x.AssetCode.ToLower()==code.ToLower(),ct)) return code;
        }
        throw new BusinessException(409,"ASSET_CODE_ALLOCATION_CONFLICT","Chưa cấp được mã tài sản, hãy thử lại sau.");
    }
}
