using System.Net;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Mvp;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ItAssetManagement.UnitTests;

public sealed class InvitationRolloutTests
{
    [Fact] public void Tokens_are_cryptographic_canonical_unique_and_hashes_are_fixed_length()
    {
        var tokens=Enumerable.Range(0,100).Select(_=>ActivationTokens.Create()).ToArray();Assert.Equal(100,tokens.Distinct().Count());
        Assert.All(tokens,t=>{Assert.Equal(43,t.Length);Assert.Equal(32,ActivationTokens.Hash(t).Length);Assert.DoesNotContain(t,Convert.ToHexString(ActivationTokens.Hash(t)));});
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("bad")][InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA!")][InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    public void Invalid_tokens_fail_with_generic_error(string? token)=>Assert.Equal("ACTIVATION_INVALID_OR_EXPIRED",Assert.Throws<BusinessException>(()=>ActivationTokens.Hash(token)).Code);
    [Fact] public void New_migration_preserves_baseline_tables_keys_and_defaults_for_old_accounts()
    {
        using var db=new AppDbContextFactory().CreateDbContext([]);var model=db.GetService<IDesignTimeModel>().Model;
        Assert.Equal(19,model.GetEntityTypes().Count());Assert.Equal(44,model.GetEntityTypes().Sum(x=>x.GetForeignKeys().Count()));Assert.Equal(86,model.GetEntityTypes().Sum(x=>x.GetCheckConstraints().Count()));
        Assert.True((bool)model.FindEntityType(typeof(ItAssetManagement.Domain.Entities.User))!.FindProperty("IsEmailConfirmed")!.GetDefaultValue()!);
        var migration=new LecturerFeedbackIdentityAndAssetCodes();Assert.Empty(migration.UpOperations.OfType<CreateTableOperation>());Assert.Equal(4,migration.UpOperations.OfType<AddColumnOperation>().Count());
        Assert.All(migration.UpOperations.OfType<AddColumnOperation>(),x=>Assert.Equal("users",x.Table));
        var sql=db.GetService<IMigrator>().GenerateScript(FullSchemaBaseline.Migration,"20261011103054_LecturerFeedbackIdentityAndAssetCodes");
        Assert.Contains("pg_advisory_xact_lock(837112, 100)",sql);Assert.Contains("lock_timeout = '5s'",sql);Assert.Contains("LECTURER_FEEDBACK_BASELINE_MISMATCH",sql);Assert.Contains("START TRANSACTION",sql);
        Assert.Contains("assets_code_immutable",sql);Assert.DoesNotContain("UPDATE public.assets",sql);Assert.DoesNotContain("DELETE FROM",sql);Assert.False(db.Database.HasPendingModelChanges());
        Assert.Throws<NotSupportedException>(()=>migration.DownOperations);
    }
    [Theory][InlineData("https://example.test","Production","Resend",200,"Sent")][InlineData("https://example.test","Production","Resend",500,"Failed")][InlineData("http://example.test","Production","Resend",200,"NotConfigured")][InlineData("https://example.test","Production","Sandbox",200,"NotConfigured")]
    public async Task Email_API_is_HTTPS_configured_and_never_returns_provider_secrets(string url,string environment,string mode,int status,string expected)
    {
        var handler=new Handler((HttpStatusCode)status);var token=ActivationTokens.Create();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Email:Mode"]=mode,["Email:PublicBaseUrl"]=url,["Email:ApiKey"]="synthetic-provider-key",["Email:From"]="test@example.test"}).Build();
        var sender=new InvitationEmailSender(new HttpClient(handler),config,new EnvironmentStub(environment));
        Assert.Equal(expected,await sender.SendAsync("user@example.test","<test>",token,default));
        if(expected is "Sent" or "Failed"){Assert.Equal("https://api.resend.com/emails",handler.Target);Assert.Contains("/#/activate?token=",handler.Body);Assert.Contains("&lt;test&gt;",handler.Body);Assert.DoesNotContain("synthetic-provider-key",handler.Body);Assert.NotNull(handler.IdempotencyKey);}
        else Assert.Null(handler.Target);
    }
    [Fact] public async Task Sandbox_writes_only_synthetic_invitation_outside_repository_and_requires_no_HTTP()
    {
        var directory=Path.Combine(Path.GetTempPath(),"itam-invitation-"+Guid.NewGuid().ToString("N"));var token=ActivationTokens.Create();var handler=new Handler(HttpStatusCode.OK);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Email:Mode"]="Sandbox",["Email:PublicBaseUrl"]="http://localhost:5091",["Email:SandboxDirectory"]=directory}).Build();
        var sender=new InvitationEmailSender(new HttpClient(handler),config,new EnvironmentStub("Development"));
        Assert.Equal("Sandboxed",await sender.SendAsync("synthetic@example.test","Nhân viên",token,default));Assert.Null(handler.Target);
        var file=Assert.Single(Directory.GetFiles(directory,"*.eml"));var contents=await File.ReadAllTextAsync(file);Assert.Contains(token,contents);Assert.Contains("30 phút",contents);Assert.DoesNotContain("Password:",contents);
        File.Delete(file);Directory.Delete(directory); // Exact fresh test files only; no business data.
    }
    private sealed class Handler(HttpStatusCode result):HttpMessageHandler
    {
        public string? Target,Body,IdempotencyKey;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Target=request.RequestUri!.AbsoluteUri;var json=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));Body=json.RootElement.GetProperty("html").GetString();IdempotencyKey=request.Headers.GetValues("Idempotency-Key").Single();return new(result){Content=new StringContent("provider-body-must-not-leak")};}
    }
    [Theory]
    [InlineData("https://example.test","Production",201,"Sent")]
    [InlineData("https://example.test","Production",400,"Failed")]
    [InlineData("http://example.test","Production",201,"NotConfigured")]
    public async Task Brevo_Email_API_is_HTTPS_configured_and_never_returns_provider_secrets(string url,string environment,int status,string expected)
    {
        var handler=new BrevoHandler((HttpStatusCode)status);var token=ActivationTokens.Create();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Email:Mode"]="Brevo",
            ["Email:PublicBaseUrl"]=url,
            ["BREVO_API_KEY"]="synthetic-brevo-key",
            ["Email:From"]="hethongqlcntt@gmail.com",
            ["Email:SenderName"]="Hệ thống Quản lý tài sản CNTT"
        }).Build();
        var sender=new InvitationEmailSender(new HttpClient(handler),config,new EnvironmentStub(environment));
        Assert.Equal(expected,await sender.SendAsync("user@example.test","<test>",token,default));
        if(expected is "Sent" or "Failed")
        {
            Assert.Equal("https://api.brevo.com/v3/smtp/email",handler.Target);
            Assert.Equal("synthetic-brevo-key",handler.ApiKey);
            Assert.Contains("/#/activate?token=",handler.HtmlContent);
            Assert.Contains("&lt;test&gt;",handler.HtmlContent);
            Assert.DoesNotContain("synthetic-brevo-key",handler.RawBody);
            Assert.Equal("hethongqlcntt@gmail.com",handler.SenderEmail);
        }
        else Assert.Null(handler.Target);
    }
    [Fact]
    public async Task Brevo_account_details_are_HTML_encoded_without_password_or_API_key()
    {
        var handler=new BrevoHandler(HttpStatusCode.Created);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Email:Mode"]="Brevo",["Email:PublicBaseUrl"]="https://example.test",
            ["BREVO_API_KEY"]="synthetic-brevo-key",["Email:From"]="sender@example.test"
        }).Build();
        var account=new InvitationAccountInfo("<staff>","staff@example.test","<script>name</script>","NV<1>","0900<test>","Kỹ thuật <A>");
        var sender=new InvitationEmailSender(new HttpClient(handler),config,new EnvironmentStub("Production"));
        Assert.Equal("Sent",await sender.SendAsync("staff@example.test",account.DisplayName,ActivationTokens.Create(),default,account));
        Assert.Contains("&lt;staff&gt;",handler.HtmlContent);
        Assert.Contains("&lt;script&gt;name&lt;/script&gt;",handler.HtmlContent);
        Assert.Contains("Kỹ thuật &lt;A&gt;",handler.HtmlContent);
        Assert.DoesNotContain("<script>",handler.HtmlContent);
        Assert.DoesNotContain("synthetic-brevo-key",handler.RawBody);
        Assert.DoesNotContain("passwordHash",handler.RawBody);
    }
    [Fact]
    public async Task Brevo_uses_HTTPS_Render_hostname_fallback_for_activation_link()
    {
        var handler=new BrevoHandler(HttpStatusCode.Created);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Email:Mode"]="Brevo",["RENDER_EXTERNAL_HOSTNAME"]="synthetic.onrender.com",
            ["BREVO_API_KEY"]="synthetic-brevo-key",["Email:From"]="sender@example.test"
        }).Build();
        var sender=new InvitationEmailSender(new HttpClient(handler),config,new EnvironmentStub("Production"));
        Assert.Equal("Sent",await sender.SendAsync("staff@example.test","Nhân viên",ActivationTokens.Create(),default));
        Assert.Contains("https://synthetic.onrender.com/#/activate?token=",handler.HtmlContent);
        Assert.DoesNotContain("localhost",handler.HtmlContent);
    }
    [Fact]
    public async Task Brevo_missing_key_does_not_send_HTTP_or_claim_delivery()
    {
        var handler=new BrevoHandler(HttpStatusCode.Created);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Email:Mode"]="Brevo",["Email:PublicBaseUrl"]="https://example.test",
            ["BREVO_API_KEY"]="",["Email:From"]="sender@example.test"
        }).Build();
        var sender=new InvitationEmailSender(new HttpClient(handler),config,new EnvironmentStub("Production"));
        Assert.Equal("NotConfigured",await sender.SendAsync("staff@example.test","Nhân viên",ActivationTokens.Create(),default));
        Assert.Null(handler.Target);
    }
    // Provider tests use BrevoHandler only. Token persistence and activation are
    // exercised by the isolated HTTP integration tests, never by a live email here.
    private sealed class BrevoHandler(HttpStatusCode result):HttpMessageHandler
    {
        public string? Target,ApiKey,HtmlContent,SenderEmail,RawBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Target=request.RequestUri!.AbsoluteUri;
            ApiKey=request.Headers.GetValues("api-key").SingleOrDefault();
            RawBody=await request.Content!.ReadAsStringAsync(ct);
            var json=System.Text.Json.JsonDocument.Parse(RawBody);
            HtmlContent=json.RootElement.GetProperty("htmlContent").GetString();
            SenderEmail=json.RootElement.GetProperty("sender").GetProperty("email").GetString();
            return new(result){Content=new StringContent("{\"messageId\":\"<synthetic-msg-id>\"}")};
        }
    }
    private sealed class EnvironmentStub(string environment):IHostEnvironment
    {public string EnvironmentName{get;set;}=environment;public string ApplicationName{get;set;}="Test";public string ContentRootPath{get;set;}=AppContext.BaseDirectory;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
