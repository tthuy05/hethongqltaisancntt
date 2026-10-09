using System.Text.Json;
using ItAssetManagement.Application.Contracts.Database;
using ItAssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using ItAssetManagement.Api.Mvp;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using ItAssetManagement.Api.Hosting;
using Microsoft.AspNetCore.HttpOverrides;

var probeOnly = args.Contains("--verify-neon", StringComparer.Ordinal);
var setupOnly = args.Contains("--setup-neon-m1", StringComparer.Ordinal);
var inspectOnly = args.Contains("--inspect-neon-schema", StringComparer.Ordinal);
var seedOnly = args.Contains("--seed-development", StringComparer.Ordinal);
var demoOnly = args.Contains("--seed-m1-demo", StringComparer.Ordinal);
var roleCatalogOnly = args.Contains("--seed-role-catalog", StringComparer.Ordinal);
var auditReadOnly = args.Contains("--seed-audit-read", StringComparer.Ordinal);
var assignmentValidationOnly = args.Contains("--setup-assignment-validation", StringComparer.Ordinal);
var assignmentSetupOnly = args.Contains("--setup-assignment-maintenance", StringComparer.Ordinal);
string[] cliModes = ["--verify-neon", "--setup-neon-m1", "--inspect-neon-schema", "--seed-development", "--seed-m1-demo",
    "--seed-role-catalog", "--seed-audit-read", "--setup-assignment-validation", "--setup-assignment-maintenance"];
var modeCount = cliModes.Count(mode => args.Contains(mode, StringComparer.Ordinal));
var builder = WebApplication.CreateBuilder(args.Where(argument => !cliModes.Contains(argument, StringComparer.Ordinal)).ToArray());
if (modeCount > 0) builder.Logging.ClearProviders();
if (DeploymentHosting.PortUrl(builder.Configuration["PORT"], builder.Environment.IsDevelopment()) is { } portUrl)
    builder.WebHost.UseUrls(portUrl);
if (!builder.Environment.IsDevelopment() && DeploymentHosting.RenderAllowedHosts(builder.Configuration["RENDER"],
    builder.Configuration["RENDER_EXTERNAL_HOSTNAME"], builder.Configuration["AllowedHosts"]) is { } renderHosts)
    builder.Configuration["AllowedHosts"] = renderHosts;
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>(DeploymentHosting.ConfigureForwardedHeaders);
builder.Services.AddMvpOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BusinessExceptionHandler>();
builder.Services.AddControllers().ConfigureApplicationPartManager(parts =>
    parts.FeatureProviders.Add(new AssignmentControllerFeatureProvider(builder.Configuration)))
    .AddJsonOptions(options =>
{
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    var resolver = new DefaultJsonTypeInfoResolver();
    resolver.Modifiers.Add(type =>
    {
        if (type.Type == typeof(AssetDto))
            type.Properties.Single(p => p.Name == "purchasePrice").ShouldSerialize = (obj, _) => ((AssetDto)obj).CanReadCost;
    });
    options.JsonSerializerOptions.TypeInfoResolver = resolver;
});
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
    new BadRequestObjectResult(Errors.Problem(context.HttpContext, 400, "VALIDATION_ERROR", "Request không hợp lệ.",
        context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, _ => new[] { "Giá trị không hợp lệ hoặc thiếu field bắt buộc." }))));
builder.Services.AddMvpAuthentication();
builder.Services.AddScoped<IRepository, EfRepository>();
builder.Services.AddScoped<IUnitOfWork, AuditedUnitOfWork>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<AuthService>(); builder.Services.AddScoped<AssetService>(); builder.Services.AddScoped<MasterService>();
builder.Services.AddScoped<UserLookupService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IAccountPersistence, AccountPersistence>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<UserAccountReadService>();
builder.Services.AddScoped<RoleCatalogService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<IAssignmentSchemaReadiness, AssignmentSchemaReadiness>();
builder.Services.AddScoped<DevelopmentSeed>();
builder.Services.AddScoped<DevelopmentDemoSeed>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, _) =>
    { context.HttpContext.Response.Headers.RetryAfter = "60"; await Errors.WriteAsync(context.HttpContext, 429, "RATE_LIMITED", "Thử lại sau."); };
});
builder.Services.AddSingleton<IReadOnlyDatabaseProbe>(_ => new ReadOnlyDatabaseProbe(
    () => builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (NeonConnectionPolicy.TryCreate(builder.Configuration.GetConnectionString("DefaultConnection"), out var settings))
        options.UseNpgsql(settings!.ConnectionString, provider => provider.MigrationsHistoryTable("ef_migrations_history", "public"));
    // Startup never applies migrations; writes go through an audited unit of work.
});

