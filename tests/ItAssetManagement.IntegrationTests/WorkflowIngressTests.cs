using System.Net;
using ItAssetManagement.Api.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

public sealed class WorkflowIngressTests
{
    private sealed class Probe(bool block = false) : IAssignmentSchemaReadiness, IMaintenanceSchemaReadiness
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<bool> IsReadyAsync(CancellationToken ct) {
            Interlocked.Increment(ref calls);
            return block ? await Release.Task.WaitAsync(ct) : false;
        }
    }
    private static WebApplicationFactory<Program> Factory(Probe probe, bool enabled = true) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "", ["Frontend:Enabled"] = "false", ["Swagger:Enabled"] = "false",
            [AssignmentRollout.ConfigurationKey] = enabled.ToString(), [MaintenanceRollout.ConfigurationKey] = enabled.ToString(),
            ["Logging:LogLevel:Default"] = "None" }));
        builder.ConfigureServices(services => {
            services.RemoveAll<IAssignmentSchemaReadiness>(); services.AddSingleton<IAssignmentSchemaReadiness>(probe);
            services.RemoveAll<IMaintenanceSchemaReadiness>(); services.AddSingleton<IMaintenanceSchemaReadiness>(probe);
        });
    });
    [Fact]
    public async Task Anonymous_workflow_paths_share_a_budget_before_readiness_and_do_not_limit_health()
    {
        var probe = new Probe(); await using var factory = Factory(probe); using var client = factory.CreateClient();
        string[] paths = ["/api/v1/asset-assignments", "/api/v1/assets/1/assignments", "/api/v1/maintenance-tickets", "/api/v1/asset-maintenance"];
        for (var index = 0; index < WorkflowIngress.RequestsPerMinute; index++) {
            using var response = await client.GetAsync(paths[index % paths.Length]); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        using var rejected = await client.GetAsync(paths[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("RATE_LIMITED", await rejected.Content.ReadAsStringAsync());
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(WorkflowIngress.RequestsPerMinute, probe.Calls);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
    }
    [Fact]
    public async Task Readiness_connection_concurrency_is_bounded_without_queueing_anonymous_requests()
    {
        var probe = new Probe(true); await using var factory = Factory(probe); using var client = factory.CreateClient();
        var pending = Enumerable.Range(0, WorkflowIngress.ConcurrentRequests).Select(_ => client.GetAsync("/api/v1/maintenance-tickets")).ToArray();
        try {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (probe.Calls < WorkflowIngress.ConcurrentRequests) await Task.Delay(10, deadline.Token);
            using var rejected = await client.GetAsync("/api/v1/asset-assignments");
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode); Assert.Equal(WorkflowIngress.ConcurrentRequests, probe.Calls);
        } finally { probe.Release.TrySetResult(false); }
        foreach (var response in await Task.WhenAll(pending)) { using (response) Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); }
    }
    [Fact]
    public async Task Closed_modules_never_invoke_readiness_or_consume_workflow_budget()
    {
        var probe = new Probe(); await using var factory = Factory(probe, false); using var client = factory.CreateClient();
        for (var index = 0; index <= WorkflowIngress.RequestsPerMinute; index++) {
            using var response = await client.GetAsync(index % 2 == 0 ? "/api/v1/asset-assignments" : "/api/v1/maintenance-tickets");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Equal(0, probe.Calls);
    }
}
