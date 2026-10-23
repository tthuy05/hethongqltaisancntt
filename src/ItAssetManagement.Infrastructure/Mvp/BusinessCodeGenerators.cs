using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ItAssetManagement.Infrastructure.Mvp;

public static class FallbackCodeCounter
{
    private static readonly ConcurrentDictionary<string, StrongBox<long>> Counters = new();

    public static long Next(string sequenceName)
    {
        var box = Counters.GetOrAdd(sequenceName, _ => new StrongBox<long>(0));
        return Interlocked.Increment(ref box.Value);
    }

    public static void Reset(string sequenceName)
    {
        Counters.TryRemove(sequenceName, out _);
    }
}

public static class BusinessCodeGeneratorEngine
{
    private static readonly ConcurrentDictionary<string, bool> EnsuredSequences = new();

    public static void ResetEnsuredSequences()
    {
        EnsuredSequences.Clear();
    }

    public sealed record ControlledSequence(
        string SequenceName,
        string Prefix,
        string TableName,
        string ColumnName);

    public static readonly IReadOnlyList<ControlledSequence> ControlledSequences = new ControlledSequence[]
    {
        new("asset_code_sequence", "AST-", "assets", "asset_code"),
        new("employee_code_sequence", "NV", "users", "employee_code"),
        new("department_code_sequence", "PB", "departments", "code"),
        new("maintenance_ticket_code_sequence", "BT", "maintenance_tickets", "ticket_code"),
        new("software_code_sequence", "PM", "softwares", "code"),
        new("license_code_sequence", "BQ", "software_licenses", "license_code")
    };

    public static async Task EnsureControlledSequencesAsync(DbConnection conn, CancellationToken ct = default)
    {
        foreach (var seq in ControlledSequences)
        {
            await EnsureSequenceAsync(conn, null, seq.SequenceName, seq.Prefix, seq.TableName, seq.ColumnName, ct);
            EnsuredSequences.TryAdd(seq.SequenceName, true);
        }
    }

