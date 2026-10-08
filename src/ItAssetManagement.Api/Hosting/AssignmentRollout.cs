using System.Data;
using System.Data.Common;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Npgsql;

namespace ItAssetManagement.Api.Hosting;

public static class AssignmentRollout
{
    public const string ConfigurationKey = "Features:AssignmentApi:Enabled";
    public static bool IsEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration[ConfigurationKey], out var enabled) && enabled;
}

// Run after the framework's controller discovery. Disabled routes are also absent
// from OpenAPI; Thiện's Controller stays unchanged.
public sealed class AssignmentControllerFeatureProvider(IConfiguration configuration) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (!AssignmentRollout.IsEnabled(configuration))
            foreach (var controller in feature.Controllers.Where(type =>
                type.FullName == "ItAssetManagement.Api.Mvp.AssetAssignmentsController").ToArray())
                feature.Controllers.Remove(controller);
    }
}

public interface IAssignmentSchemaReadiness
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

// No startup probe, migration, seed, or EF entity access. Never caches a successful
// result across requests: an accidentally enabled rollout on schema10 stays closed.
public sealed class AssignmentSchemaReadiness(IConfiguration configuration) : IAssignmentSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (!NeonConnectionPolicy.TryCreate(configuration.GetConnectionString("DefaultConnection"), out var settings)) return false;
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        settings.ApplicationName = "ItAssetManagement.AssignmentReadiness";
        try
        {
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
                await readOnly.ExecuteNonQueryAsync(cancellationToken);
            await using (var inventory = new NpgsqlCommand("""
                SELECT to_regclass('public.asset_assignments') IS NOT NULL
                    AND to_regclass('public.maintenance_tickets') IS NOT NULL
                    AND to_regclass('public.ef_migrations_history') IS NOT NULL
                    AND to_regclass('public.permissions') IS NOT NULL
                    AND to_regclass('public.roles') IS NOT NULL
                    AND to_regclass('public.role_permissions') IS NOT NULL
                """, connection, transaction))
                if (!Equals(await inventory.ExecuteScalarAsync(cancellationToken), true)) return false;
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM public.ef_migrations_history WHERE "MigrationId"=@migration)
                  AND (SELECT count(*) FROM public.permissions WHERE code=ANY(@codes) AND is_active)=3
                  AND (SELECT count(*) FROM public.role_permissions rp
                      JOIN public.roles r ON r.id=rp.role_id
                      JOIN public.permissions p ON p.id=rp.permission_id
                      WHERE r.code IN ('ADMIN_IT','SYSTEM_MANAGER') AND r.is_active AND p.is_active AND p.code=ANY(@codes))=6
                  AND NOT EXISTS (SELECT 1 FROM public.role_permissions rp
                      JOIN public.roles r ON r.id=rp.role_id JOIN public.permissions p ON p.id=rp.permission_id
                      WHERE r.code='TECHNICAL_SUPPORT' AND p.code=ANY(@codes))
                """, connection, transaction);
            command.Parameters.AddWithValue("migration", "20261008080630_AddAssignmentMaintenance");
            command.Parameters.AddWithValue("codes", new[] { "assignments.read", "assignments.assign", "assignments.return" });
            return Equals(await command.ExecuteScalarAsync(cancellationToken), true);
        }
        catch (Exception error) when (error is DbException or TimeoutException or IOException or InvalidOperationException)
        {
            // Fail closed without logging a provider exception, host, credential or SQL.
            return false;
        }
    }
}

public sealed class AssignmentRolloutMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/asset-assignments", StringComparison.OrdinalIgnoreCase))
        {
            if (!AssignmentRollout.IsEnabled(configuration)) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            // Before authentication/model binding/Controller activation. Even a stale
            // valid bearer token cannot reach AssignmentService while tables are absent.
            var readiness = context.RequestServices.GetRequiredService<IAssignmentSchemaReadiness>();
            if (!await readiness.IsReadyAsync(context.RequestAborted))
            {
                context.Response.Headers.RetryAfter = "30";
                await Errors.WriteAsync(context, StatusCodes.Status503ServiceUnavailable,
                    "ASSIGNMENT_NOT_READY", "Chức năng cấp phát chưa sẵn sàng. Thử lại sau.");
                return;
            }
            // A reload that closes the rollout while readiness is running must not
            // leave an enabled middleware behind a version response reporting false.
            if (!AssignmentRollout.IsEnabled(configuration)) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
        }
        await next(context);
    }
}
