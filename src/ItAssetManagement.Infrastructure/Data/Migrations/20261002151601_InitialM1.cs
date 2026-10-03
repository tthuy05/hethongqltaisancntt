using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ItAssetManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialM1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "asset_types",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_useful_life_months = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_types", x => x.id);
                    table.CheckConstraint("ck_asset_types_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_asset_types_useful_life_positive", "default_useful_life_months > 0");
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                    table.CheckConstraint("ck_permissions_row_version_length", "octet_length(row_version) = 16");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.CheckConstraint("ck_roles_row_version_length", "octet_length(row_version) = 16");
                });

            migrationBuilder.CreateTable(
                name: "asset_status_histories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    changed_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_status_histories", x => x.id);
                    table.CheckConstraint("ck_asset_status_histories_from_status", "from_status IN ('IN_STOCK','IN_USE','MAINTENANCE','BROKEN','RETIRED')");
                    table.CheckConstraint("ck_asset_status_histories_source", "source IN ('ASSIGNMENT','MAINTENANCE','ADMIN','IMPORT','SYSTEM')");
                    table.CheckConstraint("ck_asset_status_histories_status_changed", "from_status IS NULL OR from_status <> to_status");
                    table.CheckConstraint("ck_asset_status_histories_to_status", "to_status IN ('IN_STOCK','IN_USE','MAINTENANCE','BROKEN','RETIRED')");
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    asset_type_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    specification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    operating_system = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: true),
                    purchase_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    warranty_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    owning_department_id = table.Column<long>(type: "bigint", nullable: false),
                    current_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "IN_STOCK"),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    created_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.CheckConstraint("ck_assets_archive_time", "(NOT is_archived AND archived_at_utc IS NULL) OR (is_archived AND archived_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_assets_purchase_cost_nonnegative", "purchase_cost >= 0 AND purchase_cost <> 'NaN'::numeric");
                    table.CheckConstraint("ck_assets_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_assets_status", "current_status IN ('IN_STOCK','IN_USE','MAINTENANCE','BROKEN','RETIRED')");
                    table.CheckConstraint("ck_assets_warranty_date", "warranty_end_date >= purchase_date");
                    table.ForeignKey(
                        name: "fk_assets_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "public",
                        principalTable: "asset_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "USER"),
                    action = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    request_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    old_values_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_values_json = table.Column<string>(type: "jsonb", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    failure_reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    previous_entry_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    entry_hash = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_logs_actor_identity", "(actor_type = 'USER' AND actor_user_id IS NOT NULL) OR (actor_type IN ('SYSTEM','ANONYMOUS') AND actor_user_id IS NULL)");
                    table.CheckConstraint("ck_audit_logs_actor_type", "actor_type IN ('USER','SYSTEM','ANONYMOUS')");
                    table.CheckConstraint("ck_audit_logs_hash_length", "entry_hash IS NULL OR octet_length(entry_hash) = 32");
                    table.CheckConstraint("ck_audit_logs_metadata_json_shape", "metadata_json IS NULL OR jsonb_typeof(metadata_json) IN ('object','array')");
                    table.CheckConstraint("ck_audit_logs_new_values_json_shape", "new_values_json IS NULL OR jsonb_typeof(new_values_json) IN ('object','array')");
                    table.CheckConstraint("ck_audit_logs_old_values_json_shape", "old_values_json IS NULL OR jsonb_typeof(old_values_json) IN ('object','array')");
                    table.CheckConstraint("ck_audit_logs_outcome", "outcome IN ('SUCCESS','FAILURE','DENIED')");
                    table.CheckConstraint("ck_audit_logs_previous_hash_length", "previous_entry_hash IS NULL OR octet_length(previous_entry_hash) = 32");
                });

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_department_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    created_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                    table.CheckConstraint("ck_departments_parent_not_self", "parent_department_id <> id");
                    table.CheckConstraint("ck_departments_row_version_length", "octet_length(row_version) = 16");
                    table.ForeignKey(
                        name: "fk_departments_parent_department_id",
                        column: x => x.parent_department_id,
                        principalSchema: "public",
                        principalTable: "departments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employee_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    department_id = table.Column<long>(type: "bigint", nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_admin_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    admin_locked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lockout_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    token_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_login_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    created_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_admin_lock_time", "(NOT is_admin_locked AND admin_locked_at_utc IS NULL) OR (is_admin_locked AND admin_locked_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_users_failed_login_nonnegative", "failed_login_count >= 0");
                    table.CheckConstraint("ck_users_row_version_length", "octet_length(row_version) = 16");
                    table.CheckConstraint("ck_users_token_version_nonnegative", "token_version >= 0");
                    table.ForeignKey(
                        name: "fk_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_users_department_id",
                        column: x => x.department_id,
                        principalSchema: "public",
                        principalTable: "departments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false),
                    granted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    granted_by_user_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_permissions_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_role_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "public",
                        principalTable: "permissions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_role_permissions_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "roles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    assigned_by_user_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_roles_assigned_by_user_id",
                        column: x => x.assigned_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_user_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "roles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_user_roles_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_status_histories_asset_time",
                schema: "public",
                table: "asset_status_histories",
                columns: new[] { "asset_id", "changed_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_asset_status_histories_changed_by_user_id",
                schema: "public",
                table: "asset_status_histories",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_status_histories_correlation",
                schema: "public",
                table: "asset_status_histories",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_types_active_name",
                schema: "public",
                table: "asset_types",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_archived",
                schema: "public",
                table: "assets",
                columns: new[] { "is_archived", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_created_by_user_id",
                schema: "public",
                table: "assets",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_department_status",
                schema: "public",
                table: "assets",
                columns: new[] { "owning_department_id", "current_status" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_type_status",
                schema: "public",
                table: "assets",
                columns: new[] { "asset_type_id", "current_status" })
                .Annotation("Npgsql:IndexInclude", new[] { "asset_code", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_updated_by_user_id",
                schema: "public",
                table: "assets",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_outcome",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "action", "outcome", "occurred_at_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_time",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_correlation",
                schema: "public",
                table: "audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "occurred_at_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_time",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "occurred_at_utc", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_departments_active_name",
                schema: "public",
                table: "departments",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_departments_created_by_user_id",
                schema: "public",
                table: "departments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_parent",
                schema: "public",
                table: "departments",
                column: "parent_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_updated_by_user_id",
                schema: "public",
                table: "departments",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_module_active",
                schema: "public",
                table: "permissions",
                columns: new[] { "module", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_granted_by_user_id",
                schema: "public",
                table: "role_permissions",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission",
                schema: "public",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "uq_role_permissions_role_permission",
                schema: "public",
                table: "role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_active_name",
                schema: "public",
                table: "roles",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_assigned_by_user_id",
                schema: "public",
                table: "user_roles",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role",
                schema: "public",
                table: "user_roles",
                columns: new[] { "role_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "uq_user_roles_user_role",
                schema: "public",
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_created_by_user_id",
                schema: "public",
                table: "users",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_department_active",
                schema: "public",
                table: "users",
                columns: new[] { "department_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_users_updated_by_user_id",
                schema: "public",
                table: "users",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_users_normalized_email",
                schema: "public",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_normalized_username",
                schema: "public",
                table: "users",
                column: "normalized_username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_asset_status_histories_asset_id",
                schema: "public",
                table: "asset_status_histories",
                column: "asset_id",
                principalSchema: "public",
                principalTable: "assets",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_asset_status_histories_changed_by_user_id",
                schema: "public",
                table: "asset_status_histories",
                column: "changed_by_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_created_by_user_id",
                schema: "public",
                table: "assets",
                column: "created_by_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_updated_by_user_id",
                schema: "public",
                table: "assets",
                column: "updated_by_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_owning_department_id",
                schema: "public",
                table: "assets",
                column: "owning_department_id",
                principalSchema: "public",
                principalTable: "departments",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_actor_user_id",
                schema: "public",
                table: "audit_logs",
                column: "actor_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_departments_created_by_user_id",
                schema: "public",
                table: "departments",
                column: "created_by_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_departments_updated_by_user_id",
                schema: "public",
                table: "departments",
                column: "updated_by_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.Sql(M1DatabaseObjects.CreateSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Initial setup is forward-only: never use rollback to erase the shared database.
            throw new NotSupportedException("InitialM1 rollback is disabled; use a reviewed forward migration.");
        }
    }
}
