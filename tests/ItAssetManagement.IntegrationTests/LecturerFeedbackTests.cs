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
public sealed class LecturerFeedbackTests(MvpFixture fixture)
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
    public async Task Custom_lead_and_support_same_department_have_distinct_rights_and_revocation_is_immediate()
    {
        using var admin=await fixture.ClientAsync();
        var created=await admin.PostAsJsonAsync("/api/v1/roles",new{name="Technical Support Lead",description="Quyền tài sản thực tế; ticket theo phòng chưa triển khai"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var role=(await created.Content.ReadFromJsonAsync<RoleDetailDto>())!;
        Assert.False(role.IsSystem);Assert.Empty(role.Permissions);var originalVersion=role.RowVersion;
        var matrix=(await admin.GetFromJsonAsync<List<PermissionOption>>("/api/v1/roles/matrix"))!;
        var ids=matrix.Where(x=>new[]{Permissions.AssetRead,Permissions.AssetUpdate,Permissions.DepartmentRead,Permissions.TypeRead}.Contains(x.Code)).Select(x=>x.Id).ToArray();
        var response=await admin.PutAsJsonAsync($"/api/v1/roles/{role.Id}/permissions",new{permissionIds=ids,rowVersion=role.RowVersion});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        role=(await response.Content.ReadFromJsonAsync<RoleDetailDto>())!;
        Assert.NotEqual(originalVersion, role.RowVersion);
        var loaded=(await admin.GetFromJsonAsync<RoleDetailDto>($"/api/v1/roles/{role.Id}"))!;Assert.Equal(ids.Order(),loaded.Permissions.Select(x=>x.Id).Order());
        var lead=await Invite(role.Id);var support=await Invite();Assert.Equal(lead.DepartmentId,support.DepartmentId);
        Assert.Equal(HttpStatusCode.OK,(await Activate(lead)).StatusCode);Assert.Equal(HttpStatusCode.OK,(await Activate(support)).StatusCode);
        using var leader=await fixture.ClientAsync(lead.Email);using var staff=await fixture.ClientAsync(support.Email);
        var assetResponse=await admin.PostAsJsonAsync("/api/v1/assets",fixture.Asset());Assert.Equal(HttpStatusCode.Created,assetResponse.StatusCode);
        var asset=await assetResponse.Content.ReadFromJsonAsync<JsonElement>();var id=asset.GetProperty("id").GetInt64();var dto=fixture.Asset();dto.RowVersion=asset.GetProperty("rowVersion").GetString();dto.Name="Lead sửa";
        Assert.Equal(HttpStatusCode.Forbidden,(await staff.PutAsJsonAsync($"/api/v1/assets/{id}",dto)).StatusCode);
        var updated=await leader.PutAsJsonAsync($"/api/v1/assets/{id}",dto);Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await leader.PostAsJsonAsync("/api/v1/roles",new{name="Không được tự nâng quyền"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await leader.GetAsync("/api/v1/users")).StatusCode);
        var revoke=await admin.PutAsJsonAsync($"/api/v1/roles/{role.Id}/permissions",new{permissionIds=ids.Where(x=>x!=matrix.Single(p=>p.Code==Permissions.AssetUpdate).Id).ToArray(),rowVersion=role.RowVersion});
        Assert.Equal(HttpStatusCode.OK,revoke.StatusCode);dto.RowVersion=(await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetString();
        Assert.Equal(HttpStatusCode.Forbidden,(await leader.PutAsJsonAsync($"/api/v1/assets/{id}",dto)).StatusCode);
        var account=(await admin.GetFromJsonAsync<UserAccountStateDto>($"/api/v1/users/{lead.Id}/account"))!;Assert.DoesNotContain(Permissions.AssetUpdate,account.EffectivePermissions);
    }
    [NeonFact]
    public async Task Matrix_denies_non_admin_and_protects_last_admin_dependencies_and_disabled_modules()
    {
        using var admin=await fixture.ClientAsync();using var manager=await fixture.ClientAsync(fixture.ManagerEmail);
        Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/api/v1/roles/matrix")).StatusCode);
        var roles=(await admin.GetFromJsonAsync<PagedResponse<RoleSummaryDto>>("/api/v1/roles?pageSize=100"))!;
        var adminId=roles.Items.Single(x=>x.Code=="ADMIN_IT").Id;var detail=(await admin.GetFromJsonAsync<RoleDetailDto>($"/api/v1/roles/{adminId}"))!;
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/v1/roles/{adminId}/permissions",new{permissionIds=Array.Empty<long>(),rowVersion=detail.RowVersion})).StatusCode);
        var custom=(await (await admin.PostAsJsonAsync("/api/v1/roles",new{name="Empty fixture group"})).Content.ReadFromJsonAsync<RoleDetailDto>())!;
        var catalog=(await admin.GetFromJsonAsync<List<PermissionOption>>("/api/v1/roles/matrix"))!;
        var updateId=catalog.Single(x=>x.Code==Permissions.AssetUpdate).Id;
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PutAsJsonAsync($"/api/v1/roles/{custom.Id}/permissions",new{permissionIds=new[]{updateId},rowVersion=custom.RowVersion})).StatusCode);
        await using(var disabled=fixture.Factory.WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Features:AssignmentApi:Enabled"]="false"}))))
        {
            using var blocked=disabled.CreateClient();
            var ownLogin=await blocked.PostAsJsonAsync("/api/v1/auth/login",new{email=MvpFixture.AdminEmail,password=MvpFixture.Password});
            Assert.Equal(HttpStatusCode.OK,ownLogin.StatusCode);
            var ownToken=(await ownLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
            blocked.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",ownToken);
            Assert.Equal(HttpStatusCode.NotFound,(await blocked.GetAsync("/api/v1/asset-assignments")).StatusCode);
            var unavailable=(await blocked.GetFromJsonAsync<List<PermissionOption>>("/api/v1/roles/matrix"))!;
            Assert.All(unavailable.Where(x=>x.Code.StartsWith("assignments.")),x=>Assert.False(x.Available));
            var forbiddenIds=unavailable.Where(x=>x.Code==Permissions.AssignmentRead).Select(x=>x.Id).ToArray();
            Assert.Equal(HttpStatusCode.BadRequest,(await blocked.PutAsJsonAsync($"/api/v1/roles/{custom.Id}/permissions",new{permissionIds=forbiddenIds,rowVersion=custom.RowVersion})).StatusCode);
        }
        var user=await Invite(custom.Id);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PatchAsJsonAsync($"/api/v1/roles/{custom.Id}/status",new{status="Inactive",reason="test",rowVersion=custom.RowVersion})).StatusCode);
        Assert.False(user.IsEmailConfirmed);
    }
    [NeonFact]
    public async Task Bootstrap_seed_does_not_restore_a_revoked_default_group_permission()
    {
        using var admin=await fixture.ClientAsync();
        var roles=(await admin.GetFromJsonAsync<PagedResponse<RoleSummaryDto>>("/api/v1/roles?pageSize=100"))!;
        var roleId=roles.Items.Single(x=>x.Code=="SYSTEM_MANAGER").Id;
        var original=(await admin.GetFromJsonAsync<RoleDetailDto>($"/api/v1/roles/{roleId}"))!;
        try
        {
            var selected=original.Permissions.Where(x=>x.Code!=Permissions.AssetCost).Select(x=>x.Id).ToArray();
            Assert.Equal(HttpStatusCode.OK,(await admin.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions",new{permissionIds=selected,rowVersion=original.RowVersion})).StatusCode);
            await using var scope=fixture.Factory.Services.CreateAsyncScope();
            Assert.Equal(0,(await scope.ServiceProvider.GetRequiredService<ItAssetManagement.Infrastructure.Mvp.DevelopmentSeed>().RunAsync(new(MvpFixture.AdminEmail,MvpFixture.Password))).Added);
            var current=(await admin.GetFromJsonAsync<RoleDetailDto>($"/api/v1/roles/{roleId}"))!;Assert.DoesNotContain(current.Permissions,x=>x.Code==Permissions.AssetCost);
        }
        finally
        {
            var current=(await admin.GetFromJsonAsync<RoleDetailDto>($"/api/v1/roles/{roleId}"))!;
            Assert.Equal(HttpStatusCode.OK,(await admin.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions",new{permissionIds=original.Permissions.Select(x=>x.Id).ToArray(),rowVersion=current.RowVersion})).StatusCode);
        }
    }
    [NeonFact]
    public async Task Delegated_identity_operator_cannot_grant_admin_or_permissions_they_do_not_hold()
    {
        using var admin=await fixture.ClientAsync();
        var group=(await (await admin.PostAsJsonAsync("/api/v1/roles",new{name="Delegated identity operator"})).Content.ReadFromJsonAsync<RoleDetailDto>())!;
        var catalog=(await admin.GetFromJsonAsync<List<PermissionOption>>("/api/v1/roles/matrix"))!;
        var selected=catalog.Where(x=>new[]{Permissions.UserRead,Permissions.UserCreate,Permissions.RoleRead,Permissions.RoleAssign,Permissions.RolePermissionsRead}.Contains(x.Code)).Select(x=>x.Id).ToArray();
        Assert.Equal(HttpStatusCode.OK,(await admin.PutAsJsonAsync($"/api/v1/roles/{group.Id}/permissions",new{permissionIds=selected,rowVersion=group.RowVersion})).StatusCode);
        var user=await Invite(group.Id);Assert.Equal(HttpStatusCode.OK,(await Activate(user)).StatusCode);
        using var delegated=await fixture.ClientAsync(user.Email);
        Assert.Equal(HttpStatusCode.Forbidden,(await delegated.GetAsync("/api/v1/roles/matrix")).StatusCode);
        var roles=(await admin.GetFromJsonAsync<PagedResponse<RoleSummaryDto>>("/api/v1/roles?pageSize=100"))!;
        var profile=(await delegated.GetFromJsonAsync<UserDto>($"/api/v1/users/{user.Id}"))!;
        foreach(var roleCode in new[]{"ADMIN_IT","SYSTEM_MANAGER"})
        {
            var target=roles.Items.Single(x=>x.Code==roleCode).Id;
            Assert.Equal(HttpStatusCode.Forbidden,(await delegated.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles",new{roleIds=new[]{target},rowVersion=profile.RowVersion})).StatusCode);
            var key=fixture.Prefix+Guid.NewGuid().ToString("N")[..10];
            Assert.Equal(HttpStatusCode.Forbidden,(await delegated.PostAsJsonAsync("/api/v1/users",new{username=key,email=key+"@fixture.test",displayName="Không được nâng quyền",roleIds=new[]{target}})).StatusCode);
        }
        var empty=(await (await admin.PostAsJsonAsync("/api/v1/roles",new{name="No access until reviewed"})).Content.ReadFromJsonAsync<RoleDetailDto>())!;
        var allowedKey=fixture.Prefix+Guid.NewGuid().ToString("N")[..10];
        Assert.Equal(HttpStatusCode.Created,(await delegated.PostAsJsonAsync("/api/v1/users",new{username=allowedKey,email=allowedKey+"@fixture.test",displayName="Được mời trong phạm vi quyền",roleIds=new[]{empty.Id}})).StatusCode);
        var unchanged=(await admin.GetFromJsonAsync<UserAccountStateDto>($"/api/v1/users/{user.Id}/account"))!;
        Assert.Equal(new[]{group.Id},unchanged.RoleIds);
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
