using System.Text.RegularExpressions;
using ItAssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItAssetManagement.Infrastructure.Data;

// Only the six remaining approved Baseline V1 tables. No workflow services are registered.
internal static class FullSchemaMapping
{
    private const string TicketStatuses = "'PENDING','IN_PROGRESS','RESOLVED','FAILED','CANCELLED'";
    public static void Configure(ModelBuilder model)
    {
        var h = Table<MaintenanceHistory>(model, "maintenance_histories");
        Strings(h, ("EventType",30), ("FromStatus",30), ("ToStatus",30), ("Comment",4000));
        Foreign<MaintenanceHistory,MaintenanceTicket>(h, "MaintenanceTicketId");
        foreach (var column in new[] { "FromAssignedToUserId", "ToAssignedToUserId", "PerformedByUserId" })
            Foreign<MaintenanceHistory,User>(h, column);
        h.Property(x => x.Cost).HasPrecision(18,2);
        h.Property(x => x.PerformedAtUtc).HasDefaultValueSql("clock_timestamp()");
        Check(h,"event_type","event_type IN ('CREATED','STATUS_CHANGED','ASSIGNED','COMMENTED','COST_UPDATED','RESOLVED','FAILED','CANCELLED')");
        Check(h,"from_status",$"from_status IN ({TicketStatuses})");
        Check(h,"to_status",$"to_status IN ({TicketStatuses})");
        Money(h,"cost");
        h.HasIndex(x => new { x.MaintenanceTicketId, x.PerformedAtUtc, x.Id }).HasDatabaseName("ix_maintenance_histories_ticket_time");
        h.HasIndex(x => x.CorrelationId).HasDatabaseName("ix_maintenance_histories_correlation");

        var s = Table<Software>(model,"softwares");
        Strings(s,("Code",50),("Name",250),("Publisher",250),("Version",100),("Description",2000));
        Mutable(s);
        s.HasIndex(x => new { x.Name, x.Publisher }).HasDatabaseName("ix_softwares_name_publisher");
        s.HasIndex(x => new { x.IsActive, x.Name }).HasDatabaseName("ix_softwares_active");

        var l = Table<SoftwareLicense>(model,"software_licenses");
        Strings(l,("LicenseCode",100),("Vendor",250),("LicenseType",30),("LicenseKeyLast4",4),("KeyVersion",50),("Notes",2000));
        Foreign<SoftwareLicense,Software>(l,"SoftwareId"); Mutable(l);
        l.Property(x => x.PurchaseCost).HasPrecision(18,2);
        Check(l,"type","license_type IN ('PER_USER','PER_DEVICE','VOLUME','SUBSCRIPTION','OTHER')");
        Check(l,"quantity_positive","total_quantity > 0");
        Check(l,"key_last4","license_key_last4 IS NULL OR char_length(license_key_last4) = 4");
        Check(l,"key_tuple","(license_key_ciphertext IS NULL AND license_key_last4 IS NULL AND key_version IS NULL) OR (license_key_ciphertext IS NOT NULL AND license_key_last4 IS NOT NULL AND key_version IS NOT NULL)");
        Check(l,"expiry","expires_at_utc IS NULL OR starts_at_utc IS NULL OR expires_at_utc > starts_at_utc");
        Money(l,"purchase_cost");
        l.HasIndex(x => new { x.SoftwareId, x.IsActive }).HasDatabaseName("ix_software_licenses_software_active");
        l.HasIndex(x => x.ExpiresAtUtc).HasFilter("expires_at_utc IS NOT NULL AND is_active = true").HasDatabaseName("ix_software_licenses_expiry");

        var a = Table<LicenseAssignment>(model,"license_assignments");
        Strings(a,("Notes",1000)); Mutable(a,active:false);
        Foreign<LicenseAssignment,SoftwareLicense>(a,"SoftwareLicenseId");
        Foreign<LicenseAssignment,Asset>(a,"AssignedAssetId");
        foreach (var column in new[] { "AssignedUserId", "AssignedByUserId", "RevokedByUserId" }) Foreign<LicenseAssignment,User>(a,column);
        a.Property(x => x.AssignedAtUtc).HasDefaultValueSql("clock_timestamp()");
        a.Property(x => x.IsArchived).HasDefaultValue(false);
        Check(a,"target_xor","(assigned_user_id IS NOT NULL AND assigned_asset_id IS NULL) OR (assigned_user_id IS NULL AND assigned_asset_id IS NOT NULL)");
        Check(a,"revoked_actor","revoked_by_user_id IS NULL OR revoked_at_utc IS NOT NULL");
        Check(a,"revoked_after_assigned","revoked_at_utc IS NULL OR revoked_at_utc >= assigned_at_utc");
        Check(a,"closed_before_archive","NOT is_archived OR revoked_at_utc IS NOT NULL");
        a.HasIndex(x => new { x.SoftwareLicenseId, x.RevokedAtUtc }).HasDatabaseName("ix_license_assignments_license_active");
        a.HasIndex(x => new { x.SoftwareLicenseId, x.AssignedUserId }).IsUnique()
            .HasFilter("revoked_at_utc IS NULL AND NOT is_archived AND assigned_user_id IS NOT NULL").HasDatabaseName("uq_license_assignments_active_user");
        a.HasIndex(x => new { x.SoftwareLicenseId, x.AssignedAssetId }).IsUnique()
            .HasFilter("revoked_at_utc IS NULL AND NOT is_archived AND assigned_asset_id IS NOT NULL").HasDatabaseName("uq_license_assignments_active_asset");
        a.HasIndex(x => new { x.AssignedUserId, x.AssignedAtUtc, x.Id }).IsDescending(false,true,true)
            .HasFilter("assigned_user_id IS NOT NULL").HasDatabaseName("ix_license_assignments_user_history");
        a.HasIndex(x => new { x.AssignedAssetId, x.AssignedAtUtc, x.Id }).IsDescending(false,true,true)
            .HasFilter("assigned_asset_id IS NOT NULL").HasDatabaseName("ix_license_assignments_asset_history");

        var r = Table<ReplacementRule>(model,"replacement_rules");
        Strings(r,("Code",100),("Name",250),("Description",2000)); Mutable(r);
        Foreign<ReplacementRule,AssetType>(r,"AssetTypeId");
        r.Property(x => x.Version).HasDefaultValue(1);
        r.Property(x => x.Priority).HasDefaultValue(100);
        r.Property(x => x.RequireWarrantyExpired).HasDefaultValue(false);
        r.Property(x => x.EffectiveFromUtc).HasDefaultValueSql("clock_timestamp()");
        r.Property(x => x.MaximumMaintenanceCostRatio).HasPrecision(5,4);
        r.Property(x => x.EstimatedUnitCost).HasPrecision(18,2);
        Check(r,"version_positive","version > 0");
        Check(r,"priority_nonnegative","priority >= 0");
        foreach (var column in new[] { "minimum_age_months", "minimum_maintenance_count", "minimum_failure_count" })
            Check(r,column+"_positive",$"{column} IS NULL OR {column} > 0");
        Check(r,"ratio","maximum_maintenance_cost_ratio IS NULL OR maximum_maintenance_cost_ratio BETWEEN 0 AND 1");
        Money(r,"estimated_unit_cost");
        Check(r,"effective_period","effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
        Check(r,"at_least_one_condition","minimum_age_months IS NOT NULL OR maximum_maintenance_cost_ratio IS NOT NULL OR minimum_maintenance_count IS NOT NULL OR require_warranty_expired OR minimum_failure_count IS NOT NULL");
        r.HasIndex(x => new { x.IsActive, x.AssetTypeId, x.Priority, x.EffectiveFromUtc, x.EffectiveToUtc }).HasDatabaseName("ix_replacement_rules_evaluation");

        var rec = Table<ReplacementRecommendation>(model,"replacement_recommendations");
        Strings(rec,("Disposition",30),("Priority",20),("Reason",2000),("DispositionNote",2000)); Mutable(rec,active:false);
        Foreign<ReplacementRecommendation,Asset>(rec,"AssetId");
        Foreign<ReplacementRecommendation,ReplacementRule>(rec,"ReplacementRuleId");
        Foreign<ReplacementRecommendation,User>(rec,"DispositionByUserId");
        rec.Property(x => x.Disposition).HasDefaultValue("ACTIVE");
        rec.Property(x => x.IsCurrent).HasDefaultValue(true);
        rec.Property(x => x.IsArchived).HasDefaultValue(false);
        rec.Property(x => x.RecommendedAtUtc).HasDefaultValueSql("clock_timestamp()");
        rec.Property(x => x.Score).HasPrecision(9,4);
        rec.Property(x => x.EstimatedReplacementCost).HasPrecision(18,2);
        rec.Property(x => x.EvaluationSnapshotJson).HasColumnType("jsonb");
        Check(rec,"disposition","disposition IN ('ACTIVE','PLANNED','DISMISSED','SUPERSEDED')");
        Check(rec,"priority","priority IN ('LOW','MEDIUM','HIGH','CRITICAL')");
        Money(rec,"score"); Money(rec,"estimated_replacement_cost");
        Check(rec,"planned_year","planned_replacement_year IS NULL OR planned_replacement_year BETWEEN 2000 AND 2100");
        Check(rec,"snapshot_shape","jsonb_typeof(evaluation_snapshot_json) IN ('object','array')");
        Check(rec,"disposition_time","disposition_at_utc IS NULL OR disposition_at_utc >= recommended_at_utc");
        Check(rec,"disposition_actor","disposition_by_user_id IS NULL OR disposition_at_utc IS NOT NULL");
        Check(rec,"disposition_evidence","(disposition = 'ACTIVE' AND disposition_at_utc IS NULL) OR (disposition IN ('PLANNED','DISMISSED') AND disposition_at_utc IS NOT NULL AND disposition_by_user_id IS NOT NULL) OR (disposition = 'SUPERSEDED' AND disposition_at_utc IS NOT NULL)");
        Check(rec,"current_disposition","NOT is_current OR (disposition IN ('ACTIVE','PLANNED') AND NOT is_archived)");
        Check(rec,"archive_disposition","NOT is_archived OR disposition <> 'ACTIVE'");
        rec.HasIndex(x => x.AssetId).IsUnique().HasFilter("is_current = true").HasDatabaseName("uq_replacement_recommendations_current_asset");
        rec.HasIndex(x => new { x.Disposition, x.Priority, x.RecommendedAtUtc }).IsDescending(false,false,true).HasDatabaseName("ix_replacement_recommendations_disposition_time");
        rec.HasIndex(x => new { x.AssetId, x.RecommendedAtUtc }).IsDescending(false,true).HasDatabaseName("ix_replacement_recommendations_asset");
        rec.HasIndex(x => new { x.PlannedReplacementYear, x.IsCurrent, x.AssetId }).IncludeProperties(x => x.EstimatedReplacementCost).HasDatabaseName("ix_replacement_recommendations_budget");
    }

    private static EntityTypeBuilder<T> Table<T>(ModelBuilder model,string table) where T : class
    {
        var builder = model.Entity<T>(); builder.ToTable(table,"public");
        builder.HasKey("Id").HasName("pk_"+table); builder.Property<long>("Id").UseIdentityByDefaultColumn(); return builder;
    }
    private static void Strings<T>(EntityTypeBuilder<T> b,params (string Name,int Length)[] columns) where T : class
    { foreach (var (name,length) in columns) b.Property(name).HasMaxLength(length); }
    private static void Check<T>(EntityTypeBuilder<T> b,string name,string sql) where T : class =>
        b.ToTable(t => t.HasCheckConstraint("ck_"+b.Metadata.GetTableName()+"_"+name,sql));
    private static void Money<T>(EntityTypeBuilder<T> b,string column) where T : class =>
        Check(b,column+"_nonnegative",$"{column} IS NULL OR ({column} >= 0 AND {column} <> 'NaN'::numeric)");
    private static void Foreign<T,TTarget>(EntityTypeBuilder<T> b,string column) where T : class where TTarget : class =>
        b.HasOne<TTarget>().WithMany().HasForeignKey(column).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_"+b.Metadata.GetTableName()+"_"+Regex.Replace(column,"([a-z0-9])([A-Z])","$1_$2").ToLowerInvariant());
    private static void Mutable<T>(EntityTypeBuilder<T> b,bool active=true) where T : class
    {
        b.Property<byte[]>("RowVersion").IsRequired().IsConcurrencyToken().ValueGeneratedNever();
        b.Property<DateTime>("CreatedAtUtc").HasDefaultValueSql("clock_timestamp()");
        if (active) b.Property<bool>("IsActive").HasDefaultValue(true);
        Check(b,"row_version_length","octet_length(row_version) = 16");
    }
}
