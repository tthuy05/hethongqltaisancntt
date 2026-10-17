using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class AssetStatusHistoryApiTests(MvpFixture fixture)
{
    private void VerifyTarget()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }
    private async Task<AssetDto> CreateAsync(HttpClient client)
    {
        VerifyTarget(); using var response = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssetDto>())!;
    }
    [NeonFact]
    public async Task History_returns_stable_newest_first_pages_with_current_actor_name_and_unchanged_status_contract()
    {
        using var admin = await fixture.ClientAsync(); var asset = await CreateAsync(admin);
        var actor = (await admin.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        using var retired = await admin.PatchAsJsonAsync($"/api/v1/assets/{asset.Id}/status", new StatusRequest("Retired", "Synthetic history check", asset.RowVersion));
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var newest = (await admin.GetFromJsonAsync<PagedResponse<HistoryDto>>($"/api/v1/assets/{asset.Id}/status-history?page=1&pageSize=1"))!;
        var oldest = (await admin.GetFromJsonAsync<PagedResponse<HistoryDto>>($"/api/v1/assets/{asset.Id}/status-history?page=2&pageSize=1"))!;
        Assert.Equal(2, newest.TotalItems); Assert.Equal(2, newest.TotalPages);
        var row = Assert.Single(newest.Items); Assert.Equal("InStock", row.FromStatus); Assert.Equal("Retired", row.ToStatus);
        Assert.Equal("ADMIN", row.Source); Assert.Equal(actor.Id, row.ChangedByUserId); Assert.Equal(actor.DisplayName, row.ChangedByDisplayName);
        Assert.Equal(DateTimeKind.Utc, row.ChangedAtUtc.Kind); Assert.NotEqual(Guid.Empty, row.CorrelationId);
        var initial = Assert.Single(oldest.Items); Assert.Null(initial.FromStatus); Assert.Equal("InStock", initial.ToStatus);
        Assert.Equal("SYSTEM", initial.Source); Assert.True(initial.Id < row.Id);
    }
    [NeonFact]
    public async Task Support_can_read_nonfinancial_history_without_email_cost_or_mutation_fields()
    {
        using var admin = await fixture.ClientAsync(); var asset = await CreateAsync(admin);
        using var support = await fixture.ClientAsync(fixture.SupportEmail);
        using var response = await support.GetAsync($"/api/v1/assets/{asset.Id}/status-history"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = (await response.Content.ReadFromJsonAsync<JsonElement>()).ToString();
        Assert.Contains("changedByDisplayName", json); Assert.DoesNotContain("purchasePrice", json);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }
    [NeonFact]
    public async Task Existing_history_only_accepts_paging_and_keeps_401_404_boundaries()
    {
        using var admin = await fixture.ClientAsync(); var asset = await CreateAsync(admin);
        foreach (var query in new[] { "from=2026-10-15", "status=Retired", "sortBy=id", "page=0", "pageSize=101", "page=1&page=2" })
        {
            using var invalid = await admin.GetAsync($"/api/v1/assets/{asset.Id}/status-history?{query}"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var missing = await admin.GetAsync("/api/v1/assets/9223372036854775807/status-history"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var anonymous = fixture.Factory.CreateClient(); using var denied = await anonymous.GetAsync($"/api/v1/assets/{asset.Id}/status-history");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
    [NeonFact]
    public async Task Asset_read_alone_does_not_grant_status_history_access()
    {
        using var admin = await fixture.ClientAsync(); var asset = await CreateAsync(admin);
        var suffix = Guid.NewGuid().ToString("N")[..8]; var code = "HISTORY_" + suffix.ToUpperInvariant();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var role = new Role { Code = code, Name = "Isolated inventory-only history test", IsActive = true };
                db.Add(role); audit.Record("fixture.history", role, null, "SYSTEM"); await db.SaveChangesAsync();
                var permission = await db.Set<Permission>().SingleAsync(p => p.Code == Permissions.AssetRead);
                var grant = new RolePermission { RoleId = role.Id, PermissionId = permission.Id };
                db.Add(grant); audit.Record("fixture.history", grant, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
            });
        }
        var email = await fixture.UserAsync("history" + suffix, code); using var inventory = await fixture.ClientAsync(email);
        using var canRead = await inventory.GetAsync($"/api/v1/assets/{asset.Id}"); Assert.Equal(HttpStatusCode.OK, canRead.StatusCode);
        using var cannotReadHistory = await inventory.GetAsync($"/api/v1/assets/{asset.Id}/status-history"); Assert.Equal(HttpStatusCode.Forbidden, cannotReadHistory.StatusCode);
    }
}
