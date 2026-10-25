using System.Data;
using System.Data.Common;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Npgsql;

namespace ItAssetManagement.Api.Hosting;

public interface IMaintenanceSchemaReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class MaintenanceSchemaReadiness(IConfiguration config) : IMaintenanceSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!NeonConnectionPolicy.TryCreate(config.GetConnectionString("DefaultConnection"), out var settings)) return false;
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        try
        {
            await using var connection = new NpgsqlConnection(settings.ConnectionString); await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)) await mode.ExecuteNonQueryAsync(ct);
            await using (var tables = new NpgsqlCommand("""
                SELECT to_regclass('public.maintenance_tickets') IS NOT NULL AND to_regclass('public.maintenance_histories') IS NOT NULL
                    AND to_regclass('public.asset_assignments') IS NOT NULL AND to_regclass('public.asset_status_histories') IS NOT NULL
                    AND to_regclass('public.permissions') IS NOT NULL AND to_regclass('public.roles') IS NOT NULL
                    AND to_regclass('public.role_permissions') IS NOT NULL
                """, connection, transaction))
                if (!Equals(await tables.ExecuteScalarAsync(ct), true)) return false;
            await using var grants = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM public.permissions WHERE code=ANY(@codes) AND is_active)=8
                  AND (SELECT count(*) FROM public.role_permissions rp JOIN public.roles r ON r.id=rp.role_id
                      JOIN public.permissions p ON p.id=rp.permission_id WHERE r.code IN ('ADMIN_IT','SYSTEM_MANAGER')
                      AND r.is_active AND p.is_active AND p.code=ANY(@codes))=16
                  AND NOT EXISTS(SELECT 1 FROM public.role_permissions rp JOIN public.roles r ON r.id=rp.role_id
                      JOIN public.permissions p ON p.id=rp.permission_id WHERE r.code='TECHNICAL_SUPPORT' AND p.code=ANY(@forbidden))
                """, connection, transaction);
            grants.Parameters.AddWithValue("codes", Permissions.Maintenance);
            grants.Parameters.AddWithValue("forbidden", new[] { Permissions.MaintenanceAssign, Permissions.MaintenanceCancel, Permissions.MaintenanceCostWrite });
            return Equals(await grants.ExecuteScalarAsync(ct), true);
        }
        catch (Exception error) when (error is DbException or TimeoutException or IOException or InvalidOperationException)
        { return false; } // No provider exception, credentials or SQL in output/log.
    }
}
