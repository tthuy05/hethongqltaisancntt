using System.Threading.RateLimiting;

namespace ItAssetManagement.Api.Hosting;

public static class WorkflowIngress
{
    public const int RequestsPerMinute = 120;
    public const int ConcurrentRequests = 8;
    private static bool Applies(HttpContext context, IConfiguration configuration) =>
        AssignmentRollout.IsEnabled(configuration) && AssignmentRollout.IsPath(context.Request.Path) ||
        MaintenanceRollout.IsEnabled(configuration) && MaintenanceRollout.IsPath(context.Request.Path) ||
        SoftwareRollout.IsEnabled(configuration) && SoftwareRollout.IsPath(context.Request.Path) ||
        ReplacementRuleRollout.IsEnabled(configuration) && ReplacementRuleRollout.IsPath(context.Request.Path) ||
        ReportingRollout.IsEnabled(configuration) && ReportingRollout.IsPath(context.Request.Path) ||
        LicenseRollout.IsEnabled(configuration) && LicenseRollout.IsPath(context.Request.Path) &&
            (!LicenseRollout.IsAllocationPath(context.Request.Path) || LicenseRollout.IsAllocationEnabled(configuration));

    public static PartitionedRateLimiter<HttpContext> CreateLimiter(IConfiguration configuration) =>
        PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => Applies(context, configuration)
                ? RateLimitPartition.GetFixedWindowLimiter("workflow", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = RequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter("other")),
            PartitionedRateLimiter.Create<HttpContext, string>(context => Applies(context, configuration)
                ? RateLimitPartition.GetConcurrencyLimiter("workflow", _ => new ConcurrencyLimiterOptions {
                    PermitLimit = ConcurrentRequests, QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter("other")));
}
