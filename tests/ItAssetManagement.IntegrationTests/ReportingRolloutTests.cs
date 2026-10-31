using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Api.Hosting;
using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ItAssetManagement.IntegrationTests;

public sealed class ReportingRolloutTests
{
    private sealed class Ready(bool value) : IReportingReadiness
    {
        public int Calls;
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(value); }
    }
    private static WebApplicationFactory<Program> Factory(string? flag, Ready ready) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"]="", [ReportingRollout.Key]=flag,["Frontend:Enabled"]="false"
        }));
        builder.ConfigureServices(services=>{services.RemoveAll<IReportingReadiness>();services.AddSingleton<IReportingReadiness>(ready);});
    });
    [Theory]
    [InlineData(null)] [InlineData("false")] [InlineData("invalid")]
    public async Task Closed_reporting_is_absent_and_does_not_probe_or_affect_assets(string? flag)
    {
        var ready=new Ready(false);await using var app=Factory(flag,ready);using var client=app.CreateClient();
        foreach(var path in new[]{"/api/v1/reports/asset-inventory","/api/v1/dashboard/summary","/api/v1/dashboard/costs"})
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path)).StatusCode);
        Assert.Equal(0,ready.Calls);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/assets")).StatusCode);
        Assert.DoesNotContain(app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items.SelectMany(x=>x.Items),
            x=>x.RelativePath?.StartsWith("api/v1/reports")==true||x.RelativePath?.StartsWith("api/v1/dashboard")==true);
        using var scope=app.Services.CreateScope();
        var availability=scope.ServiceProvider.GetRequiredService<IPermissionAvailability>();
        Assert.All(Permissions.Reports,code=>Assert.False(availability.IsAvailable(code)));
        Assert.True(availability.IsAvailable(Permissions.DashboardRead)); // Existing M1 grant stays available.
    }
    [Fact]
    public async Task Legacy_prepared_flag_cannot_open_reviewed_reporting_without_new_rollout_key()
    {
        var ready=new Ready(false);
        await using var app=Factory(null,ready).WithWebHostBuilder(builder=>builder.ConfigureAppConfiguration((_,config)=>
            config.AddInMemoryCollection(new Dictionary<string,string?>{["Features:ReportingApi:Enabled"]="true"})));
        using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
        Assert.Equal(0,ready.Calls);
    }
    [Fact]
    public async Task Unready_reporting_is_bounded_503_before_authentication()
    {
        var ready=new Ready(false);await using var app=Factory("true",ready);using var client=app.CreateClient();
        using var response=await client.GetAsync("/api/v1/reports/asset-inventory");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Equal("REPORTING_NOT_READY",(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal("30",response.Headers.RetryAfter?.ToString());Assert.Equal(1,ready.Calls);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/live")).StatusCode);
    }
    [Fact]
    public async Task Ready_reporting_still_requires_authentication()
    {
        await using var app=Factory("true",new Ready(true));using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/reports/asset-inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/dashboard/summary")).StatusCode);
        using var scope=app.Services.CreateScope();
        Assert.All(Permissions.Reports,code=>Assert.True(scope.ServiceProvider.GetRequiredService<IPermissionAvailability>().IsAvailable(code)));
    }
}
