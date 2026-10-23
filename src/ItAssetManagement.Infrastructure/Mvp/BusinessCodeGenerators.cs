using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public static async Task<string> NextCodeAsync<TEntity>(
        AppDbContext db,
        string sequenceName,
        string prefix,
        string tableName,
        string columnName,
        Func<string, Expression<Func<TEntity, bool>>> predicateFactory,
        CancellationToken ct) where TEntity : class
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

                if (!EnsuredSequences.ContainsKey(sequenceName))
                {
                    try
                    {
                        await using var ensureCmd = conn.CreateCommand();
                        ensureCmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                        ensureCmd.CommandText = $@"
DO $$
DECLARE
    highest bigint;
BEGIN
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
                        EnsuredSequences.TryAdd(sequenceName, true);
                    }
                    catch
                    {
                        // Proceed even if schema check cannot run; nextval will execute if sequence exists.
                    }
                }

                await using var command = conn.CreateCommand();
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = $"SELECT nextval('public.{sequenceName}')";
                var value = Convert.ToInt64(await command.ExecuteScalarAsync(ct));

                var code = prefix + value.ToString("D6", CultureInfo.InvariantCulture);
                if (!await db.Set<TEntity>().AnyAsync(predicateFactory(code), ct))
                {
                    return code;
                }
            }

            throw new BusinessException(409, $"{prefix}_CODE_ALLOCATION_CONFLICT", $"Chưa cấp được mã {prefix}, hãy thử lại sau.");
        }
        else
        {
            // Fallback for offline / unconfigured DbContext unit tests
            var value = FallbackCodeCounter.Next(sequenceName);
            return prefix + value.ToString("D6", CultureInfo.InvariantCulture);
        }
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
