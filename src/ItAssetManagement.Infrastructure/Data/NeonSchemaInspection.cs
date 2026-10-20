using System.Data.Common;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

public sealed record NeonSchemaInspectionResult(string Status, string? Database = null, string? Version = null,
    string? Encoding = null, string? Collation = null, IReadOnlyList<string>? Tables = null,
    int PrimaryKeys = 0, int ForeignKeys = 0, int CheckConstraints = 0, int Indexes = 0,
    long? BusinessRows = null, string? Migration = null, string? FailureCode = null,
    IReadOnlyDictionary<string, long>? RowCounts = null, long? ArchivedAssets = null,
    string? WorkflowQuery = null, int WorkflowAssetsChecked = 0, int ActiveWorkflowAssets = 0);

public static class NeonSchemaInspection
{
    public static async Task<NeonSchemaInspectionResult> CheckAsync(string? secret, string? isolatedDatabase = null)
    {
        if (!NeonConnectionPolicy.TryCreate(secret, out var settings)) return new("NotConfiguredOrInvalid");
        if (isolatedDatabase is not null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(isolatedDatabase,"\\Ait_asset_management_(m1|full_schema)_verify_[a-z0-9_]{1,24}\\z"))
                return new("InvalidInspectionTarget");
            settings!.Database = isolatedDatabase;
        }
        try
        {
            await using var connection = new NpgsqlConnection(settings!.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY",connection,transaction))
                await readOnly.ExecuteNonQueryAsync();
            await using var catalog = new NpgsqlCommand("""
                SELECT current_database(),current_setting('server_version'),current_setting('server_encoding'),
                  (SELECT datcollate FROM pg_database WHERE datname=current_database()),
                  ARRAY(SELECT tablename::text FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY tablename),
                  (SELECT count(*)::int FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='p' AND conrelid <> 'public.ef_migrations_history'::regclass),
                  (SELECT count(*)::int FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f'),
                  (SELECT count(*)::int FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='c'),
                  (SELECT count(*)::int FROM pg_indexes WHERE schemaname='public' AND tablename <> 'ef_migrations_history'),
                  (SELECT "MigrationId" FROM public.ef_migrations_history ORDER BY "MigrationId" DESC LIMIT 1)
                """,connection,transaction);
            string db, version, encoding, collation, migration;
            string[] tables;
            int pk, fk, check, indexes;
            await using (var reader = await catalog.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                db=reader.GetString(0); version=reader.GetString(1); encoding=reader.GetString(2); collation=reader.GetString(3);
                tables=reader.GetFieldValue<string[]>(4); pk=reader.GetInt32(5); fk=reader.GetInt32(6); check=reader.GetInt32(7);
                indexes=reader.GetInt32(8); migration=reader.GetString(9);
            }
            var businessTables = migration == FullSchemaBaseline.Migration
                ? NeonM1Setup.Tables.Concat(NeonAssignmentMaintenanceSetup.NewTables).Concat(FullSchemaBaseline.NewTables).ToArray()
                : migration == NeonAssignmentMaintenanceSetup.Migration
                ? NeonM1Setup.Tables.Concat(NeonAssignmentMaintenanceSetup.NewTables).ToArray() : NeonM1Setup.Tables;
            var expectedFk = migration == FullSchemaBaseline.Migration ? 41 : migration == NeonAssignmentMaintenanceSetup.Migration ? 27 : 19;
            if (migration is not (NeonAssignmentMaintenanceSetup.InitialMigration or NeonAssignmentMaintenanceSetup.Migration or FullSchemaBaseline.Migration) ||
                !tables.Order().SequenceEqual(businessTables.Append("ef_migrations_history").Order()) || pk!=businessTables.Length || fk!=expectedFk)
                return new("SchemaMismatch", Database:db);
            // Fixed source-controlled allowlist, no user-supplied SQL identifiers; SELECT only.
            var countSql = string.Join(" UNION ALL ",businessTables.Select(table => $"SELECT '{table}', count(*) FROM public.{table}"));
            await using var rows = new NpgsqlCommand(countSql,connection,transaction);
            var counts = new Dictionary<string, long>();
            await using (var reader = await rows.ExecuteReaderAsync())
                while (await reader.ReadAsync()) counts[reader.GetString(0)] = reader.GetInt64(1);
            await using var archived = new NpgsqlCommand("SELECT count(*) FROM public.assets WHERE is_archived", connection, transaction);
            var archivedCount = Convert.ToInt64(await archived.ExecuteScalarAsync());
            // Exercise the actual repository query on either supported schema without
            // changing assets or calling archive/retire on the public API.
            var assetIds = new List<long>();
            await using (var ids = new NpgsqlCommand("SELECT id FROM public.assets WHERE NOT is_archived ORDER BY id LIMIT 100", connection, transaction))
            await using (var reader = await ids.ExecuteReaderAsync())
                while (await reader.ReadAsync()) assetIds.Add(reader.GetInt64(0));
            await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection,
                provider => provider.MigrationsHistoryTable("ef_migrations_history", "public")).Options);
            await context.Database.UseTransactionAsync(transaction);
            var repository = new EfRepository(context); var activeWorkflows = 0;
            foreach (var assetId in assetIds)
                if (await repository.HasActiveWorkflowAsync(assetId, CancellationToken.None)) activeWorkflows++;
            if (migration == NeonAssignmentMaintenanceSetup.InitialMigration && activeWorkflows != 0)
                return new("SchemaMismatch", Database: db);
            return new("Verified",db,version,encoding,collation,tables,pk,fk,check,indexes,counts.Values.Sum(),migration,
                RowCounts: counts, ArchivedAssets: archivedCount, WorkflowQuery: "VerifiedReadOnly",
                WorkflowAssetsChecked: assetIds.Count, ActiveWorkflowAssets: activeWorkflows);
        }
        catch (Exception error) when (error is DbException or TimeoutException or System.IO.IOException)
        {
            return new("Failed",FailureCode: error is PostgresException pg ? "SQLSTATE_"+pg.SqlState : error.GetType().Name);
        }
    }
}
