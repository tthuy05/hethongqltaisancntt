using ItAssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItAssetManagement.Infrastructure.Data;

// Thiện's Week 4 proposal integrated by Thủy. Configure before the shared snake-case pass.
// Only the two handed-off baseline tables; MaintenanceHistory/workflow remains planned.
internal static class AssignmentMaintenanceMappingProposal
{
    private const string TicketStatuses = "'PENDING','IN_PROGRESS','RESOLVED','FAILED','CANCELLED'";
    private const string Priorities = "'LOW','MEDIUM','HIGH','CRITICAL'";

    public static void Configure(ModelBuilder model)
    {
        // --- asset_assignments ---
        var aa = model.Entity<AssetAssignment>();
        aa.ToTable("asset_assignments", "public");
        aa.HasKey("Id").HasName("pk_asset_assignments");
        aa.Property<long>("Id").UseIdentityByDefaultColumn();

        aa.Property(x => x.AssignmentNote).HasMaxLength(1000);
        aa.Property(x => x.ReturnNote).HasMaxLength(1000);

        aa.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asset_assignments_asset_id");
        aa.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asset_assignments_assigned_user_id");
        aa.HasOne<Department>().WithMany().HasForeignKey(x => x.AssignedDepartmentId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asset_assignments_assigned_department_id");
        aa.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asset_assignments_assigned_by_user_id");
        aa.HasOne<User>().WithMany().HasForeignKey(x => x.ReturnedByUserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_asset_assignments_returned_by_user_id");

        aa.Property<byte[]>("RowVersion").IsRequired().IsConcurrencyToken().ValueGeneratedNever();
        aa.Property(x => x.CreatedAtUtc).HasDefaultValueSql("clock_timestamp()");
        aa.Property(x => x.AssignedAtUtc).HasDefaultValueSql("clock_timestamp()");
        aa.Property(x => x.IsArchived).HasDefaultValue(false);

        aa.ToTable(t =>
        {
            t.HasCheckConstraint("ck_asset_assignments_row_version_length", "octet_length(row_version) = 16");
            t.HasCheckConstraint("ck_asset_assignments_target_xor",
                "(assigned_user_id IS NOT NULL AND assigned_department_id IS NULL) OR (assigned_user_id IS NULL AND assigned_department_id IS NOT NULL)");
            t.HasCheckConstraint("ck_asset_assignments_returned_actor",
                "returned_by_user_id IS NULL OR returned_at_utc IS NOT NULL");
            t.HasCheckConstraint("ck_asset_assignments_closed_before_archive",
                "NOT is_archived OR returned_at_utc IS NOT NULL");
            t.HasCheckConstraint("ck_asset_assignments_expected_after_assigned",
                "expected_return_at_utc IS NULL OR expected_return_at_utc >= assigned_at_utc");
            t.HasCheckConstraint("ck_asset_assignments_returned_after_assigned",
                "returned_at_utc IS NULL OR returned_at_utc >= assigned_at_utc");
        });

        aa.HasIndex(x => x.AssetId)
            .HasFilter("returned_at_utc IS NULL AND NOT is_archived")
            .IsUnique()
            .HasDatabaseName("uq_asset_assignments_one_active");
        aa.HasIndex(x => new { x.AssignedUserId, x.ReturnedAtUtc }).HasFilter("assigned_user_id IS NOT NULL").HasDatabaseName("ix_asset_assignments_user_active");
        aa.HasIndex(x => new { x.AssignedDepartmentId, x.ReturnedAtUtc }).HasFilter("assigned_department_id IS NOT NULL").HasDatabaseName("ix_asset_assignments_department_active");
        aa.HasIndex(x => new { x.AssetId, x.AssignedAtUtc }).IsDescending(false, true).HasDatabaseName("ix_asset_assignments_asset_history");

        // --- maintenance_tickets ---
        var mt = model.Entity<MaintenanceTicket>();
        mt.ToTable("maintenance_tickets", "public");
        mt.HasKey("Id").HasName("pk_maintenance_tickets");
        mt.Property<long>("Id").UseIdentityByDefaultColumn();

        mt.Property(x => x.TicketCode).HasMaxLength(50);
        mt.Property(x => x.Title).HasMaxLength(250);
        mt.Property(x => x.Description).HasMaxLength(4000);
        mt.Property(x => x.Priority).HasMaxLength(20);
        mt.Property(x => x.Status).HasMaxLength(30);
        mt.Property(x => x.Resolution).HasMaxLength(4000);
        mt.Property(x => x.EstimatedCost).HasPrecision(18, 2);
        mt.Property(x => x.ActualCost).HasPrecision(18, 2);

        mt.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_maintenance_tickets_asset_id");
        mt.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_maintenance_tickets_requested_by_user_id");
        mt.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_maintenance_tickets_assigned_to_user_id");

        mt.Property<byte[]>("RowVersion").IsRequired().IsConcurrencyToken().ValueGeneratedNever();
        mt.Property(x => x.CreatedAtUtc).HasDefaultValueSql("clock_timestamp()");
        mt.Property(x => x.OpenedAtUtc).HasDefaultValueSql("clock_timestamp()");
        mt.Property(x => x.Priority).HasDefaultValue("MEDIUM");
        mt.Property(x => x.Status).HasDefaultValue("PENDING");
        mt.Property(x => x.IsArchived).HasDefaultValue(false);

        mt.ToTable(t =>
        {
            t.HasCheckConstraint("ck_maintenance_tickets_row_version_length", "octet_length(row_version) = 16");
            t.HasCheckConstraint("ck_maintenance_tickets_status", $"status IN ({TicketStatuses})");
            t.HasCheckConstraint("ck_maintenance_tickets_priority", $"priority IN ({Priorities})");
            t.HasCheckConstraint("ck_maintenance_tickets_started_after_opened",
                "started_at_utc IS NULL OR started_at_utc >= opened_at_utc");
            t.HasCheckConstraint("ck_maintenance_tickets_due_after_opened",
                "due_at_utc IS NULL OR due_at_utc >= opened_at_utc");
            t.HasCheckConstraint("ck_maintenance_tickets_resolved_after_started",
                "resolved_at_utc IS NULL OR resolved_at_utc >= COALESCE(started_at_utc, opened_at_utc)");
            t.HasCheckConstraint("ck_maintenance_tickets_estimated_cost_nonnegative",
                "estimated_cost IS NULL OR (estimated_cost >= 0 AND estimated_cost <> 'NaN'::numeric)");
            t.HasCheckConstraint("ck_maintenance_tickets_actual_cost_nonnegative",
                "actual_cost IS NULL OR (actual_cost >= 0 AND actual_cost <> 'NaN'::numeric)");
            t.HasCheckConstraint("ck_maintenance_tickets_resolved_requires_resolution",
                "status NOT IN ('RESOLVED','FAILED') OR (resolution IS NOT NULL AND resolved_at_utc IS NOT NULL)");
            t.HasCheckConstraint("ck_maintenance_tickets_in_progress_requires_assignee",
                "status NOT IN ('IN_PROGRESS','RESOLVED','FAILED') OR (assigned_to_user_id IS NOT NULL AND started_at_utc IS NOT NULL)");
            t.HasCheckConstraint("ck_maintenance_tickets_archive_terminal",
                "NOT is_archived OR status IN ('RESOLVED','FAILED','CANCELLED')");
        });

        // lower(ticket_code) unique index is migration-owned, like the M1 code indexes.
        mt.HasIndex(x => x.AssetId)
            .HasFilter("status = 'IN_PROGRESS' AND NOT is_archived")
            .IsUnique()
            .HasDatabaseName("uq_maintenance_one_in_progress");
        mt.HasIndex(x => new { x.AssetId, x.Status }).HasDatabaseName("ix_maintenance_tickets_asset_status");
        mt.HasIndex(x => new { x.AssignedToUserId, x.Status, x.DueAtUtc }).HasDatabaseName("ix_maintenance_tickets_assignee_status");
        mt.HasIndex(x => new { x.Status, x.OpenedAtUtc }).IsDescending(false, true).HasDatabaseName("ix_maintenance_tickets_open");
    }
}
