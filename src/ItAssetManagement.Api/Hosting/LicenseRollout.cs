using System.Data;
using System.Data.Common;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Npgsql;
using ItAssetManagement.Infrastructure.Security;

namespace ItAssetManagement.Api.Hosting;

public static class LicenseRollout
{
    public const string Key = "Features:LicenseApi:Enabled";
    public const string AllocationKey = "Features:LicenseAllocationApi:Enabled";
    public static bool IsEnabled(IConfiguration config) => bool.TryParse(config[Key], out var flag) && flag;
    public static bool IsAllocationEnabled(IConfiguration config) => IsEnabled(config) && bool.TryParse(config[AllocationKey], out var flag) && flag;
    public static bool IsAllocationPath(PathString path) {
        if (path.StartsWithSegments("/api/v1/license-assignments", StringComparison.OrdinalIgnoreCase)) return true;
        var parts = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: >= 5 } && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase) && parts[1].Equals("v1", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("software-licenses", StringComparison.OrdinalIgnoreCase) && parts[4].Equals("assignments", StringComparison.OrdinalIgnoreCase);
    }
    public static bool IsPath(PathString path) =>
        path.StartsWithSegments("/api/v1/software-licenses", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/license-assignments", StringComparison.OrdinalIgnoreCase);
}

public sealed class LicenseControllerFeatureProvider(IConfiguration config) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (!LicenseRollout.IsAllocationEnabled(config))
            foreach (var controller in feature.Controllers.Where(x => x.FullName == typeof(LicenseAssignmentController).FullName).ToArray()) feature.Controllers.Remove(controller);
        if (!LicenseRollout.IsEnabled(config))
        {
            foreach (var controller in feature.Controllers.Where(x =>
                x.FullName == typeof(SoftwareLicenseController).FullName ||
                x.FullName == typeof(LicenseAssignmentController).FullName).ToArray())
                feature.Controllers.Remove(controller);
        }
    }
}

public interface ILicenseReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }

public sealed class LicenseReadiness(IConfiguration config) : ILicenseReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!NeonConnectionPolicy.TryCreate(config.GetConnectionString("DefaultConnection"), out var settings)) return false;
        try { using var ring = ConfiguredLicenseKeyRing.FromConfiguration(config); } catch (InvalidOperationException) { return false; }
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        try
        {
            await using var connection = new NpgsqlConnection(settings.ConnectionString); await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx)) await mode.ExecuteNonQueryAsync(ct);
            await using (var tables = new NpgsqlCommand("SELECT to_regclass('public.softwares') IS NOT NULL AND to_regclass('public.license_assignments') IS NOT NULL AND to_regclass('public.software_licenses') IS NOT NULL AND to_regclass('public.license_code_sequence') IS NOT NULL AND to_regclass('public.permissions') IS NOT NULL AND to_regclass('public.role_permissions') IS NOT NULL AND to_regclass('public.roles') IS NOT NULL", connection, tx))
                if (!Equals(await tables.ExecuteScalarAsync(ct), true)) return false;
            await using var query = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM permissions WHERE code=ANY(@codes) AND is_active)=6
                  AND (SELECT count(*) FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.is_active AND p.is_active AND p.code=ANY(@codes) AND (r.code='ADMIN_IT' OR r.code='SYSTEM_MANAGER' AND p.code=ANY(@metadata)))=10
                  AND NOT EXISTS(SELECT 1 FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.code='TECHNICAL_SUPPORT' AND p.code=ANY(@codes))
                  AND NOT EXISTS(SELECT 1 FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.code<>'ADMIN_IT' AND p.code=ANY(@sensitive))
                """, connection, tx);
            query.Parameters.AddWithValue("codes", LicensePermissions.All.Except(LicensePermissions.Assignments).ToArray());
            query.Parameters.AddWithValue("metadata", LicensePermissions.Metadata);
            query.Parameters.AddWithValue("sensitive", new[] { Permissions.LicenseKeyManage, Permissions.LicenseKeyReveal });
            return Equals(await query.ExecuteScalarAsync(ct), true);
        }
        catch (Exception error) when (error is DbException or IOException or InvalidOperationException or TimeoutException) { return false; }
    }
}

public sealed class LicenseRolloutMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (LicenseRollout.IsPath(context.Request.Path))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!LicenseRollout.IsEnabled(config)) { context.Response.StatusCode = 404; return; }
            if (LicenseRollout.IsAllocationPath(context.Request.Path) && !LicenseRollout.IsAllocationEnabled(config)) { context.Response.StatusCode = 404; return; }
            if (!await context.RequestServices.GetRequiredService<ILicenseReadiness>().IsReadyAsync(context.RequestAborted))
            {
                context.Response.Headers.RetryAfter = "30";
                await Errors.WriteAsync(context, 503, "LICENSE_NOT_READY", "Quản lý bản quyền phần mềm chưa sẵn sàng.");
                return;
            }
            if (!LicenseRollout.IsEnabled(config)) { context.Response.StatusCode = 404; return; }
            if (LicenseRollout.IsAllocationPath(context.Request.Path)) {
                if (!LicenseRollout.IsAllocationEnabled(config)) { context.Response.StatusCode = 404; return; }
                if (!await context.RequestServices.GetRequiredService<ILicenseAllocationReadiness>().IsReadyAsync(context.RequestAborted)) {
                    context.Response.Headers.RetryAfter = "30";
                    await Errors.WriteAsync(context, 503, "LICENSE_ALLOCATION_NOT_READY", "Cấp phát bản quyền chưa sẵn sàng."); return;
                }
                if (!LicenseRollout.IsAllocationEnabled(config)) { context.Response.StatusCode = 404; return; }
            }
        }
        await next(context);
    }
}

public interface ILicenseAllocationReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class LicenseAllocationReadiness(IConfiguration config) : ILicenseAllocationReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct) {
        if (!NeonConnectionPolicy.TryCreate(config.GetConnectionString("DefaultConnection"), out var settings)) return false;
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        try {
            await using var conn = new NpgsqlConnection(settings.ConnectionString); await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY", conn, tx)) await mode.ExecuteNonQueryAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT to_regclass('public.license_assignments') IS NOT NULL
                  AND (SELECT count(*) FROM permissions WHERE code=ANY(@codes) AND is_active)=4
                  AND (SELECT count(*) FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.code IN ('ADMIN_IT','SYSTEM_MANAGER') AND r.is_active AND p.is_active AND p.code=ANY(@codes))=8
                  AND NOT EXISTS(SELECT 1 FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.code='TECHNICAL_SUPPORT' AND p.code=ANY(@codes))
                """, conn, tx);
            command.Parameters.AddWithValue("codes", LicensePermissions.Assignments); return Equals(await command.ExecuteScalarAsync(ct), true);
        } catch (Exception error) when (error is DbException or IOException or InvalidOperationException or TimeoutException) { return false; }
    }
}
