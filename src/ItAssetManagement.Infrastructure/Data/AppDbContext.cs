using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Infrastructure.Data;

// Writes require an audited transaction, including both stages of identity-key creation.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private bool _writing;
    private readonly HashSet<object> _written = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _audited = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<long> _historyAssets = [];
    protected override void OnModelCreating(ModelBuilder modelBuilder) => M1Model.Configure(modelBuilder);
    internal void BeginAuditedWrite()
    {
        if (_writing) throw new InvalidOperationException("Nested write transaction is not supported.");
        _writing = true; _written.Clear(); _audited.Clear(); _historyAssets.Clear();
    }
    internal void Cover(object entity) => _audited.Add(entity);
    internal void VerifyAuditCoverage()
    {
        if (_written.Any(x => !_audited.Contains(x)) || _historyAssets.Any(id => !_audited.OfType<Asset>().Any(a => a.Id == id)))
            throw new InvalidOperationException("Every business write requires a transactional audit event.");
        if (ChangeTracker.HasChanges()) throw new InvalidOperationException("Unsaved changes cannot be committed.");
    }
    internal void EndAuditedWrite(bool rollback)
    {
        _writing = false; _written.Clear(); _audited.Clear(); _historyAssets.Clear();
        if (rollback) ChangeTracker.Clear();
    }
    private void Prepare()
    {
        if (!_writing) throw new NotSupportedException("Business persistence requires an audited unit of work.");
        ChangeTracker.DetectChanges();
        foreach (var e in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Entity is AuditLog or AssetStatusHistory)
                throw new InvalidOperationException("Hard delete and append-only history mutation are forbidden.");
            if (e.Entity is AssetStatusHistory history) { _historyAssets.Add(history.AssetId); continue; }
            if (e.Entity is AuditLog) continue;
            _written.Add(e.Entity);
            if (e.Metadata.FindProperty("RowVersion") != null)
            {
                e.Property("RowVersion").CurrentValue = RandomNumberGenerator.GetBytes(16);
                if (e.State == EntityState.Modified) e.Property("RowVersion").IsModified = true;
            }
            if (e.State == EntityState.Added && e.Metadata.FindProperty("CreatedAtUtc") != null)
                e.Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow;
            if (e.State == EntityState.Modified && e.Metadata.FindProperty("UpdatedAtUtc") != null)
                e.Property("UpdatedAtUtc").CurrentValue = DateTime.UtcNow;
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { Prepare(); return base.SaveChanges(acceptAllChangesOnSuccess); }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { Prepare(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
