using System.Text.RegularExpressions;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.Infrastructure.Mvp;

// Explicit operator task only. Never registered as a startup action or HTTP endpoint.
public sealed class AssetTypeCodeConversion(AppDbContext db, IAssetTypeCodeGenerator codes, IAuditWriter audit)
{
    public sealed record Change(long Id, string OldCode, string NewCode);
    public async Task<IReadOnlyList<Change>> RunAsync(CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Conversion requires a clean context outside a transaction.");
        db.ChangeTracker.Clear();
        var snapshot = await db.Set<AssetType>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var changes = new List<Change>();
        foreach (var type in snapshot.Where(x => !Regex.IsMatch(x.Code, "^LTS[0-9]{6,}$")))
            changes.Add(new(type.Id, type.Code, await codes.NextAsync(ct)));
        if (changes.Count == 0) return changes;
        // Allocation/sequence initialization must happen outside the business transaction.
        await new AuditedUnitOfWork(db).RunAsync(async () =>
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='30s'", ct);
            await new EfRepository(db).LockAsync("master-data", ct);
            var current = await db.Set<AssetType>().FromSqlRaw("SELECT * FROM public.asset_types ORDER BY id FOR UPDATE").ToListAsync(ct);
            if (snapshot.Count != current.Count || snapshot.Any(old => !current.Any(x => x.Id == old.Id && x.Code == old.Code && x.RowVersion.SequenceEqual(old.RowVersion))))
                throw new InvalidOperationException("Asset type baseline changed; review a fresh snapshot before retrying.");
            foreach (var change in changes)
            {
                var type = current.Single(x => x.Id == change.Id);
                type.Code = change.NewCode;
                audit.Record("asset-types.code.convert", type, null, "SYSTEM", before: new { Code = change.OldCode }, after: new { Code = change.NewCode });
            }
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return changes;
    }
}
