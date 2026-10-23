using ItAssetManagement.Application.Export;
using ItAssetManagement.Application.Import;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalExcelIntegrationTests
{
    private sealed class Actor(long? id) : IActor
    {
        public long? UserId => id;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST";
        public string? Path => "/local-isolated-excel";
        public bool Has(string permission) => permission is Permissions.ImportAssets or Permissions.ExportAssets or Permissions.AssetCost;
    }
    private sealed class FailingAudit(IAuditWriter inner) : IAuditWriter
    {
        private int count;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null)
        {
            if (entity is Asset && ++count == 2) throw new InvalidOperationException("Synthetic second-asset audit failure");
            inner.Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
        }
    }

    [LocalPostgresFact]
    public async Task Workbook_preview_commit_export_and_audit_failure_are_atomic_with_password_authentication()
    {
        await using var db = LocalSequenceAuthenticationTests.Database();
        await db.Database.MigrateAsync();
        var repo = new EfRepository(db); var uow = new AuditedUnitOfWork(db);
        var bootstrap = new AuditWriter(db, new Actor(null));
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var department = new Department { Code = "TEST" + suffix, Name = "Local import department" };
        var type = new AssetType { Code = "TEST" + suffix, Name = "Local import type", DefaultUsefulLifeMonths = 36 };
        var user = new User { Username = "local" + suffix, NormalizedUsername = ("local" + suffix).ToUpperInvariant(),
            Email = suffix + "@fixture.test", NormalizedEmail = (suffix + "@fixture.test").ToUpperInvariant(),
            FullName = "Local Excel fixture", PasswordHash = "not-a-login-hash", IsEmailConfirmed = true };
        await uow.RunAsync(async () => {
            db.AddRange(department, type, user); await db.SaveChangesAsync();
            foreach (var entity in new object[] { department, type, user }) bootstrap.Record("test.fixture", entity, null, "SYSTEM");
            await db.SaveChangesAsync(); return true;
        });
        var actor = new Actor(user.Id); var audit = new AuditWriter(db, actor);
        var service = new AssetImportService(repo, uow, actor, audit, new AssetCodeGenerator(db));
        byte[] Workbook(string tag, bool invalid = false) => SafeExcelWorkbookWriter.CreateWorkbook(
            ["Name", "AssetTypeId", "DepartmentId", "SerialNumber", "PurchasePrice"],
            new object?[][] { ["=SUM(1,2)", type.Id, department.Id, suffix + tag + "1", 12.25m],
                [invalid ? "" : "Fixture 2", type.Id, department.Id, suffix + tag + "2", 0m] });
        Task<AssetImportResult> Import(byte[] bytes, bool preview = false, AssetImportService? target = null) =>
            (target ?? service).ImportAssetsAsync(new MemoryStream(bytes), "fixture.xlsx", ImportSecurityLimits.AllowedMimeType, bytes.Length, new(preview));
        var before = await db.Set<Asset>().CountAsync(); var auditBefore = await db.Set<AuditLog>().CountAsync();
        var bytes = Workbook("ok"); var preview = await Import(bytes, true);
        Assert.Equal(2, preview.ValidRows); Assert.False(preview.Committed);
        Assert.Equal(before, await db.Set<Asset>().CountAsync()); Assert.Equal(auditBefore, await db.Set<AuditLog>().CountAsync());
        BusinessCodeGeneratorEngine.ResetEnsuredSequences(); // Exercise isolated authenticated DDL inside import UoW.
        var committed = await Import(bytes); Assert.True(committed.Committed);
        var imported = await db.Set<Asset>().Where(x => x.SerialNumber!.StartsWith(suffix + "ok")).ToListAsync();
        Assert.Equal(2, imported.Count); Assert.All(imported, x => Assert.Matches("^AST-[0-9]{6,}$", x.AssetCode));
        var ids = imported.Select(x => x.Id).ToArray();
        Assert.Equal(2, await db.Set<AssetStatusHistory>().CountAsync(x => ids.Contains(x.AssetId) && x.Source == "IMPORT"));
        var exported = await new AssetExportService(repo, actor).ExportAssetsAsync(new() { DepartmentId = department.Id });
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(exported.Bytes)))
        using (var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()))
        { var xml = await reader.ReadToEndAsync(); Assert.Contains("12.25", xml); Assert.DoesNotContain("<f>", xml); }
        var invalid = await Import(Workbook("bad", true)); Assert.False(invalid.Committed); Assert.NotEmpty(invalid.Errors);
        Assert.Equal(before + 2, await db.Set<Asset>().CountAsync());
        var historyBefore = await db.Set<AssetStatusHistory>().CountAsync(); auditBefore = await db.Set<AuditLog>().CountAsync();
        var failing = new AssetImportService(repo, uow, actor, new FailingAudit(audit), new AssetCodeGenerator(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Import(Workbook("rollback"), target: failing));
        Assert.Equal(before + 2, await db.Set<Asset>().CountAsync());
        Assert.Equal(historyBefore, await db.Set<AssetStatusHistory>().CountAsync());
        Assert.Equal(auditBefore, await db.Set<AuditLog>().CountAsync());
        Assert.False(db.ChangeTracker.HasChanges());

        // Real multipart/controller + SQL path, synthetic authentication only.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = "", ["Frontend:Enabled"] = "false", ["Logging:LogLevel:Default"] = "None",
                ["Features:ImportExportApi:Enabled"] = "true"
            }));
            builder.ConfigureServices(services => {
                services.AddDbContext<ItAssetManagement.Infrastructure.Data.AppDbContext>(options => options.UseNpgsql(
                    Environment.GetEnvironmentVariable("ITAM_LOCAL_POSTGRES_TEST_CONNECTION"), p => p.MigrationsHistoryTable("ef_migrations_history", "public")));
                services.RemoveAll<IActor>(); services.AddSingleton<IActor>(actor);
                services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, PreparedQueryContractTests.Handler>("fixture", _ => {});
            });
        });
        using var client = factory.CreateClient();
        var template = await client.GetAsync("/api/v1/import/assets/template"); Assert.True(template.IsSuccessStatusCode);
        Assert.Empty(SafeExcelWorkbookParser.ParseAssetWorkbook(new MemoryStream(await template.Content.ReadAsByteArrayAsync())));
        async Task<AssetImportResult> Upload(bool dryRun)
        {
            using var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(Workbook("http")); content.Headers.ContentType = new(ImportSecurityLimits.AllowedMimeType);
            form.Add(content, "file", "fixture.xlsx");
            using var response = await client.PostAsync("/api/v1/import/assets?dryRun=" + dryRun.ToString().ToLowerInvariant(), form);
            Assert.True(response.IsSuccessStatusCode, "Isolated import HTTP status " + (int)response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<AssetImportResult>())!;
        }
        Assert.False((await Upload(true)).Committed); Assert.True((await Upload(false)).Committed);
        using var download = await client.GetAsync("/api/v1/export/assets?departmentId=" + department.Id);
        Assert.True(download.IsSuccessStatusCode); Assert.Equal(ExportSecurityLimits.ContentType, download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(before + 4, await db.Set<Asset>().CountAsync());
    }
}
