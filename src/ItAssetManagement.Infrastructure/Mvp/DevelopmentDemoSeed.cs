using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.Infrastructure.Mvp;

public sealed record DemoCredentials(string ManagerPassword, string SupportPassword);
public sealed record DemoSeedResult(int AddedUsers, int AddedUserRoles, int AddedAssets, int DemoAssets, int DemoHistories);

// Explicit development CLI only; never invoked at HTTP startup. All writes use the existing audited transaction.
public sealed class DevelopmentDemoSeed(AppDbContext db, IRepository repo, IUnitOfWork uow,
    IPasswordService passwords, IAuditWriter audit)
{
    public const string Prefix = "M1-DEMO-20261003-";
    public const string ManagerEmail = "manager.demo@itasset.test", SupportEmail = "support.demo@itasset.test";
    public async Task<DemoSeedResult> RunAsync(DemoCredentials credentials, CancellationToken ct = default)
    {
        var added = await uow.RunAsync(async () =>
        {
            await repo.LockAsync("development-seed", ct); await repo.LockAsync("master-data", ct);
            var departments = await db.Set<Department>().Where(x => x.IsActive).ToListAsync(ct);
            var types = await db.Set<AssetType>().Where(x => x.IsActive).ToListAsync(ct);
            var deptCodes = new[] { "IT", "ACCOUNTING", "HR", "SALES" };
            var typeCodes = new[] { "LAPTOP", "DESKTOP", "MONITOR", "PRINTER", "ROUTER", "SWITCH", "SERVER", "OTHER" };
            if (deptCodes.Any(code => departments.Count(x => x.Code == code) != 1) ||
                typeCodes.Any(code => types.Count(x => x.Code == code) != 1)) throw new InvalidOperationException("Active baseline masters required.");
            var users = 0; var links = 0; var assets = 0;
            foreach (var (email, roleCode, password, name) in new[] {
                (ManagerEmail, "SYSTEM_MANAGER", credentials.ManagerPassword, "Demo System Manager"),
                (SupportEmail, "TECHNICAL_SUPPORT", credentials.SupportPassword, "Demo Technical Support") })
            {
                // Also validate private input on repeat without resetting an existing password.
                if (password is null || password.Length is < 12 or > 256) throw new InvalidOperationException("Private bootstrap invalid.");
                var role = await db.Set<Role>().SingleAsync(x => x.Code == roleCode && x.IsActive, ct);
                var user = await db.Set<User>().SingleOrDefaultAsync(x => x.NormalizedEmail == email.ToUpperInvariant(), ct);
                if (user != null)
                {
                    var grants = await db.Set<UserRole>().Where(x => x.UserId == user.Id).Select(x => x.RoleId).ToListAsync(ct);
                    if (grants.Count != 1 || grants[0] != role.Id) throw new InvalidOperationException("Existing demo identity has unexpected access; no promotion allowed.");
                    continue; // Preserve profile, password, locks/activity and grants unchanged.
                }
                user = new User { Email = email, NormalizedEmail = email.ToUpperInvariant(), Username = email.Split('@')[0],
                    NormalizedUsername = email.Split('@')[0].ToUpperInvariant(), FullName = name,
                    DepartmentId = departments.Single(x => x.Code == "IT").Id };
                user.PasswordHash = passwords.Hash(user, password); db.Add(user); await db.SaveChangesAsync(ct);
                var link = new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow };
                db.Add(link); await db.SaveChangesAsync(ct);
                audit.Record("development.demo.seed", user, null, "SYSTEM"); audit.Record("development.demo.seed", link, null, "SYSTEM");
                users++; links++;
            }
            for (var i = 1; i <= 24; i++)
            {
                var code = Prefix + i.ToString("D3");
                if (await db.Set<Asset>().AnyAsync(x => x.AssetCode.ToLower() == code.ToLower(), ct)) continue;
                var type = types.Single(x => x.Code == typeCodes[(i - 1) % 8]);
                var dept = departments.Single(x => x.Code == deptCodes[((i - 1) / 8 + i - 1) % 4]);
                var purchase = new DateOnly(2026, 10, 3).AddDays(-45 - i * 25);
                var asset = new Asset { AssetCode = code, Name = $"Demo {type.Name} {i:D2}", AssetTypeId = type.Id,
                    OwningDepartmentId = dept.Id, SerialNumber = $"M1-DEMO-SERIAL-{i:D3}", Manufacturer = "Demo vendor",
                    Model = $"M1 sample {i:D2}", Specification = "Dữ liệu minh họa để diễn tập M1, không phải tài sản doanh nghiệp thật.",
                    OperatingSystem = type.Code is "LAPTOP" or "DESKTOP" ? "Windows 11 (demo)" : null,
                    Location = $"Phòng {dept.Name} — vị trí demo {i:D2}", PurchaseDate = purchase,
                    PurchaseCost = i % 6 == 0 ? null : i * 1250000m + .25m, WarrantyEndDate = purchase.AddMonths(i % 2 == 0 ? 12 : 36),
                    Notes = "M1-DEMO namespace; retained between runs. Không cấp phát hoặc bảo trì giả." };
                db.Add(asset); await db.SaveChangesAsync(ct);
                var correlation = Guid.NewGuid();
                db.Add(new AssetStatusHistory { AssetId = asset.Id, ToStatus = "IN_STOCK", Source = "SYSTEM",
                    Reason = "Create explicit M1 demo sample", ChangedAtUtc = asset.CreatedAtUtc, CorrelationId = correlation });
                audit.Record("development.demo.seed", asset, null, "SYSTEM", after: new { asset.AssetCode, asset.CurrentStatus });
                await db.SaveChangesAsync(ct);
                if (i % 8 == 0)
                {
                    asset.CurrentStatus = "RETIRED";
                    db.Add(new AssetStatusHistory { AssetId = asset.Id, FromStatus = "IN_STOCK", ToStatus = "RETIRED", Source = "SYSTEM",
                        Reason = "Explicit demo retirement; no assignment/maintenance workflow", ChangedAtUtc = DateTime.UtcNow, CorrelationId = correlation });
                    audit.Record("development.demo.retire", asset, null, "SYSTEM", before: new { CurrentStatus = "IN_STOCK" }, after: new { asset.CurrentStatus });
                    await db.SaveChangesAsync(ct);
                }
                assets++;
            }
            await db.SaveChangesAsync(ct); return (users, links, assets);
        }, ct);
        var demoIds = db.Set<Asset>().Where(x => x.AssetCode.StartsWith(Prefix)).Select(x => x.Id);
        return new(added.users, added.links, added.assets, await demoIds.CountAsync(ct),
            await db.Set<AssetStatusHistory>().CountAsync(x => demoIds.Contains(x.AssetId), ct));
    }
}
