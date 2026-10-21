namespace ItAssetManagement.Api.Hosting;

// Prepared integration only. No setting is enabled by this patch, no seed or DDL.
// Owner acceptance/schema/grant verification is required before any future opt-in.
public static class MaintenanceRollout
{
    public const string ConfigurationKey = "Features:MaintenanceApi:Enabled";
    public static bool IsEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration[ConfigurationKey], out var enabled) && enabled;
}

public sealed class MaintenanceRolloutMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/asset-maintenance", StringComparison.OrdinalIgnoreCase)
            && !MaintenanceRollout.IsEnabled(configuration))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(context);
    }
}
