using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class InvitationRolloutTests(MvpFixture fixture)
{
    private async Task<UserDto> Invite(params long[] roleIds)
    {
        using var admin=await fixture.ClientAsync(); var key=fixture.Prefix+Guid.NewGuid().ToString("N")[..10];
        var result=await admin.PostAsJsonAsync("/api/v1/users",new { username=key,email=key+"@fixture.test",displayName="Nhân viên kỹ thuật",departmentId=fixture.DepartmentId,roleIds=roleIds.Length==0?new[]{fixture.SupportRoleId}:roleIds });
        Assert.Equal(HttpStatusCode.Created,result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<UserDto>())!;
    }
    private async Task<HttpResponseMessage> Activate(UserDto user,string? token=null)
    {
        using var client=fixture.Factory.CreateClient();
        return await client.PostAsJsonAsync("/api/v1/auth/activate",new {token=token??fixture.Mailbox.Token(user.Email),password=MvpFixture.Password,confirmPassword=MvpFixture.Password});
    }
    [NeonFact]
    public async Task Invitation_is_pending_hashed_30_minutes_and_atomic_activation_is_one_use()
    {
        var before=fixture.Clock.GetUtcNow().UtcDateTime; var user=await Invite();
        Assert.False(user.IsEmailConfirmed); Assert.Equal("Sandboxed",user.InvitationEmailStatus); Assert.NotEmpty(user.DepartmentName!);
        Assert.InRange(user.ActivationExpiresAt!.Value,before.AddMinutes(30),fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(30));
        await using(var scope=fixture.Factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row=await db.Set<User>().SingleAsync(x=>x.Id==user.Id);
            Assert.Equal(ActivationTokens.Hash(fixture.Mailbox.Token(user.Email)),row.ActivationTokenHash);
            Assert.False(scope.ServiceProvider.GetRequiredService<IPasswordService>().Verify(row,MvpFixture.Password));
            Assert.Single(await db.Set<UserRole>().Where(x=>x.UserId==user.Id).ToArrayAsync());
        }
        using var anonymous=fixture.Factory.CreateClient();
        for(var attempt=0;attempt<6;attempt++) Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/v1/auth/login",new {user.Email,password=MvpFixture.Password})).StatusCode);
        await using(var pendingScope=fixture.Factory.Services.CreateAsyncScope()) {
            var pending=await pendingScope.ServiceProvider.GetRequiredService<AppDbContext>().Set<User>().SingleAsync(x=>x.Id==user.Id);
            Assert.Equal(0,pending.FailedLoginCount);Assert.Null(pending.LockoutEndUtc);
        }
        Assert.Equal(HttpStatusCode.OK,(await Activate(user)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Activate(user)).StatusCode);
        using var loggedIn=await fixture.ClientAsync(user.Email); Assert.Equal(HttpStatusCode.OK,(await loggedIn.GetAsync("/api/v1/assets")).StatusCode);
        await using var finalScope=fixture.Factory.Services.CreateAsyncScope(); var final=await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().Set<User>().SingleAsync(x=>x.Id==user.Id);
        Assert.True(final.IsEmailConfirmed); Assert.Null(final.ActivationTokenHash); Assert.Null(final.ActivationExpiresAtUtc);
    }
    [NeonFact]
    public async Task Resend_invalidates_prior_token_and_has_no_token_in_response_or_audit()
    {
        using var admin=await fixture.ClientAsync(); var user=await Invite(); var old=fixture.Mailbox.Token(user.Email);
        var resend=await admin.PostAsJsonAsync($"/api/v1/users/{user.Id}/invitation",new{}); Assert.Equal(HttpStatusCode.OK,resend.StatusCode);
        Assert.DoesNotContain(old,await resend.Content.ReadAsStringAsync());
        Assert.NotEqual(old,fixture.Mailbox.Token(user.Email)); Assert.Equal(HttpStatusCode.BadRequest,(await Activate(user,old)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Activate(user)).StatusCode);
        await using var scope=fixture.Factory.Services.CreateAsyncScope(); var audits=await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AuditLog>().Where(x=>x.EntityId==user.Id.ToString()).ToListAsync();
        Assert.Contains(audits,x=>x.Action=="users.invitation.resend"); Assert.Contains(audits,x=>x.Action=="users.activate");
        foreach(var row in audits) {var serialized=JsonSerializer.Serialize(row);Assert.DoesNotContain(old,serialized);Assert.DoesNotContain(fixture.Mailbox.Token(user.Email),serialized);}
    }
    [NeonFact]
    public async Task Expired_invalid_and_concurrent_activation_have_server_side_guards()
    {
        var expired=await Invite(); fixture.Clock.Offset=TimeSpan.FromMinutes(31);
        try {Assert.Equal(HttpStatusCode.BadRequest,(await Activate(expired)).StatusCode);}finally{fixture.Clock.Offset=TimeSpan.Zero;}
        Assert.Equal(HttpStatusCode.BadRequest,(await Activate(expired,"bad-token")).StatusCode);
        var concurrent=await Invite();var responses=await Task.WhenAll(Activate(concurrent),Activate(concurrent));
        Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.BadRequest);
    }
    [NeonFact]
    public async Task Email_failure_and_missing_config_leave_pending_account_resendable_without_duplicate()
    {
        foreach(var status in new[]{"Failed","NotConfigured"})
        {
            fixture.Mailbox.Status=status;UserDto user;
            try {user=await Invite();Assert.Equal(status,user.InvitationEmailStatus);}finally{fixture.Mailbox.Status="Sandboxed";}
            using var admin=await fixture.ClientAsync();
            Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync("/api/v1/users",new{user.Username,user.Email,user.DisplayName,roleIds=new[]{fixture.SupportRoleId}})).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync($"/api/v1/users/{user.Id}/invitation",new{})).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await Activate(user)).StatusCode);
        }
    }
    [NeonFact]
    public async Task Generated_asset_codes_are_unique_immutable_searchable_and_preserve_existing_codes()
    {
        using var admin=await fixture.ClientAsync();var responses=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>admin.PostAsJsonAsync("/api/v1/assets",fixture.Asset())));
        Assert.All(responses,x=>Assert.Equal(HttpStatusCode.Created,x.StatusCode));
        var assets=await Task.WhenAll(responses.Select(x=>x.Content.ReadFromJsonAsync<JsonElement>()));var codes=assets.Select(x=>x.GetProperty("assetCode").GetString()).ToArray();
        Assert.Equal(codes.Length,codes.Distinct().Count());Assert.All(codes,x=>Assert.Matches("^AST-[0-9]{6,}$",x!));
        var first=assets[0];var id=first.GetProperty("id").GetInt64();var dto=fixture.Asset();dto.AssetCode="CLIENT-CODE";
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync("/api/v1/assets",dto)).StatusCode);
        dto.RowVersion=first.GetProperty("rowVersion").GetString();Assert.Equal(HttpStatusCode.BadRequest,(await admin.PutAsJsonAsync($"/api/v1/assets/{id}",dto)).StatusCode);
        dto.AssetCode=null;var updated=await admin.PutAsJsonAsync($"/api/v1/assets/{id}",dto);Assert.Equal(HttpStatusCode.OK,updated.StatusCode);Assert.Equal(codes[0],(await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assetCode").GetString());
        var found=await admin.GetFromJsonAsync<JsonElement>("/api/v1/assets?keyword="+codes[0]);Assert.Equal(1,found.GetProperty("totalItems").GetInt32());
        await using var scope=fixture.Factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Set<Asset>().AnyAsync(x=>x.AssetCode=="AST-000123"&&x.Name=="Tài sản cũ"));
        Assert.False(db.Database.HasPendingModelChanges());Assert.Equal(4,(await db.Database.GetAppliedMigrationsAsync()).Count());
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>scope.ServiceProvider.GetRequiredService<IUnitOfWork>().RunAsync(async()=>{
            await db.Database.ExecuteSqlRawAsync("UPDATE public.assets SET asset_code='ILLEGAL' WHERE asset_code='AST-000123'");return true;
        }));Assert.Equal("23514",error.SqlState);
        Assert.True(await db.Set<Asset>().AnyAsync(x=>x.AssetCode=="AST-000123"));
    }
}
