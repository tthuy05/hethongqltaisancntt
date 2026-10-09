using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ItAssetManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBaselineV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maintenance_histories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    maintenance_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    from_assigned_to_user_id = table.Column<long>(type: "bigint", nullable: true),
                    to_assigned_to_user_id = table.Column<long>(type: "bigint", nullable: true),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    performed_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    performed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_histories", x => x.id);
                    table.CheckConstraint("ck_maintenance_histories_cost_nonnegative", "cost IS NULL OR (cost >= 0 AND cost <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_maintenance_histories_event_type", "event_type IN ('CREATED','STATUS_CHANGED','ASSIGNED','COMMENTED','COST_UPDATED','RESOLVED','FAILED','CANCELLED')");
                    table.CheckConstraint("ck_maintenance_histories_from_status", "from_status IN ('PENDING','IN_PROGRESS','RESOLVED','FAILED','CANCELLED')");
                    table.CheckConstraint("ck_maintenance_histories_to_status", "to_status IN ('PENDING','IN_PROGRESS','RESOLVED','FAILED','CANCELLED')");
                    table.ForeignKey(
                        name: "fk_maintenance_histories_from_assigned_to_user_id",
                        column: x => x.from_assigned_to_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_maintenance_histories_maintenance_ticket_id",
                        column: x => x.maintenance_ticket_id,
                        principalSchema: "public",
                        principalTable: "maintenance_tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_maintenance_histories_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_maintenance_histories_to_assigned_to_user_id",
                        column: x => x.to_assigned_to_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "replacement_rules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    asset_type_id = table.Column<long>(type: "bigint", nullable: true),
                    minimum_age_months = table.Column<int>(type: "integer", nullable: true),
                    maximum_maintenance_cost_ratio = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    minimum_maintenance_count = table.Column<int>(type: "integer", nullable: true),
                    require_warranty_expired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    minimum_failure_count = table.Column<int>(type: "integer", nullable: true),
                    estimated_unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_replacement_rules", x => x.id);
                    table.CheckConstraint("ck_replacement_rules_at_least_one_condition", "minimum_age_months IS NOT NULL OR maximum_maintenance_cost_ratio IS NOT NULL OR minimum_maintenance_count IS NOT NULL OR require_warranty_expired OR minimum_failure_count IS NOT NULL");
                    table.CheckConstraint("ck_replacement_rules_effective_period", "effective_to_utc IS NULL OR effective_to_utc > effective_from_utc");
                    table.CheckConstraint("ck_replacement_rules_estimated_unit_cost_nonnegative", "estimated_unit_cost IS NULL OR (estimated_unit_cost >= 0 AND estimated_unit_cost <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_replacement_rules_minimum_age_months_positive", "minimum_age_months IS NULL OR minimum_age_months > 0");
                    table.CheckConstraint("ck_replacement_rules_minimum_failure_count_positive", "minimum_failure_count IS NULL OR minimum_failure_count > 0");
                    table.CheckConstraint("ck_replacement_rules_minimum_maintenance_count_positive", "minimum_maintenance_count IS NULL OR minimum_maintenance_count > 0");
                    table.CheckConstraint("ck_replacement_rules_priority_nonnegative", "priority >= 0");
                    table.CheckConstraint("ck_replacement_rules_ratio", "maximum_maintenance_cost_ratio IS NULL OR maximum_maintenance_cost_ratio BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_replacement_rules_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_replacement_rules_version_positive", "version > 0");
                    table.ForeignKey(
                        name: "fk_replacement_rules_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "public",
                        principalTable: "asset_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "softwares",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    publisher = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_softwares", x => x.id);
                    table.CheckConstraint("ck_softwares_row_version_length", "octet_length(row_version) = 16");
                });

            migrationBuilder.CreateTable(
                name: "replacement_recommendations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    replacement_rule_id = table.Column<long>(type: "bigint", nullable: false),
                    disposition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE"),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    estimated_replacement_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    planned_replacement_year = table.Column<short>(type: "smallint", nullable: true),
                    evaluation_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    recommended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    disposition_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    disposition_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    disposition_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_replacement_recommendations", x => x.id);
                    table.CheckConstraint("ck_replacement_recommendations_archive_disposition", "NOT is_archived OR disposition <> 'ACTIVE'");
                    table.CheckConstraint("ck_replacement_recommendations_current_disposition", "NOT is_current OR (disposition IN ('ACTIVE','PLANNED') AND NOT is_archived)");
                    table.CheckConstraint("ck_replacement_recommendations_disposition", "disposition IN ('ACTIVE','PLANNED','DISMISSED','SUPERSEDED')");
                    table.CheckConstraint("ck_replacement_recommendations_disposition_actor", "disposition_by_user_id IS NULL OR disposition_at_utc IS NOT NULL");
                    table.CheckConstraint("ck_replacement_recommendations_disposition_evidence", "(disposition = 'ACTIVE' AND disposition_at_utc IS NULL) OR (disposition IN ('PLANNED','DISMISSED') AND disposition_at_utc IS NOT NULL AND disposition_by_user_id IS NOT NULL) OR (disposition = 'SUPERSEDED' AND disposition_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_replacement_recommendations_disposition_time", "disposition_at_utc IS NULL OR disposition_at_utc >= recommended_at_utc");
                    table.CheckConstraint("ck_replacement_recommendations_estimated_replacement_cost_nonn~", "estimated_replacement_cost IS NULL OR (estimated_replacement_cost >= 0 AND estimated_replacement_cost <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_replacement_recommendations_planned_year", "planned_replacement_year IS NULL OR planned_replacement_year BETWEEN 2000 AND 2100");
                    table.CheckConstraint("ck_replacement_recommendations_priority", "priority IN ('LOW','MEDIUM','HIGH','CRITICAL')");
                    table.CheckConstraint("ck_replacement_recommendations_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_replacement_recommendations_score_nonnegative", "score IS NULL OR (score >= 0 AND score <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_replacement_recommendations_snapshot_shape", "jsonb_typeof(evaluation_snapshot_json) IN ('object','array')");
                    table.ForeignKey(
                        name: "fk_replacement_recommendations_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_replacement_recommendations_disposition_by_user_id",
                        column: x => x.disposition_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_replacement_recommendations_replacement_rule_id",
                        column: x => x.replacement_rule_id,
                        principalSchema: "public",
                        principalTable: "replacement_rules",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "software_licenses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    software_id = table.Column<long>(type: "bigint", nullable: false),
                    license_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vendor = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    license_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_quantity = table.Column<int>(type: "integer", nullable: false),
                    license_key_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    license_key_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    key_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    purchased_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purchase_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_software_licenses", x => x.id);
                    table.CheckConstraint("ck_software_licenses_expiry", "expires_at_utc IS NULL OR starts_at_utc IS NULL OR expires_at_utc > starts_at_utc");
                    table.CheckConstraint("ck_software_licenses_key_last4", "license_key_last4 IS NULL OR char_length(license_key_last4) = 4");
                    table.CheckConstraint("ck_software_licenses_key_tuple", "(license_key_ciphertext IS NULL AND license_key_last4 IS NULL AND key_version IS NULL) OR (license_key_ciphertext IS NOT NULL AND license_key_last4 IS NOT NULL AND key_version IS NOT NULL)");
                    table.CheckConstraint("ck_software_licenses_purchase_cost_nonnegative", "purchase_cost IS NULL OR (purchase_cost >= 0 AND purchase_cost <> 'NaN'::numeric)");
                    table.CheckConstraint("ck_software_licenses_quantity_positive", "total_quantity > 0");
                    table.CheckConstraint("ck_software_licenses_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_software_licenses_type", "license_type IN ('PER_USER','PER_DEVICE','VOLUME','SUBSCRIPTION','OTHER')");
                    table.ForeignKey(
                        name: "fk_software_licenses_software_id",
                        column: x => x.software_id,
                        principalSchema: "public",
                        principalTable: "softwares",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "license_assignments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    software_license_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_user_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_asset_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    revoked_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_license_assignments", x => x.id);
                    table.CheckConstraint("ck_license_assignments_closed_before_archive", "NOT is_archived OR revoked_at_utc IS NOT NULL");
                    table.CheckConstraint("ck_license_assignments_revoked_actor", "revoked_by_user_id IS NULL OR revoked_at_utc IS NOT NULL");
                    table.CheckConstraint("ck_license_assignments_revoked_after_assigned", "revoked_at_utc IS NULL OR revoked_at_utc >= assigned_at_utc");
                    table.CheckConstraint("ck_license_assignments_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_license_assignments_target_xor", "(assigned_user_id IS NOT NULL AND assigned_asset_id IS NULL) OR (assigned_user_id IS NULL AND assigned_asset_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_license_assignments_assigned_asset_id",
                        column: x => x.assigned_asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_license_assignments_assigned_by_user_id",
                        column: x => x.assigned_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_license_assignments_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_license_assignments_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_license_assignments_software_license_id",
                        column: x => x.software_license_id,
                        principalSchema: "public",
                        principalTable: "software_licenses",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_license_assignments_asset_history",
                schema: "public",
                table: "license_assignments",
                columns: new[] { "assigned_asset_id", "assigned_at_utc", "id" },
                descending: new[] { false, true, true },
                filter: "assigned_asset_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_license_assignments_assigned_by_user_id",
                schema: "public",
                table: "license_assignments",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_license_assignments_license_active",
                schema: "public",
                table: "license_assignments",
                columns: new[] { "software_license_id", "revoked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_license_assignments_revoked_by_user_id",
                schema: "public",
                table: "license_assignments",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_license_assignments_user_history",
                schema: "public",
                table: "license_assignments",
                columns: new[] { "assigned_user_id", "assigned_at_utc", "id" },
                descending: new[] { false, true, true },
                filter: "assigned_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_license_assignments_active_asset",
                schema: "public",
                table: "license_assignments",
                columns: new[] { "software_license_id", "assigned_asset_id" },
                unique: true,
                filter: "revoked_at_utc IS NULL AND NOT is_archived AND assigned_asset_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_license_assignments_active_user",
                schema: "public",
                table: "license_assignments",
                columns: new[] { "software_license_id", "assigned_user_id" },
                unique: true,
                filter: "revoked_at_utc IS NULL AND NOT is_archived AND assigned_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_histories_correlation",
                schema: "public",
                table: "maintenance_histories",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_histories_from_assigned_to_user_id",
                schema: "public",
                table: "maintenance_histories",
                column: "from_assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_histories_performed_by_user_id",
                schema: "public",
                table: "maintenance_histories",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_histories_ticket_time",
                schema: "public",
                table: "maintenance_histories",
                columns: new[] { "maintenance_ticket_id", "performed_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_histories_to_assigned_to_user_id",
                schema: "public",
                table: "maintenance_histories",
                column: "to_assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_replacement_recommendations_asset",
                schema: "public",
                table: "replacement_recommendations",
                columns: new[] { "asset_id", "recommended_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_replacement_recommendations_budget",
                schema: "public",
                table: "replacement_recommendations",
                columns: new[] { "planned_replacement_year", "is_current", "asset_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "estimated_replacement_cost" });

            migrationBuilder.CreateIndex(
                name: "ix_replacement_recommendations_disposition_by_user_id",
                schema: "public",
                table: "replacement_recommendations",
                column: "disposition_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_replacement_recommendations_disposition_time",
                schema: "public",
                table: "replacement_recommendations",
                columns: new[] { "disposition", "priority", "recommended_at_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_replacement_recommendations_replacement_rule_id",
                schema: "public",
                table: "replacement_recommendations",
                column: "replacement_rule_id");

            migrationBuilder.CreateIndex(
                name: "uq_replacement_recommendations_current_asset",
                schema: "public",
                table: "replacement_recommendations",
                column: "asset_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "ix_replacement_rules_asset_type_id",
                schema: "public",
                table: "replacement_rules",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_replacement_rules_evaluation",
                schema: "public",
                table: "replacement_rules",
                columns: new[] { "is_active", "asset_type_id", "priority", "effective_from_utc", "effective_to_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_software_licenses_expiry",
                schema: "public",
                table: "software_licenses",
                column: "expires_at_utc",
                filter: "expires_at_utc IS NOT NULL AND is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_software_licenses_software_active",
                schema: "public",
                table: "software_licenses",
                columns: new[] { "software_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_softwares_active",
                schema: "public",
                table: "softwares",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_softwares_name_publisher",
                schema: "public",
                table: "softwares",
                columns: new[] { "name", "publisher" });

            migrationBuilder.Sql(FullSchemaDatabaseObjects.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Forward-only schema rollout. Restore to a new database after recovery review; never drop workflow history.");
        }
    }
}
