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
    private readonly HashSet<UserRole> _membershipDeletes = new(ReferenceEqualityComparer.Instance);
    protected override void OnModelCreating(ModelBuilder modelBuilder) => M1Model.Configure(modelBuilder);
    internal void BeginAuditedWrite()
    {
        if (_writing) throw new InvalidOperationException("Nested write transaction is not supported.");
        _writing = true; _written.Clear(); _audited.Clear(); _historyAssets.Clear(); _membershipDeletes.Clear();
    }
    internal void Cover(object entity) => _audited.Add(entity);
    internal void RemoveRoleMembership(UserRole membership)
    {
        if (!_writing) throw new InvalidOperationException("Role removal requires an audited transaction.");
        _membershipDeletes.Add(membership); Remove(membership);
    }
    internal void VerifyAuditCoverage()
    {
        if (_written.Any(x => !_audited.Contains(x)) || _historyAssets.Any(id => !_audited.OfType<Asset>().Any(a => a.Id == id)))
            throw new InvalidOperationException("Every business write requires a transactional audit event.");
        if (ChangeTracker.HasChanges()) throw new InvalidOperationException("Unsaved changes cannot be committed.");
    }
    internal void EndAuditedWrite(bool rollback)
    {
        _writing = false; _written.Clear(); _audited.Clear(); _historyAssets.Clear(); _membershipDeletes.Clear();
        if (rollback) ChangeTracker.Clear();
    }
    private void Prepare()
    {
        if (!_writing) throw new NotSupportedException("Business persistence requires an audited unit of work.");
        ChangeTracker.DetectChanges();
        foreach (var e in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            // Only explicitly approved mutable membership links may be removed. They
            // still enter _written and must have an audit event before commit.
            var membershipRemoval = e.Entity is UserRole link && _membershipDeletes.Contains(link);
            if (e.State == EntityState.Deleted && !membershipRemoval || e.State == EntityState.Modified && e.Entity is AuditLog or AssetStatusHistory or MaintenanceHistory)
                throw new InvalidOperationException("Hard delete and append-only history mutation are forbidden.");
            if (e.State == EntityState.Modified && e.Entity is AssetAssignment)
            {
                var immutable = new[] { "AssetId", "AssignedUserId", "AssignedDepartmentId", "AssignedAtUtc", "AssignedByUserId", "CreatedAtUtc" };
                if (immutable.Any(name => e.Property(name).IsModified))
                    throw new InvalidOperationException("Assignment identity/target/history cannot be rewritten.");
                if (e.OriginalValues.GetValue<DateTime?>("ReturnedAtUtc") is not null)
                {
                    var archiveOnly = !e.OriginalValues.GetValue<bool>("IsArchived") && e.CurrentValues.GetValue<bool>("IsArchived") &&
                        e.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "IsArchived" or "RowVersion");
                    if (!archiveOnly) throw new InvalidOperationException("Closed assignment history is immutable.");
                }
            }
            if (e.Entity is AssetStatusHistory history) { _historyAssets.Add(history.AssetId); continue; }
            if (e.State == EntityState.Modified && e.Entity is LicenseAssignment)
            {
                var immutable = new[] { "SoftwareLicenseId", "AssignedUserId", "AssignedAssetId", "AssignedAtUtc", "AssignedByUserId", "CreatedAtUtc" };
                if (immutable.Any(name => e.Property(name).IsModified))
                    throw new InvalidOperationException("License allocation identity/target/history cannot be rewritten.");
                if (e.OriginalValues.GetValue<DateTime?>("RevokedAtUtc") is not null)
                {
                    var archiveOnly = !e.OriginalValues.GetValue<bool>("IsArchived") && e.CurrentValues.GetValue<bool>("IsArchived") &&
                        e.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "IsArchived" or "RowVersion");
                    if (!archiveOnly) throw new InvalidOperationException("Revoked license allocation history is immutable.");
                }
            }
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
