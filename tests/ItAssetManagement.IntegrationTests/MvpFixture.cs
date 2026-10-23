using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[CollectionDefinition("isolated-neon", DisableParallelization = true)]
public sealed class IsolatedCollection : ICollectionFixture<MvpFixture>;
public sealed class MvpFixture : IAsyncLifetime
{
    public const string Password = "Fixture-password-2026!";
    public const string AdminEmail = "admin.fixture@itasset.test";
    public string Prefix { get; } = "T" + Guid.NewGuid().ToString("N")[..12];
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public long DepartmentId { get; private set; }
    public long TypeId { get; private set; }
    public string SupportEmail { get; private set; } = null!;
    public string InactiveEmail { get; private set; } = null!;
    public string LockedEmail { get; private set; } = null!;
    public string ManagerEmail { get; private set; } = null!;
    public long SupportRoleId { get; private set; }
    public InvitationMailbox Mailbox { get; } = new();
    public TestClock Clock { get; } = new();
    private readonly Dictionary<string, string> _tokens = [];
    public async Task InitializeAsync()
    {
        // Explicit opt-in: ordinary dotnet test never silently writes to a cloud database.
        if (Environment.GetEnvironmentVariable("ITAM_RUN_NEON_TESTS") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configPath = Environment.GetEnvironmentVariable("ITAM_TEST_CONFIG_PATH") ?? Path.Combine(root, "src/ItAssetManagement.Api/appsettings.Development.json");
        var config = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(configPath));
        var testConnection = config.TryGetProperty("ConnectionStrings", out var connections)
            ? connections.GetProperty("DefaultConnection").GetString()
            : config.GetProperty("ConnectionStrings:NeonSetupConnection").GetString();
        if (!NeonConnectionPolicy.TryCreate(testConnection, out var settings))
            throw new InvalidOperationException("Development connection unavailable.");
        var testDatabase = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE") ?? "it_asset_management_m1_verify_20261002";
        if (!System.Text.RegularExpressions.Regex.IsMatch(testDatabase, "\\Ait_asset_management_(m1|full_schema)_verify_[a-z0-9_]{1,24}\\z") || settings!.Database == testDatabase)
            throw new InvalidOperationException("Isolation guard failed.");
        settings.Database = testDatabase;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services => {
                services.AddSingleton<IInvitationEmailSender>(Mailbox);
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IStartupFilter, IsolatedRequestAddress>();
            });
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString, ["Database:EnableReadinessProbe"] = "true",
              ["Logging:LogLevel:Default"] = "None", ["Frontend:Enabled"] = "false",
              ["Features:AssignmentApi:Enabled"] = "true" }));
        });
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Existing isolated schema is required; tests never create or migrate it.");
        await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunAsync(new(AdminEmail, Password));
        DepartmentId = await db.Set<Department>().Where(x => x.Code == "IT").Select(x => x.Id).SingleAsync();
        TypeId = await db.Set<AssetType>().Where(x => x.Code == "LAPTOP").Select(x => x.Id).SingleAsync();
        SupportRoleId = await db.Set<Role>().Where(x => x.Code == "TECHNICAL_SUPPORT").Select(x => x.Id).SingleAsync();
        SupportEmail = await UserAsync("support", "TECHNICAL_SUPPORT");
        ManagerEmail = await UserAsync("manager", "SYSTEM_MANAGER");
        InactiveEmail = await UserAsync("inactive", "ADMIN_IT", active: false);
        LockedEmail = await UserAsync("locked", "ADMIN_IT", locked: true);
    }
    public async Task<string> UserAsync(string suffix, string role, bool active = true, bool locked = false)
    {
        var email = Prefix + suffix + "@fixture.test";
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>(); var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        await uow.RunAsync(async () =>
        {
            var user = new User { Username = Prefix + suffix, NormalizedUsername = (Prefix + suffix).ToUpperInvariant(), Email = email,
                NormalizedEmail = email.ToUpperInvariant(), FullName = "Isolated test user", IsActive = active,
                IsAdminLocked = locked, AdminLockedAtUtc = locked ? DateTime.UtcNow : null };
            user.PasswordHash = hasher.Hash(user, Password); db.Add(user); await db.SaveChangesAsync();
            var link = new UserRole { UserId = user.Id, RoleId = await db.Set<Role>().Where(x => x.Code == role).Select(x => x.Id).SingleAsync(), AssignedAtUtc = DateTime.UtcNow };
            db.Add(link); await db.SaveChangesAsync(); audit.Record("test.fixture", user, null, "SYSTEM"); audit.Record("test.fixture", link, null, "SYSTEM");
            await db.SaveChangesAsync(); return true;
        });
        return email;
    }
    public async Task<HttpClient> ClientAsync(string? email = null)
    {
        var client = Factory.CreateClient();
        email ??= AdminEmail;
        if (_tokens.TryGetValue(email, out var cached))
        { client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cached); return client; }
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = email ?? AdminEmail, password = Password });
        if (!login.IsSuccessStatusCode) throw new InvalidOperationException("Fixture login failed with " + (int)login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        _tokens[email!] = token!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    public async Task<UserDto> ActivateUserAsync(UserDto user)
    {
        await using var scope=Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UserInvitationService>().ActivateAsync(new() { Token=Mailbox.Token(user.Email),Password=Password,ConfirmPassword=Password },default);
        using var admin=await ClientAsync();
        return (await admin.GetFromJsonAsync<UserDto>("/api/v1/users/"+user.Id))!;
    }
    public AssetRequest Asset(string? suffix = null) => new() {
        Name = "Fixture laptop", AssetTypeId = TypeId, OwningDepartmentId = DepartmentId, PurchasePrice = 1200.25m };
    public async Task DisposeAsync() { if (Factory != null) await Factory.DisposeAsync(); }
}
public sealed class InvitationMailbox : IInvitationEmailSender
{
    public string Status { get; set; } = "Sandboxed";
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> tokens = new();
    public string Token(string email) => tokens[email];
    public Task<string> SendAsync(string email, string displayName, string token, CancellationToken ct, InvitationAccountInfo? accountInfo = null)
    { tokens[email]=token; return Task.FromResult(Status); }
}
public sealed class TestClock : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
}
// Functional DB tests are not rate-limit tests: give each synthetic request its
// own address so long suites do not share a login bucket. Production is unchanged.
public sealed class IsolatedRequestAddress : IStartupFilter
{
    private int serial;
    public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
    {
        Microsoft.AspNetCore.Builder.UseExtensions.Use(app, async (context, continuation) => {
            var id=Interlocked.Increment(ref serial);
            context.Connection.RemoteIpAddress=new System.Net.IPAddress(new byte[]{127,1,(byte)(id>>8),(byte)id});
            await continuation();
        });
        next(app);
    };
}
public sealed class NeonFactAttribute : FactAttribute
{
    public NeonFactAttribute() { if (Environment.GetEnvironmentVariable("ITAM_RUN_NEON_TESTS") != "1") Skip = "Explicit isolated Neon opt-in required."; }
}
