using System.Data;
using System.Data.Common;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Npgsql;

namespace ItAssetManagement.Api.Hosting;

public static class SoftwareRollout
{
    public const string Key = "Features:SoftwareApi:Enabled";
    public static bool IsEnabled(IConfiguration config) => bool.TryParse(config[Key], out var flag) && flag;
    public static bool IsPath(PathString path) => path.StartsWithSegments("/api/v1/software", StringComparison.OrdinalIgnoreCase);
}
public sealed class SoftwareControllerFeatureProvider(IConfiguration config) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) {
        if (!SoftwareRollout.IsEnabled(config)) foreach (var controller in feature.Controllers.Where(x => x.FullName == typeof(SoftwareController).FullName).ToArray()) feature.Controllers.Remove(controller);
    }
}
public interface ISoftwareReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class SoftwareReadiness(IConfiguration config) : ISoftwareReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct) {
        if (!NeonConnectionPolicy.TryCreate(config.GetConnectionString("DefaultConnection"), out var settings)) return false;
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        try {
            await using var connection = new NpgsqlConnection(settings.ConnectionString); await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx)) await mode.ExecuteNonQueryAsync(ct);
            await using (var tables = new NpgsqlCommand("SELECT to_regclass('public.softwares') IS NOT NULL AND to_regclass('public.software_licenses') IS NOT NULL AND to_regclass('public.software_code_sequence') IS NOT NULL AND to_regclass('public.permissions') IS NOT NULL AND to_regclass('public.role_permissions') IS NOT NULL AND to_regclass('public.roles') IS NOT NULL", connection, tx))
                if (!Equals(await tables.ExecuteScalarAsync(ct), true)) return false;
            await using var query = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM permissions WHERE code=ANY(@codes) AND is_active)=4
                  AND (SELECT count(*) FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.code IN ('ADMIN_IT','SYSTEM_MANAGER') AND r.is_active AND p.is_active AND p.code=ANY(@codes))=8
                  AND NOT EXISTS(SELECT 1 FROM role_permissions rp JOIN roles r ON r.id=rp.role_id JOIN permissions p ON p.id=rp.permission_id
                    WHERE r.code='TECHNICAL_SUPPORT' AND p.code=ANY(@codes))
                """, connection, tx);
            query.Parameters.AddWithValue("codes", SoftwarePermissions.All); return Equals(await query.ExecuteScalarAsync(ct), true);
        } catch (Exception error) when (error is DbException or IOException or InvalidOperationException or TimeoutException) { return false; }
    }
}
public sealed class SoftwareRolloutMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context) {
        if (SoftwareRollout.IsPath(context.Request.Path)) {
            context.Response.Headers.CacheControl = "no-store";
            if (!SoftwareRollout.IsEnabled(config)) { context.Response.StatusCode = 404; return; }
            if (!await context.RequestServices.GetRequiredService<ISoftwareReadiness>().IsReadyAsync(context.RequestAborted)) {
                context.Response.Headers.RetryAfter = "30";
                await Errors.WriteAsync(context, 503, "SOFTWARE_NOT_READY", "Danh mục phần mềm chưa sẵn sàng."); return;
            }
            if (!SoftwareRollout.IsEnabled(config)) { context.Response.StatusCode = 404; return; }
        }
        await next(context);
    }
}
