using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class MvpSecurityTests(MvpFixture fixture)
{
    [NeonFact] public async Task Expired_bad_signature_wrong_issuer_and_wrong_audience_are_401()
    {
        using var admin = await fixture.ClientAsync();
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me"); var id = me.GetProperty("id").GetInt64();
        var settings = fixture.Factory.Services.GetRequiredService<JwtSettings>();
        foreach (var mode in new[] { "expired", "signature", "issuer", "audience" })
        {
            var now = DateTime.UtcNow;
            var key = mode == "signature" ? new byte[32] : settings.Key;
            var token = new JwtSecurityToken(mode == "issuer" ? "wrong" : settings.Issuer, mode == "audience" ? "wrong" : settings.Audience,
                [new Claim("sub", id.ToString()), new Claim("token_version", "0")],
                now.AddMinutes(-30), mode == "expired" ? now.AddMinutes(-10) : now.AddMinutes(5),
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
            using var client = fixture.Factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        }
    }
    [NeonFact] public async Task Account_token_version_and_inactive_state_are_checked_on_every_request()
    {
        foreach (var mode in new[] { "version", "inactive" })
        {
            var email = await fixture.UserAsync("revoke" + mode, "ADMIN_IT"); using var client = await fixture.ClientAsync(email);
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
            await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
            {
                var user = await db.Set<User>().SingleAsync(x => x.Email == email);
                if (mode == "version") user.TokenVersion++; else user.IsActive = false;
                sp.GetRequiredService<IAuditWriter>().Record("test.account.change", user, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
            });
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/assets")).StatusCode);
        }
    }
    [NeonFact] public async Task Database_role_change_overrides_still_valid_admin_claims()
    {
        var email = await fixture.UserAsync("rolechange", "ADMIN_IT"); using var client = await fixture.ClientAsync(email);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        await sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        {
            var id = await db.Set<User>().Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
            var link = await db.Set<UserRole>().SingleAsync(x => x.UserId == id);
            link.RoleId = await db.Set<Role>().Where(x => x.Code == "TECHNICAL_SUPPORT").Select(x => x.Id).SingleAsync();
            sp.GetRequiredService<IAuditWriter>().Record("test.role.change", link, null, "SYSTEM"); await db.SaveChangesAsync(); return true;
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset())).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal("TECHNICAL_SUPPORT", me.GetProperty("roles")[0].GetString());
    }
    [NeonFact] public async Task Audit_trigger_rejects_mutation_and_entity_hard_delete_rolls_back()
    {
        using var client = await fixture.ClientAsync();
        var created = await client.PostAsJsonAsync("/api/v1/assets", fixture.Asset()); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var error = await Assert.ThrowsAsync<PostgresException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { await db.Database.ExecuteSqlRawAsync("UPDATE public.audit_logs SET outcome = 'SUCCESS' WHERE false"); return true; }));
        Assert.Equal("55000", error.SqlState);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { db.Remove(await db.Set<Asset>().SingleAsync(x => x.Id == id)); await db.SaveChangesAsync(); return true; }));
        Assert.True(await db.Set<Asset>().AnyAsync(x => x.Id == id));
    }
    [NeonFact] public async Task Unique_index_race_is_translated_to_specific_business_conflict()
    {
        // Exercise the database exception path, not only the pre-flight duplicate query.
        using var client = await fixture.ClientAsync(); var request = fixture.Asset();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/assets", request)).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<AppDbContext>();
        var error = await Assert.ThrowsAsync<BusinessException>(() => sp.GetRequiredService<IUnitOfWork>().RunAsync(async () =>
        { db.Add(new Asset { AssetCode = request.AssetCode, Name = "Duplicate race", AssetTypeId = fixture.TypeId, OwningDepartmentId = fixture.DepartmentId }); await db.SaveChangesAsync(); return true; }));
        Assert.Equal(409, error.Status); Assert.Equal("ASSET_CODE_CONFLICT", error.Code);
    }
    [NeonFact] public async Task Missing_asset_and_invalid_json_return_safe_problem_details()
    {
        using var client = await fixture.ClientAsync();
        var missing = await client.GetAsync("/api/v1/assets/9223372036854775807"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var body = await missing.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("ASSET_NOT_FOUND", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("traceId", out _));
        var invalid = await client.PostAsync("/api/v1/assets", new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.DoesNotContain("Npgsql", await invalid.Content.ReadAsStringAsync());
    }
}