var app = builder.Build();
if (modeCount > 1 || modeCount > 0 && !app.Environment.IsDevelopment())
{
    Console.WriteLine("{\"status\":\"DevelopmentOnlyOrConflictingMode\"}"); Environment.ExitCode = 2;
    await app.DisposeAsync(); return;
}
_ = app.Services.GetRequiredService<JwtSettings>(); // Production fails closed; development uses a per-process key.
DeploymentHosting.ValidateProductionDatabase(app.Configuration, app.Environment.IsDevelopment());
if (assignmentValidationOnly || assignmentSetupOnly)
{
    var result = await NeonAssignmentMaintenanceSetup.RunAsync(builder.Configuration.GetConnectionString("DefaultConnection"),
        assignmentValidationOnly, builder.Configuration.GetValue<bool>("Database:AssignmentSetup:SharedBackendCompatibilityConfirmed"));
    Console.WriteLine(JsonSerializer.Serialize(result));
    Environment.ExitCode = result.Status == "Verified" ? 0 : 2;
    await app.DisposeAsync(); return;
}
if (auditReadOnly)
{
    if (!app.Environment.IsDevelopment() || probeOnly || setupOnly || inspectOnly || seedOnly || demoOnly || roleCatalogOnly)
    { Console.WriteLine("{\"status\":\"DevelopmentOnlyOrConflictingMode\"}"); Environment.ExitCode = 2; }
    else
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var added = await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunAuditReadAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { status = "AuditReadSeeded", added }));
        }
        catch { Console.WriteLine("{\"status\":\"AuditReadSeedFailed\"}"); Environment.ExitCode = 2; }
    }
    await app.DisposeAsync(); return;
}
if (roleCatalogOnly)
{
    if (!app.Environment.IsDevelopment() || probeOnly || setupOnly || inspectOnly || seedOnly || demoOnly)
    { Console.WriteLine("{\"status\":\"DevelopmentOnlyOrConflictingMode\"}"); Environment.ExitCode = 2; }
    else
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var added = await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunRoleCatalogAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { status = "RoleCatalogSeeded", added }));
        }
        catch { Console.WriteLine("{\"status\":\"RoleCatalogSeedFailed\"}"); Environment.ExitCode = 2; }
    }
    await app.DisposeAsync(); return;
}
if (demoOnly)
{
    if (!app.Environment.IsDevelopment() || probeOnly || setupOnly || inspectOnly || seedOnly)
    { Console.WriteLine("{\"status\":\"DevelopmentOnlyOrConflictingMode\"}"); Environment.ExitCode = 2; }
    else
    {
        try
        {
            var credentials = await DemoBootstrap.ReadOrCreateAsync(builder.Configuration["demo-credentials-path"], app.Environment.ContentRootPath);
            await using var scope = app.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DevelopmentDemoSeed>().RunAsync(credentials);
            Console.WriteLine(JsonSerializer.Serialize(new { status = "DemoSeeded", counts = result }));
        }
        catch { Console.WriteLine("{\"status\":\"DemoSeedFailed\"}"); Environment.ExitCode = 2; }
    }
    await app.DisposeAsync(); return;
}
if (seedOnly)
{
    if (!app.Environment.IsDevelopment() || probeOnly || setupOnly || inspectOnly ||
        string.IsNullOrEmpty(builder.Configuration["bootstrap-credentials-path"]))
    { Console.WriteLine("{\"status\":\"DevelopmentOnlyOrMissingPrivateCredentialsFile\"}"); Environment.ExitCode = 2; }
    else
    {
        try
        {
            var credentials = JsonSerializer.Deserialize<SeedCredentials>(await File.ReadAllTextAsync(builder.Configuration["bootstrap-credentials-path"]!),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException();
            await using var scope = app.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DevelopmentSeed>().RunAsync(credentials);
            Console.WriteLine(JsonSerializer.Serialize(new { status = "Seeded", counts = result }));
        }
        catch { Console.WriteLine("{\"status\":\"SeedFailed\"}"); Environment.ExitCode = 2; }
    }
    await app.DisposeAsync(); return;
}
if (inspectOnly)
{
    if (!app.Environment.IsDevelopment() || probeOnly || setupOnly)
    {
        Console.WriteLine("{\"status\":\"DevelopmentOnlyOrConflictingMode\"}");
        Environment.ExitCode = 2;
    }
    else
    {
        var result = await NeonSchemaInspection.CheckAsync(builder.Configuration.GetConnectionString("DefaultConnection"),
            builder.Configuration["inspection-database"]);
        Console.WriteLine(JsonSerializer.Serialize(result));
        Environment.ExitCode = result.Status == "Verified" ? 0 : 2;
    }
    await app.DisposeAsync();
    return;
}
if (setupOnly)
{
    if (!app.Environment.IsDevelopment() || probeOnly)
    {
        Console.WriteLine("{\"status\":\"DevelopmentOnlyOrConflictingMode\"}");
        Environment.ExitCode = 2;
    }
    else
    {
        var result = await NeonM1Setup.RunAsync(builder.Configuration.GetConnectionString("DefaultConnection"),
            builder.Configuration["target-database"] ?? "", builder.Configuration["validation-database"] ?? "");
        Console.WriteLine(JsonSerializer.Serialize(result));
        Environment.ExitCode = result.Status == "Applied" ? 0 : 2;
    }
    await app.DisposeAsync();
    return;
}
if (probeOnly)
{
    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine("{\"status\":\"DevelopmentOnly\"}");
        Environment.ExitCode = 2;
    }
    else
    {
        var result = await app.Services.GetRequiredService<IReadOnlyDatabaseProbe>().CheckAsync();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = result.Status.ToString(),
            database = result.Database,
            postgresVersion = result.PostgreSqlVersion,
            userTableCount = result.UserTableCount,
            failureCode = result.FailureCode,
            operation = "READ_ONLY_PROBE"
        }));
        Environment.ExitCode = result.Status == DatabaseProbeStatus.Reachable ? 0 : 2;
    }
    await app.DisposeAsync();
    return;
}

