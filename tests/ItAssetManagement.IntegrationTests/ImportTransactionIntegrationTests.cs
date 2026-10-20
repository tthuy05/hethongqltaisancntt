using System.Net.Http.Json;
using ItAssetManagement.Application.Import;
using ItAssetManagement.Application.Export;
using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class ImportTransactionIntegrationTests(MvpFixture fixture)
{
    internal sealed class ImportActor(long id) : IActor
    {
        public long? UserId => id; public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "POST"; public string? Path => "/isolated-fixture/import";
        public bool Has(string permission) => permission is Permissions.ImportAssets or Permissions.AssetCost;
    }
    private static void VerifyTarget(AppDbContext db)
    {
        var target = Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches(@"\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\z", target ?? "");
        Assert.Equal(target, db.Database.GetDbConnection().Database);
    }
    private byte[] Workbook(string serial, bool invalid = false) => SafeExcelWorkbookWriter.CreateWorkbook(
        ["Name", "AssetTypeId", "DepartmentId", "SerialNumber"],
        new object?[][] { ["Synthetic import", fixture.TypeId, fixture.DepartmentId, serial], [invalid ? "" : "Synthetic import 2", fixture.TypeId, fixture.DepartmentId, serial + "B"] });
    private static AssetImportService Service(IServiceProvider provider, IActor actor, IAuditWriter? audit = null) => new(
        provider.GetRequiredService<IRepository>(), provider.GetRequiredService<IUnitOfWork>(), actor,
        audit ?? new AuditWriter(provider.GetRequiredService<AppDbContext>(), actor), provider.GetRequiredService<IAssetCodeGenerator>());
    private static Task<AssetImportResult> Import(AssetImportService service, byte[] bytes, bool preview = false) =>
        service.ImportAssetsAsync(new MemoryStream(bytes), "synthetic.xlsx", ImportSecurityLimits.AllowedMimeType, bytes.Length, new(preview));

    [NeonFact]
    public async Task Preview_is_read_only_and_commit_creates_generated_codes_history_and_transactional_audit()
    {
        using var client = await fixture.ClientAsync(); var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var actor = new ImportActor(user.Id); var service = Service(scope.ServiceProvider, actor);
        var serial = "IMP" + Guid.NewGuid().ToString("N"); var bytes = Workbook(serial);
        var assetsBefore = await db.Set<Asset>().CountAsync(); var auditBefore = await db.Set<AuditLog>().CountAsync();
        var preview = await Import(service, bytes, true);
        Assert.Equal(2, preview.ValidRows); Assert.False(preview.Committed); Assert.Empty(preview.Errors);
        Assert.Equal(assetsBefore, await db.Set<Asset>().CountAsync()); Assert.Equal(auditBefore, await db.Set<AuditLog>().CountAsync());
        Assert.False(db.ChangeTracker.HasChanges());
        var result = await Import(service, bytes);
        Assert.True(result.Committed); Assert.Equal(2, result.ValidRows);
        var assets = await db.Set<Asset>().AsNoTracking().Where(a => a.SerialNumber == serial || a.SerialNumber == serial + "B").ToListAsync();
        Assert.Equal(2, assets.Count); Assert.All(assets, a => { Assert.Matches(@"\AAST-[0-9]{6,}\z", a.AssetCode); Assert.Equal(16, a.RowVersion.Length); Assert.Equal("IN_STOCK", a.CurrentStatus); });
        var ids = assets.Select(a => a.Id).ToArray();
        Assert.Equal(2, await db.Set<AssetStatusHistory>().CountAsync(h => ids.Contains(h.AssetId) && h.Source == "IMPORT"));
        Assert.Equal(3, await db.Set<AuditLog>().CountAsync(a => a.CorrelationId == actor.CorrelationId));
    }

    private sealed class FailSecondAssetAudit(IAuditWriter inner) : IAuditWriter
    {
        private int count;
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null)
        {
            if (entity is Asset && ++count == 2) throw new InvalidOperationException("Synthetic post-persistence failure");
            inner.Record(action, entity, actorId, actorType, outcome, before, after, failureCode);
        }
    }
    [NeonFact]
    public async Task Invalid_rows_and_failure_after_asset_insert_preserve_all_existing_business_data()
    {
        using var client = await fixture.ClientAsync(); var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider; var db = provider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var actor = new ImportActor(user.Id); var serial = "ROLL" + Guid.NewGuid().ToString("N");
        var assetsBefore = await db.Set<Asset>().CountAsync(); var historyBefore = await db.Set<AssetStatusHistory>().CountAsync();
        var invalid = await Import(Service(provider, actor), Workbook(serial, true));
        Assert.False(invalid.Committed); Assert.NotEmpty(invalid.Errors);
        var auditBefore = await db.Set<AuditLog>().CountAsync();
        var failing = Service(provider, actor, new FailSecondAssetAudit(new AuditWriter(db, actor)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Import(failing, Workbook(serial)));
        Assert.Equal(assetsBefore, await db.Set<Asset>().CountAsync()); Assert.Equal(historyBefore, await db.Set<AssetStatusHistory>().CountAsync());
        Assert.Equal(auditBefore, await db.Set<AuditLog>().CountAsync()); Assert.False(db.ChangeTracker.HasChanges());
    }

    [NeonFact]
    public async Task Concurrent_imports_of_same_serial_commit_once_without_partial_history()
    {
        using var client = await fixture.ClientAsync(); var user = (await client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me"))!;
        var serial = "RACE" + Guid.NewGuid().ToString("N"); var bytes = Workbook(serial);
        async Task<AssetImportResult> Attempt()
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope(); VerifyTarget(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            return await Import(Service(scope.ServiceProvider, new ImportActor(user.Id)), bytes);
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, r => r.Committed);
        Assert.Contains(results.Single(r => !r.Committed).Errors, e => e.ErrorCode == ImportErrorCodes.DuplicateDb);
        await using var verify = fixture.Factory.Services.CreateAsyncScope(); var db = verify.ServiceProvider.GetRequiredService<AppDbContext>(); VerifyTarget(db);
        var ids = await db.Set<Asset>().Where(a => a.SerialNumber == serial || a.SerialNumber == serial + "B").Select(a => a.Id).ToArrayAsync();
        Assert.Equal(2, ids.Length); Assert.Equal(2, await db.Set<AssetStatusHistory>().CountAsync(h => ids.Contains(h.AssetId)));
    }
}
