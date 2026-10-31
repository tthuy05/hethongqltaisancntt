using System.Data;
using System.Data.Common;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Npgsql;

namespace ItAssetManagement.Api.Hosting;

public static class ReplacementRuleRollout
{
    public const string Key = "Features:ReplacementRuleApi:Enabled";
    public static bool IsEnabled(IConfiguration config) => bool.TryParse(config[Key], out var flag) && flag;
    public static bool IsPath(PathString path) => path.StartsWithSegments("/api/v1/replacement-rules", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/replacement-recommendations", StringComparison.OrdinalIgnoreCase);
}
public sealed class ReplacementRuleControllerFeatureProvider(IConfiguration config) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (!ReplacementRuleRollout.IsEnabled(config))
            foreach (var controller in feature.Controllers.Where(x => x.FullName == typeof(ReplacementRuleController).FullName || x.FullName == typeof(ReplacementRecommendationController).FullName).ToArray())
                feature.Controllers.Remove(controller);
    }
}
public interface IReplacementRuleReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class ReplacementRuleReadiness(IConfiguration config) : IReplacementRuleReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!NeonConnectionPolicy.TryCreate(config.GetConnectionString("DefaultConnection"), out var settings)) return false;
        settings!.Timeout = 3; settings.CommandTimeout = 3;
        try {
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction)) await mode.ExecuteNonQueryAsync(ct);
            await using var query = new NpgsqlCommand("""
                SELECT to_regclass('public.replacement_rules') IS NOT NULL
                    AND to_regclass('public.asset_types') IS NOT NULL
                    AND to_regclass('public.replacement_recommendations') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='replacement_rules' AND column_name='severity')
                    AND EXISTS (SELECT 1 FROM public.ef_migrations_history WHERE "MigrationId"='20261030024812_AddReplacementRuleSeverity')
                    AND (SELECT count(*) FROM public.permissions WHERE code=ANY(@permissions) AND is_active)=6
                """, connection, transaction);
            query.Parameters.AddWithValue("permissions", ReplacementPermissions.All);
            return Equals(await query.ExecuteScalarAsync(ct), true);
        }
        catch (Exception error) when (error is DbException or IOException or InvalidOperationException or TimeoutException) { return false; }
    }
}
public sealed class ReplacementRuleRolloutMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ReplacementRuleRollout.IsPath(context.Request.Path)) {
            context.Response.Headers.CacheControl = "no-store";
            if (!ReplacementRuleRollout.IsEnabled(config)) { context.Response.StatusCode = 404; return; }
            if (!await context.RequestServices.GetRequiredService<IReplacementRuleReadiness>().IsReadyAsync(context.RequestAborted)) {
                context.Response.Headers.RetryAfter = "30";
                await Errors.WriteAsync(context, 503, "REPLACEMENT_RULE_NOT_READY", "Danh mục quy tắc thay thế chưa sẵn sàng."); return;
            }
            if (!ReplacementRuleRollout.IsEnabled(config)) { context.Response.StatusCode = 404; return; }
        }
        await next(context);
    }
}
