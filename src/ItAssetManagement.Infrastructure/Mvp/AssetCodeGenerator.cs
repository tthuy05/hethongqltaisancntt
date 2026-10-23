using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;

namespace ItAssetManagement.Infrastructure.Mvp;

// Required by the reviewed LF migration's immutable asset-code trigger.
public sealed class AssetCodeGenerator(AppDbContext db) : IAssetCodeGenerator
{
    public Task<string> NextAsync(CancellationToken ct) =>
        BusinessCodeGeneratorEngine.NextCodeAsync<Asset>(
            db,
            "asset_code_sequence",
            "AST-",
            "assets",
            "asset_code",
            code => x => x.AssetCode.ToLower() == code.ToLower(),
            ct,
            "ASSET_CODE_ALLOCATION_CONFLICT");
}