    public static async Task EnsureSequenceAsync(
        DbConnection conn,
        DbTransaction? transaction,
        string sequenceName,
        string prefix,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        await using var ensureCmd = conn.CreateCommand();
        ensureCmd.Transaction = transaction;
        ensureCmd.CommandText = $@"
DO $$
DECLARE
    highest bigint;
BEGIN
    PERFORM pg_advisory_xact_lock(837112, hashtext('{sequenceName}'));
    IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = '{sequenceName}') THEN
        CREATE SEQUENCE public.{sequenceName} START WITH 1;
        SELECT COALESCE(MAX(SUBSTRING({columnName} FROM '[0-9]+')::bigint), 0) INTO highest
        FROM public.{tableName}
        WHERE {columnName} ~ '^{prefix}[0-9]+$';
        IF highest > 0 THEN
            PERFORM setval('public.{sequenceName}', highest, true);
        END IF;
    END IF;
END $$;";
        await ensureCmd.ExecuteNonQueryAsync(ct);
    }

    public static async Task<string> NextCodeAsync<TEntity>(
        AppDbContext db,
        string sequenceName,
        string prefix,
        string tableName,
        string columnName,
        Func<string, Expression<Func<TEntity, bool>>> predicateFactory,
        CancellationToken ct,
        string? conflictErrorCode = null) where TEntity : class
    {
        bool isNpgsql = false;
        try
        {
            isNpgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            isNpgsql = false;
        }

        if (isNpgsql)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await db.Database.OpenConnectionAsync(ct);
                }

                var currentTx = db.Database.CurrentTransaction?.GetDbTransaction();

                // Controlled sequence initialization:
                // If sequence has not been ensured, initialize it outside any ambient transaction.
                // In PostgreSQL:
                // 1. Running DDL inside currentTx is vulnerable to transaction rollback (sequence destroyed).
                // 2. Any DDL error would poison/abort currentTx (SQLSTATE 25P02).
                // Initializing outside currentTx ensures sequence existence is durable and currentTx remains clean.
                if (!EnsuredSequences.ContainsKey(sequenceName))
                {
                    if (currentTx == null)
                    {
                        await EnsureSequenceAsync(conn, null, sequenceName, prefix, tableName, columnName, ct);
                        EnsuredSequences.TryAdd(sequenceName, true);
                    }
                    else
                    {
                        await using var isolatedConn = (DbConnection)Activator.CreateInstance(conn.GetType())!;
                        isolatedConn.ConnectionString = conn.ConnectionString;
                        await isolatedConn.OpenAsync(ct);
                        await EnsureSequenceAsync(isolatedConn, null, sequenceName, prefix, tableName, columnName, ct);
                        EnsuredSequences.TryAdd(sequenceName, true);
                    }
                }

                long value;
                try
                {
                    await using var command = conn.CreateCommand();
                    command.Transaction = currentTx;
                    command.CommandText = $"SELECT nextval('public.{sequenceName}')";
                    value = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
                }
                catch (Exception ex)
                {
                    if (IsRelationMissing(ex))
                    {
                        EnsuredSequences.TryRemove(sequenceName, out _);
                    }

                    // CRITICAL: In PostgreSQL, if an error occurred inside an active transaction (currentTx != null),
                    // PostgreSQL marks the transaction as aborted (25P02: current transaction is aborted,
                    // commands ignored until end of transaction block).
                    // We must NEVER execute any recovery queries or retries on an aborted transaction.
                    // Propagate the exception immediately to let the caller safely roll back.
                    throw;
                }

                var code = prefix + value.ToString("D6", CultureInfo.InvariantCulture);
                if (!await db.Set<TEntity>().AnyAsync(predicateFactory(code), ct))
                {
                    return code;
                }
            }

            var errCode = conflictErrorCode ?? $"{prefix.TrimEnd('-')}_CODE_ALLOCATION_CONFLICT";
            throw new BusinessException(409, errCode, $"Chưa cấp được mã {prefix}, hãy thử lại sau.");
        }
        else
        {
            // Fallback for offline / unconfigured DbContext unit tests
            var value = FallbackCodeCounter.Next(sequenceName);
            return prefix + value.ToString("D6", CultureInfo.InvariantCulture);
        }
    }

    private static bool IsRelationMissing(Exception ex)
    {
        if (ex is PostgresException pg && pg.SqlState == "42P01") return true;
        if (ex.InnerException is PostgresException pgInner && pgInner.SqlState == "42P01") return true;
        var msg = ex.Message;
        return msg.Contains("42P01") || (msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase) && msg.Contains("relation", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EmployeeCodeGenerator(AppDbContext db) : IEmployeeCodeGenerator
{
    public Task<string> NextAsync(CancellationToken ct) =>
        BusinessCodeGeneratorEngine.NextCodeAsync<User>(
            db,
            "employee_code_sequence",
            "NV",
            "users",
            "employee_code",
            code => x => x.EmployeeCode != null && x.EmployeeCode.ToLower() == code.ToLower(),
            ct);
}

public sealed class DepartmentCodeGenerator(AppDbContext db) : IDepartmentCodeGenerator
{
    public Task<string> NextAsync(CancellationToken ct) =>
        BusinessCodeGeneratorEngine.NextCodeAsync<Department>(
            db,
            "department_code_sequence",
            "PB",
            "departments",
            "code",
            code => x => x.Code.ToLower() == code.ToLower(),
            ct);
}

public sealed class MaintenanceTicketCodeGenerator(AppDbContext db) : IMaintenanceTicketCodeGenerator
{
    public Task<string> NextAsync(CancellationToken ct) =>
        BusinessCodeGeneratorEngine.NextCodeAsync<MaintenanceTicket>(
            db,
            "maintenance_ticket_code_sequence",
            "BT",
            "maintenance_tickets",
            "ticket_code",
            code => x => x.TicketCode.ToLower() == code.ToLower(),
            ct);
}

public sealed class SoftwareCodeGenerator(AppDbContext db) : ISoftwareCodeGenerator
{
    public Task<string> NextAsync(CancellationToken ct) =>
        BusinessCodeGeneratorEngine.NextCodeAsync<Software>(
            db,
            "software_code_sequence",
            "PM",
            "softwares",
            "code",
            code => x => x.Code.ToLower() == code.ToLower(),
            ct);
}

public sealed class LicenseCodeGenerator(AppDbContext db) : ILicenseCodeGenerator
{
    public Task<string> NextAsync(CancellationToken ct) =>
        BusinessCodeGeneratorEngine.NextCodeAsync<SoftwareLicense>(
            db,
            "license_code_sequence",
            "BQ",
            "software_licenses",
            "license_code",
            code => x => x.LicenseCode.ToLower() == code.ToLower(),
            ct);
}
