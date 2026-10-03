using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class DemoSeedTests(MvpFixture fixture)
{
    [NeonFact]
    public async Task Demo_seed_is_idempotent_and_preserves_passwords_profiles_existing_assets()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<DevelopmentDemoSeed>();
        var before = await db.Set<User>().AsNoTracking().SingleAsync(x => x.Email == MvpFixture.AdminEmail);
        var first = await seed.RunAsync(new(MvpFixture.Password, MvpFixture.Password));
        Assert.Equal(24, first.DemoAssets); Assert.Equal(27, first.DemoHistories);
        var demo = await db.Set<User>().AsNoTracking().SingleAsync(x => x.Email == DevelopmentDemoSeed.ManagerEmail);
        var assets = await db.Set<Asset>().AsNoTracking().Where(x => x.AssetCode.StartsWith(DevelopmentDemoSeed.Prefix))
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.CurrentStatus, RowVersion = Convert.ToBase64String(x.RowVersion) }).ToListAsync();
        var second = await seed.RunAsync(new("Different-private-password-2026!", "Different-private-password-2026!"));
        Assert.Equal(0, second.AddedUsers); Assert.Equal(0, second.AddedUserRoles); Assert.Equal(0, second.AddedAssets);
        Assert.Equal(demo.PasswordHash, (await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == demo.Id)).PasswordHash);
        Assert.Equal(before.PasswordHash, (await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == before.Id)).PasswordHash);
        Assert.Equal(assets, await db.Set<Asset>().AsNoTracking().Where(x => x.AssetCode.StartsWith(DevelopmentDemoSeed.Prefix))
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.CurrentStatus, RowVersion = Convert.ToBase64String(x.RowVersion) }).ToListAsync());
        Assert.Equal(3, assets.Count(x => x.CurrentStatus == "RETIRED"));
        var ids = assets.Select(x => x.Id.ToString()).ToArray();
        Assert.Equal(24, await db.Set<AuditLog>().CountAsync(x => x.Action == "development.demo.seed" && x.EntityType == "Asset" && ids.Contains(x.EntityId!)));
        var snapshots = await db.Set<AuditLog>().Where(x => x.Action == "development.demo.seed")
            .Select(x => new { x.OldValuesJson, x.NewValuesJson }).ToListAsync();
        Assert.DoesNotContain(MvpFixture.Password, string.Join("", snapshots.Select(x => (x.OldValuesJson ?? "") + (x.NewValuesJson ?? ""))));
    }

    [NeonFact]
    public async Task Invalid_private_bootstrap_rolls_back_without_adding_data()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = (await db.Set<User>().CountAsync(), await db.Set<Asset>().CountAsync(), await db.Set<AuditLog>().CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<DevelopmentDemoSeed>().RunAsync(new("weak", "weak")));
        Assert.Equal(before, (await db.Set<User>().CountAsync(), await db.Set<Asset>().CountAsync(), await db.Set<AuditLog>().CountAsync()));
    }
}
