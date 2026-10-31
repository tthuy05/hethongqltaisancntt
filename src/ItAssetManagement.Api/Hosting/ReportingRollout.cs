using System.Data;
using System.Data.Common;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Npgsql;

namespace ItAssetManagement.Api.Hosting;

public static class ReportingRollout
{
    // The deployed legacy backend already recognizes ReportingApi:Enabled. A new key
    // lets the operator save the reviewed rollout without enabling old prepared controllers.
    public const string Key = "Features:ReportingV2:Enabled";
    public static bool IsEnabled(IConfiguration configuration) => bool.TryParse(configuration[Key], out var enabled) && enabled;
    public static bool IsPath(PathString path) => path.StartsWithSegments("/api/v1/reports", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/dashboard", StringComparison.OrdinalIgnoreCase);
}
public interface IReportingReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class ReportingReadiness(IConfiguration configuration) : IReportingReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!NeonConnectionPolicy.TryCreate(configuration.GetConnectionString("DefaultConnection"), out var settings)) return false;
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        try {
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(ct);
            return await ProbeAsync(connection, ct);
        }
        catch (Exception error) when (error is DbException or IOException or InvalidOperationException or TimeoutException) { return false; }
    }
    // Read-only shared/isolated probe. No migrations, seed, SQL text or connection details are logged.
    public static async Task<bool> ProbeAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='3s'; SET LOCAL lock_timeout='2s'", connection, transaction))
            await mode.ExecuteNonQueryAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT NOT EXISTS (SELECT 1 FROM unnest(@tables) AS t(name) WHERE to_regclass('public.' || t.name) IS NULL)
                AND EXISTS (SELECT 1 FROM public.ef_migrations_history WHERE "MigrationId"='20261030024812_AddReplacementRuleSeverity')
                AND (SELECT count(*) FROM public.permissions WHERE code=ANY(@codes) AND is_active)=7
            """, connection, transaction);
        command.Parameters.AddWithValue("tables", new[] { "assets", "asset_types", "departments", "users", "asset_assignments", "maintenance_tickets", "softwares", "software_licenses", "license_assignments", "replacement_rules", "replacement_recommendations" });
        command.Parameters.AddWithValue("codes", Permissions.Reports);
        return Equals(await command.ExecuteScalarAsync(ct), true);
    }
}
public sealed class ReportingRolloutMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ReportingRollout.IsPath(context.Request.Path)) {
            context.Response.Headers.CacheControl = "no-store";
            if (!ReportingRollout.IsEnabled(configuration)) { context.Response.StatusCode = 404; return; }
            if (!await context.RequestServices.GetRequiredService<IReportingReadiness>().IsReadyAsync(context.RequestAborted)) {
                context.Response.Headers.RetryAfter = "30";
                await Errors.WriteAsync(context, 503, "REPORTING_NOT_READY", "Báo cáo và tổng quan chưa sẵn sàng."); return;
            }
        }
        await next(context);
    }
}
