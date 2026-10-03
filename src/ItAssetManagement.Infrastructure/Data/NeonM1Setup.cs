using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

public sealed record NeonM1SetupResult(string Status, string? Database = null, string? ValidationDatabase = null,
    string? Migration = null, int Tables = 0, int ForeignKeys = 0, IReadOnlyList<string>? Checks = null, string? FailureCode = null);

// Explicit Development CLI operation only; never called by HTTP or normal API startup.
public static class NeonM1Setup
{
    public static readonly string[] Tables = ["departments", "users", "roles", "user_roles", "permissions",
        "role_permissions", "asset_types", "assets", "asset_status_histories", "audit_logs"];

    public static async Task<NeonM1SetupResult> RunAsync(string? secret, string target, string validationDatabase)
    {
        if (!NeonConnectionPolicy.TryCreate(secret, out var settings) || settings!.Database != target ||
            !Regex.IsMatch(validationDatabase, "\\Ait_asset_management_m1_verify_[a-z0-9_]{1,24}\\z") || target == validationDatabase)
            return new("Refused", FailureCode: "INVALID_TARGET_OR_CONFIGURATION");
        // Neon documents -pooler as the same endpoint's pooling suffix. Verify the direct endpoint by TLS/auth.
        settings.Host = settings.Host!.Replace("-pooler.", ".", StringComparison.OrdinalIgnoreCase);
        settings.Pooling = false;
        settings.CommandTimeout = 60;
        settings.ApplicationName = "ItAssetManagement.M1Setup";
        var checks = new List<string>();
        try
        {
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await using var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_lock(837112, 100)", connection);
            if (!Equals(await lockCommand.ExecuteScalarAsync(), true)) return new("Refused", FailureCode: "DATABASE_CHANGE_LOCK_BUSY");
            checks.Add("DIRECT_TLS_AND_CHANGE_LOCK_PASS");
            await using var targetContext = Context(connection);
            var migration = targetContext.Database.GetMigrations().Single();
            await CheckTargetAsync(connection, targetContext, migration);
            checks.Add("TARGET_EMPTY_OR_EXACT_MIGRATION_PASS");

            // Do not reuse an existing database: isolation must be proven by fresh creation.
            await using (var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)", connection))
            {
                exists.Parameters.AddWithValue("name", validationDatabase);
                if (Equals(await exists.ExecuteScalarAsync(), true)) return new("Refused", FailureCode: "VALIDATION_DATABASE_ALREADY_EXISTS");
            }
            // Identifier is strictly allowlisted above, not interpolated from a connection secret.
            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{validationDatabase}\" TEMPLATE template0", connection))
                await create.ExecuteNonQueryAsync();
            var isolatedSettings = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Database = validationDatabase };
            await using (var isolatedConnection = new NpgsqlConnection(isolatedSettings.ConnectionString))
            {
                await isolatedConnection.OpenAsync();
                await using var isolated = Context(isolatedConnection);
                await isolated.Database.MigrateAsync();
                await VerifyCatalogAsync(isolatedConnection, checks, "ISOLATED");
                await M1ConstraintVerification.RunAsync(isolatedConnection, checks);
                await isolated.Database.MigrateAsync();
                if ((await isolated.Database.GetAppliedMigrationsAsync()).Single() != migration)
                    throw new InvalidOperationException("MIGRATION_HISTORY_MISMATCH");
                checks.Add("ISOLATED_MIGRATION_REAPPLY_PASS");
            }
            // Recheck under the same direct session lock after all isolated tests pass.
            await CheckTargetAsync(connection, targetContext, migration);
            await targetContext.Database.MigrateAsync();
            await VerifyCatalogAsync(connection, checks, "SHARED");
            if ((await targetContext.Database.GetAppliedMigrationsAsync()).Single() != migration)
                throw new InvalidOperationException("MIGRATION_HISTORY_MISMATCH");
            checks.Add("SHARED_MIGRATION_HISTORY_PASS");
            return new("Applied", target, validationDatabase, migration, 10, 19, checks);
        }
        catch (Exception error) when (error is DbException or TimeoutException or System.IO.IOException or InvalidOperationException)
        {
            // No exception message/stack/connection details in CLI output.
            var code = error is PostgresException pg ? "SQLSTATE_"+pg.SqlState : error.GetType().Name;
            return new("Failed", target, validationDatabase, Checks: checks, FailureCode: code);
        }
    }

    private static AppDbContext Context(NpgsqlConnection connection) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connection, options => options.MigrationsHistoryTable("ef_migrations_history", "public")).Options);

    private static async Task CheckTargetAsync(NpgsqlConnection connection, AppDbContext context, string migration)
    {
        await using var query = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')", connection);
        var names = new List<string>();
        await using (var reader = await query.ExecuteReaderAsync())
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        if (names.Count == 0)
        {
            // Also refuse pre-existing custom relations/functions in public, not just tables.
            await using var objects = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public') +
                    (SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public')
                """, connection);
            if (Convert.ToInt64(await objects.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("NONEMPTY_DATABASE");
            return;
        }
        if (!names.Order().SequenceEqual(Tables.Append("ef_migrations_history").Order()) ||
            !(await context.Database.GetAppliedMigrationsAsync()).SequenceEqual([migration]))
            throw new InvalidOperationException("UNRECOGNIZED_EXISTING_SCHEMA");
    }

    private static async Task VerifyCatalogAsync(NpgsqlConnection connection, List<string> checks, string prefix)
    {
        await using var query = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename=ANY(@tables)),
              (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='p' AND conrelid <> 'public.ef_migrations_history'::regclass),
              (SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f' AND confdeltype='a'),
              (SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND indexname=ANY(@expressions) AND indexdef LIKE '%UNIQUE%' AND indexdef LIKE '%lower(%'),
              (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND tgname IN ('trg_audit_logs_append_only','trg_asset_status_histories_append_only')),
              (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND column_name='row_version' AND data_type='bytea' AND is_nullable='NO'),
              (SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid WHERE c.relname='ix_assets_type_status' AND i.indnatts=4)
            """, connection);
        query.Parameters.AddWithValue("tables", Tables);
        query.Parameters.AddWithValue("expressions", new[] {"uq_departments_code","uq_roles_code","uq_permissions_code","uq_asset_types_code","uq_assets_asset_code","uq_users_employee_code","uq_assets_serial"});
        await using var reader = await query.ExecuteReaderAsync();
        await reader.ReadAsync();
        var expected = new long[] {10,10,19,7,2,6,1};
        for (var i=0; i<expected.Length; i++)
            if (reader.GetInt64(i) != expected[i]) throw new InvalidOperationException("CATALOG_MISMATCH");
        checks.Add(prefix+"_CATALOG_10_TABLES_19_FKS_7_EXPRESSION_INDEXES_PASS");
    }
}
