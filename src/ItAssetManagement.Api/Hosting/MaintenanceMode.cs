using ItAssetManagement.Api.Mvp;

namespace ItAssetManagement.Api.Hosting;

public static class MaintenanceMode
{
    public const string ConfigurationKey = "Maintenance:Enabled";

    // Missing/empty means OFF. A nonempty typo must not silently open traffic.
    public static bool IsEnabled(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        return !string.IsNullOrEmpty(value) &&
            (!bool.TryParse(value, out var enabled) || enabled);
    }

    // Exact paths only (case insensitive like ASP.NET routes), no prefix match,
    // trailing slash, special header, public toggle or authenticated bypass.
    public static bool IsAllowed(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
        (request.Path.Equals("/health/live", StringComparison.OrdinalIgnoreCase) ||
         request.Path.Equals("/health/version", StringComparison.OrdinalIgnoreCase));
}

// Register before static files, routing, auth, rate limits and any business DI.
// This controls incoming HTTP only: it cannot drain old requests or SQL writers.
public sealed class MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!MaintenanceMode.IsEnabled(configuration))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (MaintenanceMode.IsAllowed(context.Request))
        {
            // Even an otherwise valid bearer token would trigger DB-backed account
            // validation in normal auth middleware. Answer these two probes here,
            // without routing, authentication, service activation or DB access.
            if (HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.ContentType = "application/json";
                return;
            }
            object payload = context.Request.Path.Equals("/health/version", StringComparison.OrdinalIgnoreCase)
                ? DeploymentRevision.Create(configuration) : new { status = "Alive" };
            await context.Response.WriteAsJsonAsync(payload, cancellationToken: context.RequestAborted);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "60";
        if (HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.ContentType = "application/problem+json";
            return;
        }
        await Errors.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "MAINTENANCE_MODE",
            "Hệ thống đang bảo trì. Vui lòng thử lại sau.");
    }
}
