using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

public sealed record AssignmentMaintenanceSetupResult(string Status, string Database, string Migration,
    IReadOnlyList<string> Checks, NeonSchemaInspectionResult? Schema = null, int? SeedAdded = null,
    int? SeedReapplyAdded = null, string? FailureCode = null);

// Explicit Development CLI only. No HTTP/startup migration and no new database creation.
public static class NeonAssignmentMaintenanceSetup
{
    public const string InitialMigration = "20261002151601_InitialM1";
    public const string Migration = "20261008080630_AddAssignmentMaintenance";
    public const string ValidationDatabase = "it_asset_management_m1_verify_20261002";
    public static readonly string[] NewTables = ["asset_assignments", "maintenance_tickets"];
    internal const string TicketIndexSql = "CREATE UNIQUE INDEX uq_maintenance_tickets_code ON public.maintenance_tickets (lower(ticket_code));";

    public static async Task<AssignmentMaintenanceSetupResult> RunAsync(string? secret, bool validationOnly,
        bool sharedBackendCompatibilityConfirmed = false)
    {
        var target = validationOnly ? ValidationDatabase : "neondb";
        var checks = new List<string>();
        if (!NeonConnectionPolicy.TryCreate(secret, out var settings) || settings!.Database != "neondb")
            return new("Refused", target, Migration, checks, FailureCode: "INVALID_NEON_TARGET_OR_CONFIGURATION");
        // An operator must verify/deploy the backward-compatible archive/retire query
        // first. Default refusal happens before even opening the shared connection.
        if (!validationOnly && !sharedBackendCompatibilityConfirmed)
            return new("Refused", target, Migration, checks, FailureCode: "SHARED_BACKEND_COMPATIBILITY_REVIEW_REQUIRED");
        settings.Host = settings.Host!.Replace("-pooler.", ".", StringComparison.OrdinalIgnoreCase);
        settings.Pooling = false; settings.CommandTimeout = 60; settings.ApplicationName = "ItAssetManagement.AssignmentSetup";
        try
        {
            // Same lock as InitialM1 setup; hold the direct shared session throughout.
            await using var coordinator = new NpgsqlConnection(settings.ConnectionString);
            await coordinator.OpenAsync();
            await using var changeLock = new NpgsqlCommand("SELECT pg_try_advisory_lock(837112, 100)", coordinator);
            if (!Equals(await changeLock.ExecuteScalarAsync(), true))
                return new("Refused", target, Migration, checks, FailureCode: "DATABASE_CHANGE_LOCK_BUSY");
            checks.Add("DIRECT_TLS_AND_CHANGE_LOCK_PASS");
            var targetSettings = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Database = target };
            await using var connection = new NpgsqlConnection(targetSettings.ConnectionString);
            await connection.OpenAsync();
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection,
                p => p.MigrationsHistoryTable("ef_migrations_history", "public")).Options);
            ValidateMigration(db);
            checks.Add("ADDITIVE_MIGRATION_2_TABLES_8_FKS_17_CHECKS_12_INDEXES_PASS");
            await VerifyCatalogAsync(connection, db);
            checks.Add("EXISTING_RECOGNIZED_SCHEMA_AND_HISTORY_PASS");
            var beforeSchema = await ExistingSchemaFingerprintAsync(connection);
            var beforeRows = await ExistingRowsFingerprintAsync(connection);
            await db.GetService<IMigrator>().MigrateAsync(Migration);
            await VerifyCatalogAsync(connection, db, requireExtended: true);
            if (beforeSchema != await ExistingSchemaFingerprintAsync(connection)) throw new InvalidOperationException("EXISTING_SCHEMA_CHANGED");
            if (beforeRows != await ExistingRowsFingerprintAsync(connection)) throw new InvalidOperationException("EXISTING_ROWS_CHANGED_OR_CONCURRENT_WRITE");
            checks.Add("EXISTING_10_TABLES_METADATA_AND_DATA_UNCHANGED_PASS");
            await db.GetService<IMigrator>().MigrateAsync(Migration);
            await VerifyCatalogAsync(connection, db, requireExtended: true);
            checks.Add("MIGRATION_REAPPLY_NO_PENDING_PASS");
            if (validationOnly) await AssignmentMaintenanceConstraintVerification.RunAsync(connection, checks);

            var actor = new SetupActor(); var repo = new EfRepository(db); var uow = new AuditedUnitOfWork(db);
            var seed = new DevelopmentSeed(db, repo, uow, new PasswordService(), new AuditWriter(db, actor));
            var added = await seed.RunAssignmentPermissionsAsync();
            var reapply = await seed.RunAssignmentPermissionsAsync();
            if (reapply != 0) throw new InvalidOperationException("PERMISSION_SEED_NOT_IDEMPOTENT");
            await VerifyPermissionsAsync(connection);
            checks.Add("ADMIN_MANAGER_ASSIGNMENT_GRANTS_AND_SEED_REAPPLY_PASS");
            var schema = await NeonSchemaInspection.CheckAsync(secret, validationOnly ? ValidationDatabase : null);
            if (schema.Status != "Verified" || schema.PrimaryKeys != 12 || schema.ForeignKeys != 27)
                throw new InvalidOperationException("POST_APPLY_INSPECTION_FAILED");
            checks.Add("PHYSICAL_12_TABLES_27_FKS_44_CHECKS_62_INDEXES_PASS");
            return new("Verified", target, Migration, checks, schema, added, reapply);
        }
        catch (Exception error) when (error is DbException or TimeoutException or IOException or InvalidOperationException)
        {
            // Never disclose exception messages, credentials or connection details.
            return new("Failed", target, Migration, checks, FailureCode: error is PostgresException pg ? "SQLSTATE_" + pg.SqlState :
                error is InvalidOperationException && SafeCodes.Contains(error.Message) ? error.Message : error.GetType().Name);
        }
    }

    private static readonly HashSet<string> SafeCodes = ["MIGRATION_SCOPE_MISMATCH", "SCHEMA_OR_HISTORY_MISMATCH",
        "EXISTING_SCHEMA_CHANGED", "EXISTING_ROWS_CHANGED_OR_CONCURRENT_WRITE", "PERMISSION_SEED_NOT_IDEMPOTENT",
        "PERMISSION_GRANTS_MISMATCH", "POST_APPLY_INSPECTION_FAILED", "CONSTRAINT_EXPECTATION_FAILED", "ISOLATION_REQUIRED"];

    internal static void ValidateMigration(AppDbContext db)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        if (!db.Database.GetMigrations().SequenceEqual([InitialMigration, Migration]) || db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("MIGRATION_SCOPE_MISMATCH");
        var migration = assembly.CreateMigration(assembly.Migrations[Migration], db.Database.ProviderName!);
        var creates = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToArray();
        var sql = migration.UpOperations.OfType<SqlOperation>().ToArray();
        if (creates.Length != 2 || !creates.Select(x => x.Name).Order().SequenceEqual(NewTables.Order()) ||
            creates.Any(x => x.Schema != "public") || creates.Sum(x => x.ForeignKeys.Count) != 8 ||
            creates.Sum(x => x.CheckConstraints.Count) != 17 || indexes.Length != 11 ||
            indexes.Any(x => x.Schema != "public" || !NewTables.Contains(x.Table)) || sql.Length != 1 ||
            sql[0].Sql != TicketIndexSql || sql[0].SuppressTransaction || migration.UpOperations.Count != 14)
            throw new InvalidOperationException("MIGRATION_SCOPE_MISMATCH");
    }

    private static async Task VerifyCatalogAsync(NpgsqlConnection connection, AppDbContext db, bool requireExtended = false)
    {
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var extended = history.SequenceEqual([InitialMigration, Migration]);
        if (!(extended || !requireExtended && history.SequenceEqual([InitialMigration])))
            throw new InvalidOperationException("SCHEMA_OR_HISTORY_MISMATCH");
        await using var command = new NpgsqlCommand("""
            SELECT ARRAY(SELECT schemaname || '.' || tablename FROM pg_tables
                WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY schemaname,tablename),
              (SELECT count(*)::int FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='p' AND conrelid<>'public.ef_migrations_history'::regclass),
              (SELECT count(*)::int FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f' AND confdeltype='a'),
              (SELECT count(*)::int FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='c'),
              (SELECT count(*)::int FROM pg_indexes WHERE schemaname='public' AND tablename<>'ef_migrations_history'),
              (SELECT count(*)::int FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('trg_audit_logs_append_only','trg_asset_status_histories_append_only')),
              (SELECT count(*)::int FROM information_schema.columns WHERE table_schema='public' AND column_name='row_version' AND data_type='bytea' AND is_nullable='NO')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        var tables = NeonM1Setup.Tables.Concat(extended ? NewTables : []).Append("ef_migrations_history").Select(x => "public." + x).Order();
        if (!reader.GetFieldValue<string[]>(0).Order().SequenceEqual(tables) || reader.GetInt32(1) != (extended ? 12 : 10) ||
            reader.GetInt32(2) != (extended ? 27 : 19) || reader.GetInt32(3) != (extended ? 44 : 27) ||
            reader.GetInt32(4) != (extended ? 62 : 48) || reader.GetInt32(5) != 2 || reader.GetInt32(6) != (extended ? 8 : 6))
            throw new InvalidOperationException("SCHEMA_OR_HISTORY_MISMATCH");
    }

    private static async Task<string> ExistingSchemaFingerprintAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT value FROM (
              SELECT 'column|' || table_name || '|' || column_name || '|' || data_type || '|' || is_nullable || '|' ||
                COALESCE(column_default,'') || '|' || COALESCE(character_maximum_length::text,'') || '|' ||
                COALESCE(numeric_precision::text,'') || '|' || COALESCE(numeric_scale::text,'') || '|' || is_identity AS value
                FROM information_schema.columns WHERE table_schema='public' AND table_name=ANY(@tables)
              UNION ALL SELECT 'constraint|' || c.relname || '|' || k.conname || '|' || pg_get_constraintdef(k.oid)
                FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid WHERE k.connamespace='public'::regnamespace AND c.relname=ANY(@tables)
              UNION ALL SELECT 'index|' || tablename || '|' || indexdef FROM pg_indexes WHERE schemaname='public' AND tablename=ANY(@tables)
              UNION ALL SELECT 'trigger|' || c.relname || '|' || pg_get_triggerdef(t.oid) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid
                WHERE NOT t.tgisinternal AND c.relnamespace='public'::regnamespace AND c.relname=ANY(@tables)
            ) s ORDER BY value
            """, connection);
        command.Parameters.AddWithValue("tables", NeonM1Setup.Tables);
        var builder = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) builder.AppendLine(reader.GetString(0));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static async Task<string> ExistingRowsFingerprintAsync(NpgsqlConnection connection)
    {
        // Hash on the server, never return actual row values or credential hashes to CLI/logs.
        var sql = string.Join(" UNION ALL ", NeonM1Setup.Tables.Select(table =>
            $"SELECT '{table}',count(*),md5(COALESCE(string_agg(md5(row_to_json(t)::text),'' ORDER BY id),'')) FROM public.{table} t"));
        await using var command = new NpgsqlCommand(sql, connection);
        var builder = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) builder.Append(reader.GetString(0)).Append(':').Append(reader.GetInt64(1)).Append(':').Append(reader.GetString(2)).Append(';');
        return builder.ToString();
    }

    private static async Task VerifyPermissionsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM public.role_permissions rp JOIN public.roles r ON r.id=rp.role_id JOIN public.permissions p ON p.id=rp.permission_id
            WHERE r.code IN ('ADMIN_IT','SYSTEM_MANAGER') AND r.is_active AND p.is_active AND p.code=ANY(@codes)
            """, connection);
        command.Parameters.AddWithValue("codes", new[] { Permissions.AssignmentRead, Permissions.AssignmentAssign, Permissions.AssignmentReturn });
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 6) throw new InvalidOperationException("PERMISSION_GRANTS_MISMATCH");
    }

    private sealed class SetupActor : IActor
    {
        public long? UserId => null; public bool Has(string permission) => false;
        public Guid CorrelationId { get; } = Guid.NewGuid(); public string? Method => null; public string? Path => null;
    }
}
