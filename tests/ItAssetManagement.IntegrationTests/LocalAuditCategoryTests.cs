using System.Net;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalAuditCategoryTests
{
    [LocalPostgresFact]
    public async Task PostgreSql_filters_before_paging_and_returns_safe_actor_labels()
    {
        await using var app = LocalAssignmentCompletionTests.Factory();
        await LocalAssignmentCompletionTests.SeedAsync(app);
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunAuditReadAsync();
        using var admin = await LocalAssignmentCompletionTests.LoginAsync(app);
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(-5).ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));
        var path = $"/api/v1/audit-logs?from={from}&to={to}&pageSize=100";
        var all = (await admin.GetFromJsonAsync<PagedResponse<AuditLogSummaryDto>>(path))!;
        var business = (await admin.GetFromJsonAsync<PagedResponse<AuditLogSummaryDto>>(path + "&category=business"))!;
        var technical = (await admin.GetFromJsonAsync<PagedResponse<AuditLogSummaryDto>>(path + "&category=technical"))!;
        Assert.Equal(all.TotalItems, business.TotalItems + technical.TotalItems);
        Assert.All(business.Items, x => Assert.True(AuditActionCatalog.IsBusinessAction(x.Action)));
        Assert.All(technical.Items, x => Assert.False(AuditActionCatalog.IsBusinessAction(x.Action)));
        var login = Assert.Single(business.Items.Where(x => x.Action == "auth.login" && x.ActorUsername == "dev.admin").Take(1));
        Assert.False(string.IsNullOrWhiteSpace(login.ActorFullName));
        Assert.Equal("Đăng nhập hệ thống", login.ActionDescription);
        var detail = (await admin.GetFromJsonAsync<AuditLogDetailDto>($"/api/v1/audit-logs/{login.Id}"))!;
        Assert.Equal(login.ActorFullName, detail.ActorFullName);
        using var invalid = await admin.GetAsync(path + "&category=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