app.UseForwardedHeaders(); // Only explicit trusted proxies/networks; before rate limits and audit metadata.
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next(context);
});
if (DeploymentHosting.FrontendDirectory(app.Environment, app.Configuration) is { } frontend)
{
    var files = new PhysicalFileProvider(frontend);
    app.Lifetime.ApplicationStopped.Register(files.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}
else if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Frontend:Enabled", true))
    app.Logger.LogWarning("Frontend build missing. Run pnpm build from the repository root.");
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Swagger:Enabled", true))
{
    var swagger = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../../artifacts/swagger"));
    if (File.Exists(Path.Combine(swagger, "index.html")))
    {
        var files = new PhysicalFileProvider(swagger);
        app.Lifetime.ApplicationStopped.Register(files.Dispose);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files, RequestPath = "/swagger" });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, RequestPath = "/swagger" });
    }
    else app.Logger.LogWarning("Development Swagger build missing. Run pnpm build from the repository root.");
}
app.UseRouting(); // Static middleware must run before endpoint selection (including fallback).
app.UseMiddleware<AssignmentRolloutMiddleware>(); // Closed before auth/Controller/DB-dependent business code.
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" })).AllowAnonymous();
app.MapGet("/health/version", (IConfiguration configuration) => Results.Ok(DeploymentRevision.Create(configuration))).AllowAnonymous();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapGet("/health/ready", async (IReadOnlyDatabaseProbe probe, CancellationToken cancellationToken) =>
    {
        if (!app.Configuration.GetValue<bool>("Database:EnableReadinessProbe"))
            return Results.Json(new { status = "ProbeDisabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        var result = await probe.CheckAsync(cancellationToken);
        return Results.Json(new { status = result.Status.ToString() },
            statusCode: result.Status == DatabaseProbeStatus.Reachable ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }).AllowAnonymous();
}
app.MapFallback("{**path}", () => Results.NotFound()).AllowAnonymous();
// Normal HTTP startup never migrates/seeds. Production serves only an explicitly packaged frontend.
app.Run();

public partial class Program;
