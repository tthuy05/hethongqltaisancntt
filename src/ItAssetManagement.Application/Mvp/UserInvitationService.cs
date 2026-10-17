using System.Security.Cryptography;
using ItAssetManagement.Domain.Entities;

namespace ItAssetManagement.Application.Mvp;

// Profile details included in the invitation email so the recipient can verify their account info.
// Never contains password, hash, token or security state.
public sealed record InvitationAccountInfo(string Username, string Email, string DisplayName, string? EmployeeCode, string? Phone, string? DepartmentName);
public interface IInvitationEmailSender
{
    // The token leaves the application only through the configured sender, never a DTO or audit.
    Task<string> SendAsync(string email, string displayName, string token, CancellationToken ct, InvitationAccountInfo? accountInfo = null);
}
public sealed class ActivateAccountRequest
{
    public string Token { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}
public sealed record InvitationResult(long UserId, string EmailStatus, DateTime? ExpiresAt);
public static class ActivationTokens
{
    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
    public static byte[] Hash(string? token)
    {
        if (token == null || token.Length != 43 || token.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_')) throw Invalid();
        try
        {
            var bytes=Convert.FromBase64String(token.Replace('-','+').Replace('_','/')+"=");
            if(bytes.Length!=32 || Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_')!=token) throw Invalid();
            return SHA256.HashData(bytes);
        }
        catch(FormatException){throw Invalid();}
    }
    public static BusinessException Invalid()=>new(400,"ACTIVATION_INVALID_OR_EXPIRED","Liên kết không hợp lệ hoặc đã hết hạn. Hãy yêu cầu Admin gửi lại lời mời.");
}
public sealed class UserInvitationService(IRepository repo, IUnitOfWork uow, IPasswordService passwords,
    IAuditWriter audit, IActor actor, IInvitationEmailSender email, TimeProvider clock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private async Task AuthorizeAsync(string permission,CancellationToken ct)
    {
        if(actor.UserId is not >0 || !actor.Has(permission))throw new BusinessException(403,"FORBIDDEN","Không có quyền gửi lời mời tài khoản.");
        var caller=await repo.FirstAsync(repo.Query<User>().Where(x=>x.Id==actor.UserId),ct);
        if(caller==null||!caller.IsEmailConfirmed||!caller.IsActive||caller.IsAdminLocked||caller.LockoutEndUtc>Now||actor.TokenVersion!=caller.TokenVersion)
            throw new BusinessException(401,"UNAUTHORIZED","Phiên đăng nhập không còn hợp lệ.");
        var grants=from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id
                   join grant in repo.Query<RolePermission>() on role.Id equals grant.RoleId join p in repo.Query<Permission>() on grant.PermissionId equals p.Id
                   where link.UserId==actor.UserId&&role.IsActive&&p.IsActive&&p.Code==permission select p.Id;
        if(!await repo.AnyAsync(grants,ct))throw new BusinessException(403,"FORBIDDEN","Quyền đã bị thu hồi.");
    }
    public async Task<UserDto> CreateAsync(CreateUserRequest request,CancellationToken ct)
    {
        if(actor.UserId is not >0||!actor.Has(Permissions.UserCreate))throw new BusinessException(403,"FORBIDDEN","Không có quyền tạo tài khoản.");
        UserValidation.Create(request);
        var token=ActivationTokens.Create(); var hash=ActivationTokens.Hash(token);
        var id=await uow.RunAsync(async()=>
        {
            await repo.LockAsync("master-data",ct); await repo.LockAsync("users-identity",ct);
            await AuthorizeAsync(Permissions.UserCreate,ct); await AuthorizeAsync(Permissions.RoleAssign,ct);
            var normalizedEmail=request.Email.ToUpperInvariant();var normalizedUsername=request.Username.ToUpperInvariant();
            if(await repo.AnyAsync(repo.Query<User>().Where(x=>x.NormalizedEmail==normalizedEmail),ct))throw new BusinessException(409,"USER_EMAIL_CONFLICT","Email đã tồn tại. Không tạo tài khoản trùng; dùng chức năng gửi lại lời mời nếu cần.");
            if(await repo.AnyAsync(repo.Query<User>().Where(x=>x.NormalizedUsername==normalizedUsername),ct))throw new BusinessException(409,"USER_USERNAME_CONFLICT","Tên đăng nhập đã tồn tại.");
            if(request.DepartmentId!=null&&!await repo.AnyAsync(repo.Query<Department>().Where(x=>x.Id==request.DepartmentId&&x.IsActive),ct))throw Validation.Invalid("departmentId","Phòng ban phải đang hoạt động.");
            var roles=await repo.ListAsync(repo.Query<Role>().Where(x=>request.RoleIds.Contains(x.Id)&&x.IsActive),ct);
            if(roles.Count!=request.RoleIds.Length)throw Validation.Invalid("roleIds","Nhóm phải tồn tại và đang hoạt động.");
            var admin=from link in repo.Query<UserRole>() join role in repo.Query<Role>() on link.RoleId equals role.Id where link.UserId==actor.UserId&&role.IsActive&&role.Code=="ADMIN_IT" select link.Id;
            if(!await repo.AnyAsync(admin,ct))
            {
                var codes=from grant in repo.Query<RolePermission>() join p in repo.Query<Permission>() on grant.PermissionId equals p.Id where request.RoleIds.Contains(grant.RoleId)&&p.IsActive select p.Code;
                var currentCodes=await repo.ListAsync(EffectivePermissionQuery.ForUser(repo,actor.UserId!.Value),ct);
                if(roles.Any(x=>x.Code=="ADMIN_IT")||(await repo.ListAsync(codes,ct)).Any(code=>!currentCodes.Contains(code)))throw new BusinessException(403,"PRIVILEGE_ESCALATION","Không được cấp nhóm có quyền vượt quyền hiện tại.");
            }
            var user=new User {Username=request.Username,NormalizedUsername=normalizedUsername,Email=request.Email,NormalizedEmail=normalizedEmail,
                FullName=request.DisplayName,EmployeeCode=request.EmployeeCode,DepartmentId=request.DepartmentId,Phone=request.Phone,CreatedByUserId=actor.UserId,
                IsEmailConfirmed=false,ActivationTokenHash=hash,ActivationExpiresAtUtc=Now.AddMinutes(30),InvitationEmailStatus="Pending"};
            user.PasswordHash=passwords.Hash(user,ActivationTokens.Create()); // Unusable random secret, never sent or retained.
            repo.Add(user);await repo.SaveAsync(ct);
            audit.Record("users.invite",user,actor.UserId,"USER",after:new{user.DepartmentId,user.IsEmailConfirmed,roleCount=roles.Count});
            foreach(var role in roles)
            {
                var link=new UserRole {UserId=user.Id,RoleId=role.Id,AssignedByUserId=actor.UserId,AssignedAtUtc=Now};
                repo.Add(link);await repo.SaveAsync(ct);audit.Record("users.roles.add",link,actor.UserId,"USER",after:new{link.UserId,link.RoleId});
            }
            await repo.SaveAsync(ct);return user.Id;
        },ct);
        await DeliverAsync(id,token,hash,ct);
        return await ReadAsync(id,ct);
    }
    public async Task<InvitationResult> ResendAsync(long id,CancellationToken ct)
    {
        if(actor.UserId is not >0||!actor.Has(Permissions.UserCreate))throw new BusinessException(403,"FORBIDDEN","Không có quyền gửi lại lời mời.");
        if(id<=0)throw Validation.Invalid("userId","Người dùng không hợp lệ.");
        var token=ActivationTokens.Create();var hash=ActivationTokens.Hash(token);
        await uow.RunAsync(async()=>
        {
            await repo.LockAsync("users-identity",ct);await AuthorizeAsync(Permissions.UserCreate,ct);
            var user=await repo.FirstAsync(repo.Query<User>(true).Where(x=>x.Id==id),ct)??throw new BusinessException(404,"USER_NOT_FOUND","Không tìm thấy người dùng.");
            if(user.IsEmailConfirmed)throw new BusinessException(409,"ACCOUNT_ALREADY_ACTIVATED","Tài khoản đã kích hoạt, không gửi lại lời mời.");
            if(!user.IsActive||user.IsAdminLocked)throw new BusinessException(409,"ACCOUNT_UNAVAILABLE","Tài khoản đang ngừng hoạt động hoặc bị khóa.");
            user.ActivationTokenHash=hash;user.ActivationExpiresAtUtc=Now.AddMinutes(30);user.InvitationEmailStatus="Pending";
            audit.Record("users.invitation.resend",user,actor.UserId,"USER",after:new{user.IsEmailConfirmed});await repo.SaveAsync(ct);return true;
        },ct);
        var status=await DeliverAsync(id,token,hash,ct);
        var current=await ReadAsync(id,ct);return new(id,status,current.ActivationExpiresAt);
    }
    private async Task<string> DeliverAsync(long id,string token,byte[] hash,CancellationToken ct)
    {
        var user=await repo.FirstAsync(repo.Query<User>().Where(x=>x.Id==id),ct)!;
        var departmentName=user!.DepartmentId!=null?await repo.FirstAsync(repo.Query<Department>().Where(d=>d.Id==user.DepartmentId).Select(d=>d.Name),ct):null;
        var accountInfo=new InvitationAccountInfo(user.Username,user.Email,user.FullName,user.EmployeeCode,user.Phone,departmentName);
        string status;
        try{status=await email.SendAsync(user.Email,user.FullName,token,ct,accountInfo);}
        catch(Exception error) when(error is not OperationCanceledException){status="Failed";}
        if(status is not ("Sent" or "Sandboxed" or "NotConfigured" or "Failed"))status="Failed";
        return await uow.RunAsync(async()=>
        {
            await repo.LockAsync("users-identity",ct);
            var tracked=await repo.FirstAsync(repo.Query<User>(true).Where(x=>x.Id==id),ct)!;
            if(tracked!.IsEmailConfirmed||tracked.ActivationTokenHash==null||!CryptographicOperations.FixedTimeEquals(tracked.ActivationTokenHash,hash))return tracked.InvitationEmailStatus;
            tracked.InvitationEmailStatus=status;
            audit.Record("users.invitation.email",tracked,actor.UserId,"USER",status=="Failed"?"FAILURE":"SUCCESS",after:new{emailStatus=status});
            await repo.SaveAsync(ct);return status;
        },ct);
    }
    public async Task ActivateAsync(ActivateAccountRequest request,CancellationToken ct)
    {
        var hash=ActivationTokens.Hash(request.Token);
        if(string.IsNullOrWhiteSpace(request.Password)||request.Password.Length is <12 or >256)throw Validation.Invalid("password","Mật khẩu cần 12–256 ký tự.");
        if(request.Password!=request.ConfirmPassword)throw Validation.Invalid("confirmPassword","Mật khẩu nhập lại chưa khớp.");
        await uow.RunAsync(async()=>
        {
            await repo.LockAsync("users-identity",ct);
            var user=await repo.FirstAsync(repo.Query<User>(true).Where(x=>x.ActivationTokenHash!=null&&x.ActivationTokenHash.SequenceEqual(hash)),ct);
            if(user==null||user.IsEmailConfirmed||!user.IsActive||user.IsAdminLocked||user.ActivationExpiresAtUtc<=Now||user.ActivationExpiresAtUtc==null)throw ActivationTokens.Invalid();
            user.PasswordHash=passwords.Hash(user,request.Password);user.IsEmailConfirmed=true;user.ActivationTokenHash=null;user.ActivationExpiresAtUtc=null;
            user.InvitationEmailStatus="Activated";UserAccountValidation.IncrementVersion(user);
            audit.Record("users.activate",user,null,"ANONYMOUS",after:new{user.IsEmailConfirmed});await repo.SaveAsync(ct);return true;
        },ct);
    }
    private async Task<UserDto> ReadAsync(long id,CancellationToken ct)=>await repo.FirstAsync(repo.Query<User>().Where(x=>x.Id==id).Select(x=>new UserDto
    {Id=x.Id,Username=x.Username,Email=x.Email,DisplayName=x.FullName,DepartmentId=x.DepartmentId,Phone=x.Phone,EmployeeCode=x.EmployeeCode,IsActive=x.IsActive,
        DepartmentName=repo.Query<Department>().Where(d=>d.Id==x.DepartmentId).Select(d=>d.Name).FirstOrDefault(),
        IsEmailConfirmed=x.IsEmailConfirmed,InvitationEmailStatus=x.InvitationEmailStatus,ActivationExpiresAt=x.ActivationExpiresAtUtc,CreatedAtUtc=x.CreatedAtUtc,RowVersion=Convert.ToBase64String(x.RowVersion)}),ct) ?? throw new BusinessException(404,"USER_NOT_FOUND","Không tìm thấy tài khoản.");
}
