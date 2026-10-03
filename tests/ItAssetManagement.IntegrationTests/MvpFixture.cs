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
    private readonly Dictionary<string, string> _tokens = [];
    public async Task InitializeAsync()
    {
        // Explicit opt-in: ordinary dotnet test never silently writes to a cloud database.
        if (Environment.GetEnvironmentVariable("ITAM_RUN_NEON_TESTS") != "1") return;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var config = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(root, "src/ItAssetManagement.Api/appsettings.Development.json")));
        if (!NeonConnectionPolicy.TryCreate(config.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString(), out var settings))
            throw new InvalidOperationException("Development connection unavailable.");
        const string testDatabase = "it_asset_management_m1_verify_20261002";
        if (!testDatabase.StartsWith("it_asset_management_m1_verify_", StringComparison.Ordinal) || settings!.Database == testDatabase)
            throw new InvalidOperationException("Isolation guard failed.");
        settings.Database = testDatabase;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString, ["Database:EnableReadinessProbe"] = "true",
              ["Logging:LogLevel:Default"] = "None", ["Frontend:Enabled"] = "false" }));
        });
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Existing isolated schema is required; tests never create or migrate it.");
        await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunAsync(new(AdminEmail, Password));
        DepartmentId = await db.Set<Department>().Where(x => x.Code == "IT").Select(x => x.Id).SingleAsync();
        TypeId = await db.Set<AssetType>().Where(x => x.Code == "LAPTOP").Select(x => x.Id).SingleAsync();
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
    public AssetRequest Asset(string? suffix = null) => new() { AssetCode = Prefix + (suffix ?? Guid.NewGuid().ToString("N")[..8]),
        Name = "Fixture laptop", AssetTypeId = TypeId, OwningDepartmentId = DepartmentId, PurchasePrice = 1200.25m };
    public async Task DisposeAsync() { if (Factory != null) await Factory.DisposeAsync(); }
}
public sealed class NeonFactAttribute : FactAttribute
{
    public NeonFactAttribute() { if (Environment.GetEnvironmentVariable("ITAM_RUN_NEON_TESTS") != "1") Skip = "Explicit isolated Neon opt-in required."; }
}
