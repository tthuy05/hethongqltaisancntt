using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ItAssetManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset_assignments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_user_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_department_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    expected_return_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    returned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    returned_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    assignment_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    return_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_assignments", x => x.id);
                    table.CheckConstraint("ck_asset_assignments_closed_before_archive", "NOT is_archived OR returned_at_utc IS NOT NULL");
                    table.CheckConstraint("ck_asset_assignments_expected_after_assigned", "expected_return_at_utc IS NULL OR expected_return_at_utc >= assigned_at_utc");
                    table.CheckConstraint("ck_asset_assignments_returned_actor", "returned_by_user_id IS NULL OR returned_at_utc IS NOT NULL");
                    table.CheckConstraint("ck_asset_assignments_returned_after_assigned", "returned_at_utc IS NULL OR returned_at_utc >= assigned_at_utc");
                    table.CheckConstraint("ck_asset_assignments_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_asset_assignments_target_xor", "(assigned_user_id IS NOT NULL AND assigned_department_id IS NULL) OR (assigned_user_id IS NULL AND assigned_department_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_asset_assignments_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asset_assignments_assigned_by_user_id",
                        column: x => x.assigned_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asset_assignments_assigned_department_id",
                        column: x => x.assigned_department_id,
                        principalSchema: "public",
                        principalTable: "departments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asset_assignments_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_asset_assignments_returned_by_user_id",
                        column: x => x.returned_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "maintenance_tickets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "MEDIUM"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    requested_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_to_user_id = table.Column<long>(type: "bigint", nullable: true),
                    opened_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    due_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    estimated_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    actual_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_tickets", x => x.id);
                    table.CheckConstraint("ck_maintenance_tickets_actual_cost_nonnegative", "actual_cost IS NULL OR (actual_cost >= 0 AND actual_cost <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_maintenance_tickets_archive_terminal", "NOT is_archived OR status IN ('RESOLVED','FAILED','CANCELLED')");
                    table.CheckConstraint("ck_maintenance_tickets_due_after_opened", "due_at_utc IS NULL OR due_at_utc >= opened_at_utc");
                    table.CheckConstraint("ck_maintenance_tickets_estimated_cost_nonnegative", "estimated_cost IS NULL OR (estimated_cost >= 0 AND estimated_cost <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_maintenance_tickets_in_progress_requires_assignee", "status NOT IN ('IN_PROGRESS','RESOLVED','FAILED') OR (assigned_to_user_id IS NOT NULL AND started_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_maintenance_tickets_priority", "priority IN ('LOW','MEDIUM','HIGH','CRITICAL')");
                    table.CheckConstraint("ck_maintenance_tickets_resolved_after_started", "resolved_at_utc IS NULL OR resolved_at_utc >= COALESCE(started_at_utc, opened_at_utc)");
                    table.CheckConstraint("ck_maintenance_tickets_resolved_requires_resolution", "status NOT IN ('RESOLVED','FAILED') OR (resolution IS NOT NULL AND resolved_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_maintenance_tickets_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_maintenance_tickets_started_after_opened", "started_at_utc IS NULL OR started_at_utc >= opened_at_utc");
                    table.CheckConstraint("ck_maintenance_tickets_status", "status IN ('PENDING','IN_PROGRESS','RESOLVED','FAILED','CANCELLED')");
                    table.ForeignKey(
                        name: "fk_maintenance_tickets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_maintenance_tickets_assigned_to_user_id",
                        column: x => x.assigned_to_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_maintenance_tickets_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignments_asset_history",
                schema: "public",
                table: "asset_assignments",
                columns: new[] { "asset_id", "assigned_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignments_assigned_by_user_id",
                schema: "public",
                table: "asset_assignments",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignments_department_active",
                schema: "public",
                table: "asset_assignments",
                columns: new[] { "assigned_department_id", "returned_at_utc" },
                filter: "assigned_department_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignments_returned_by_user_id",
                schema: "public",
                table: "asset_assignments",
                column: "returned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_assignments_user_active",
                schema: "public",
                table: "asset_assignments",
                columns: new[] { "assigned_user_id", "returned_at_utc" },
                filter: "assigned_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_asset_assignments_one_active",
                schema: "public",
                table: "asset_assignments",
                column: "asset_id",
                unique: true,
                filter: "returned_at_utc IS NULL AND NOT is_archived");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_asset_status",
                schema: "public",
                table: "maintenance_tickets",
                columns: new[] { "asset_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_assignee_status",
                schema: "public",
                table: "maintenance_tickets",
                columns: new[] { "assigned_to_user_id", "status", "due_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_open",
                schema: "public",
                table: "maintenance_tickets",
                columns: new[] { "status", "opened_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_requested_by_user_id",
                schema: "public",
                table: "maintenance_tickets",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_maintenance_one_in_progress",
                schema: "public",
                table: "maintenance_tickets",
                column: "asset_id",
                unique: true,
                filter: "status = 'IN_PROGRESS' AND NOT is_archived");

            // EF has no expression-index metadata; preserve the documented case-insensitive business key.
            migrationBuilder.Sql("CREATE UNIQUE INDEX uq_maintenance_tickets_code ON public.maintenance_tickets (lower(ticket_code));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_assignments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "maintenance_tickets",
                schema: "public");
        }
    }
}
