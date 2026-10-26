namespace ItAssetManagement.Api.Hosting;

// Prepared integration only. No setting is enabled by this patch, no seed or DDL.
// Owner acceptance/schema/grant verification is required before any future opt-in.
public static class MaintenanceRollout
{
    public const string ConfigurationKey = "Features:MaintenanceApi:Enabled";
    public static bool IsEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration[ConfigurationKey], out var enabled) && enabled;
    public static bool IsPath(PathString path) =>
        path.StartsWithSegments("/api/v1/asset-maintenance", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/v1/maintenance-tickets", StringComparison.OrdinalIgnoreCase);
}

public sealed class MaintenanceRolloutMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (MaintenanceRollout.IsPath(context.Request.Path))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!MaintenanceRollout.IsEnabled(configuration)) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            var readiness = context.RequestServices.GetRequiredService<IMaintenanceSchemaReadiness>();
            if (!await readiness.IsReadyAsync(context.RequestAborted)) {
                context.Response.Headers.RetryAfter = "30";
                await Mvp.Errors.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "MAINTENANCE_NOT_READY", "Bảo trì chưa sẵn sàng. Thử lại sau.");
                return;
            }
            if (!MaintenanceRollout.IsEnabled(configuration)) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
        }
        await next(context);
    }
}
