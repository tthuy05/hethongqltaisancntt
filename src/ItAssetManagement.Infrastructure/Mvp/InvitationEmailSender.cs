using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ItAssetManagement.Application.Mvp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ItAssetManagement.Infrastructure.Mvp;

// HTTPS email API works with Render Free's SMTP restrictions. Missing configuration
// is reported explicitly; neither startup nor tests send mail implicitly.
public sealed class InvitationEmailSender(HttpClient http, IConfiguration config, IHostEnvironment environment) : IInvitationEmailSender
{
    public async Task<string> SendAsync(string email, string displayName, string token, CancellationToken ct, InvitationAccountInfo? accountInfo = null)
    {
        var mode=config["Email:Mode"];
        var publicUrl=config["Email:PublicBaseUrl"];
        if(string.IsNullOrWhiteSpace(publicUrl))
        {
            if(environment.IsDevelopment()) publicUrl="http://localhost:5080";
            else if(!string.IsNullOrWhiteSpace(config["RENDER_EXTERNAL_HOSTNAME"])) publicUrl=$"https://{config["RENDER_EXTERNAL_HOSTNAME"]}";
            else if(!string.IsNullOrWhiteSpace(config["RENDER_EXTERNAL_URL"])) publicUrl=config["RENDER_EXTERNAL_URL"];
        }
        if(!Uri.TryCreate(publicUrl,UriKind.Absolute,out var root)||!string.IsNullOrEmpty(root.UserInfo)||root.Fragment!=""||root.Query!=""||
            root.Scheme!="https"&&!(environment.IsDevelopment()&&root.IsLoopback&&root.Scheme=="http"))return "NotConfigured";
        var link=root.AbsoluteUri.TrimEnd('/')+"/#/activate?token="+Uri.EscapeDataString(token);
        var accountSection = "";
        if (accountInfo != null)
        {
            static string Row(string label, string? value) => string.IsNullOrWhiteSpace(value) ? "" :
                $"""<tr><td style="padding: 6px 12px; font-size: 14px; color: #64748b; white-space: nowrap;">{WebUtility.HtmlEncode(label)}</td><td style="padding: 6px 12px; font-size: 14px; color: #1e293b; font-weight: 500;">{WebUtility.HtmlEncode(value)}</td></tr>""";
            accountSection = $"""
            <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin: 16px 0;">
                <p style="font-size: 14px; font-weight: 600; color: #334155; margin: 0 0 12px 0;">Thông tin tài khoản của bạn:</p>
                <table style="border-collapse: collapse; width: 100%;">
                    {Row("Họ và tên", accountInfo.DisplayName)}
                    {Row("Tên đăng nhập", accountInfo.Username)}
                    {Row("Email", accountInfo.Email)}
                    {Row("Mã nhân viên", accountInfo.EmployeeCode)}
                    {Row("Phòng ban", accountInfo.DepartmentName)}
                    {Row("Số điện thoại", accountInfo.Phone)}
                </table>
            </div>
            """;
        }
        var html=$"""
        <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #ffffff; color: #1e293b;">
            <div style="display: none; max-height: 0px; overflow: hidden;">Xác nhận kích hoạt tài khoản của bạn để bắt đầu sử dụng hệ thống.</div>
            <h2 style="color: #0f172a; margin-top: 0; font-size: 20px;">Chào mừng bạn tham gia hệ thống</h2>
            <p style="font-size: 15px; line-height: 1.5;">Xin chào <strong>{WebUtility.HtmlEncode(displayName)}</strong>,</p>
            <p style="font-size: 15px; line-height: 1.5;">Bạn được tạo tài khoản trên <strong>Hệ thống Quản lý tài sản CNTT</strong> với thông tin sau:</p>
            {accountSection}
            <p style="font-size: 15px; line-height: 1.5;">Vui lòng nhấn vào nút dưới đây để thiết lập mật khẩu và hoàn tất kích hoạt tài khoản:</p>
            <div style="margin: 28px 0; text-align: left;">
                <a href="{WebUtility.HtmlEncode(link)}" style="background-color: #2563eb; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; font-size: 15px;">Kích hoạt tài khoản và tạo mật khẩu</a>
            </div>
            <p style="font-size: 13px; color: #64748b; line-height: 1.5;">Nếu nút bấm không hoạt động, bạn có thể sao chép liên kết sau vào trình duyệt:<br/><a href="{WebUtility.HtmlEncode(link)}" style="color: #2563eb; word-break: break-all;">{WebUtility.HtmlEncode(link)}</a></p>
            <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
            <p style="font-size: 13px; color: #64748b; line-height: 1.5; margin: 0;"><strong>Lưu ý:</strong> Liên kết chỉ dùng một lần, hết hạn sau 30 phút. Không gửi mật khẩu qua email. Nếu liên kết hết hạn, hãy liên hệ Admin.</p>
        </div>
        """;
        if(mode=="Sandbox"&&environment.IsDevelopment())
        {
            var directory=config["Email:SandboxDirectory"];
            if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathFullyQualified(directory))return "NotConfigured";
            // Sandbox output contains the synthetic test link. Keep it outside Git and webroot.
            var resolved=Path.GetFullPath(directory);
            if(resolved.StartsWith(Path.GetFullPath(environment.ContentRootPath),StringComparison.OrdinalIgnoreCase))return "NotConfigured";
            for(var ancestor=new DirectoryInfo(resolved);ancestor!=null;ancestor=ancestor.Parent)
                if(Directory.Exists(Path.Combine(ancestor.FullName,".git"))||File.Exists(Path.Combine(ancestor.FullName,".git")))return "NotConfigured";
            Directory.CreateDirectory(resolved);
            await File.WriteAllTextAsync(Path.Combine(resolved,Guid.NewGuid().ToString("N")+".eml"),
                "To: "+email+"\r\nSubject: Kích hoạt tài khoản tài sản CNTT\r\nContent-Type: text/html; charset=utf-8\r\n\r\n"+html,ct);
            return "Sandboxed";
        }
        var brevoApiKey=config["BREVO_API_KEY"]??config["Email:ApiKey"]??Environment.GetEnvironmentVariable("BREVO_API_KEY")??(OperatingSystem.IsWindows()?Environment.GetEnvironmentVariable("BREVO_API_KEY",EnvironmentVariableTarget.User):null);
        var fromEmail=config["Email:From"]??"hethongqlcntt@gmail.com";
        var senderName=config["Email:SenderName"]??"Hệ thống Quản lý tài sản CNTT";
        if(mode=="Brevo"||(string.IsNullOrWhiteSpace(mode)&&!string.IsNullOrWhiteSpace(brevoApiKey)))
        {
            if(string.IsNullOrWhiteSpace(brevoApiKey)||string.IsNullOrWhiteSpace(fromEmail))return "NotConfigured";
            using var brevoRequest=new HttpRequestMessage(HttpMethod.Post,"https://api.brevo.com/v3/smtp/email");
            brevoRequest.Headers.Add("api-key",brevoApiKey);
            brevoRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            brevoRequest.Content=JsonContent.Create(new
            {
                sender=new{name=senderName,email=fromEmail},
                to=new[]{new{email,name=string.IsNullOrWhiteSpace(displayName)?email:displayName}},
                subject="Kích hoạt tài khoản tài sản CNTT",
                htmlContent=html
            });
            using var brevoResponse=await http.SendAsync(brevoRequest,ct);
            return brevoResponse.IsSuccessStatusCode?"Sent":"Failed"; // Provider bodies and credentials are never logged/returned.
        }
        if(mode!="Resend"||string.IsNullOrWhiteSpace(config["Email:ApiKey"])||string.IsNullOrWhiteSpace(config["Email:From"]))return "NotConfigured";
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.resend.com/emails");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",config["Email:ApiKey"]);
        request.Headers.Add("Idempotency-Key","invitation/"+Convert.ToHexString(ActivationTokens.Hash(token)));
        request.Content=JsonContent.Create(new{from=config["Email:From"],to=new[]{email},subject="Kích hoạt tài khoản tài sản CNTT",html});
        using var response=await http.SendAsync(request,ct);
        return response.IsSuccessStatusCode?"Sent":"Failed"; // Provider bodies and credentials are never logged/returned.
    }
}
