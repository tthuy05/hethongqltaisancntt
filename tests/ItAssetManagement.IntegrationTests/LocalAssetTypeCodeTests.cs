using ItAssetManagement.Application.Mvp;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;

namespace ItAssetManagement.IntegrationTests;

[Collection("isolated-neon")]
public sealed class LocalAssetTypeCodeTests
{
    private sealed class Actor(long? userId = null) : IActor
    {
        public long? UserId => userId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string? Method => "CLI";
        public string? Path => "/isolated/type-codes";
        public bool Has(string permission) => true;
    }
    private sealed class RejectAudit : IAuditWriter
    {
        public void Record(string action, object? entity, long? actorId, string actorType, string outcome = "SUCCESS", object? before = null, object? after = null, string? failureCode = null)
            => throw new InvalidOperationException("Synthetic audit failure");
    }
    [LocalPostgresFact]
    public async Task Audit_failure_rolls_back_conversion_and_allows_clean_retry()
    {
        await using var db=LocalSequenceAuthenticationTests.Database();await db.Database.MigrateAsync();
        var audit=new AuditWriter(db,new Actor());var type=new AssetType {Code="ROLLBACK"+Guid.NewGuid().ToString("N"),Name="Rollback type"};
        await new AuditedUnitOfWork(db).RunAsync(async()=>{db.Add(type);await db.SaveChangesAsync();audit.Record("test.create",type,null,"SYSTEM");await db.SaveChangesAsync();return true;});
        var oldCode=type.Code;
        var generator=new AssetTypeCodeGenerator(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new AssetTypeCodeConversion(db,generator,new RejectAudit()).RunAsync());
        Assert.Equal(oldCode,(await db.Set<AssetType>().AsNoTracking().SingleAsync(x=>x.Id==type.Id)).Code);
        var changes=await new AssetTypeCodeConversion(db,generator,audit).RunAsync();
        Assert.Contains(changes,x=>x.Id==type.Id);
    }
    [LocalPostgresFact]
    public async Task Conversion_preserves_ids_references_and_audit_and_is_idempotent()
    {
        await using var db = LocalSequenceAuthenticationTests.Database();
        await db.Database.MigrateAsync();
        var actor = new Actor(); var audit = new AuditWriter(db, actor);
        var type = new AssetType { Code = "LEGACY" + Guid.NewGuid().ToString("N"), Name = "Legacy printer" };
        await new AuditedUnitOfWork(db).RunAsync(async () => {db.Add(type);audit.Record("test.create", type, null, "SYSTEM");await db.SaveChangesAsync();return true;});
        db.ChangeTracker.Clear();
        var before = await db.Set<AssetType>().AsNoTracking().OrderBy(x=>x.Id).Select(x=>new{x.Id,x.Name,x.DefaultUsefulLifeMonths,x.IsActive}).ToListAsync();
        var references = await db.Set<Asset>().AsNoTracking().OrderBy(x=>x.Id).Select(x=>new{x.Id,x.AssetTypeId,x.PurchaseCost,x.AssetCode}).ToListAsync();
        var generator = new AssetTypeCodeGenerator(db);
        var converter = new AssetTypeCodeConversion(db,generator,audit);
        var changes = await converter.RunAsync();
        Assert.Contains(changes,x=>x.Id==type.Id);
        Assert.All(changes,x=>Assert.Matches("^LTS[0-9]{6,}$",x.NewCode));
        Assert.Equal(before,await db.Set<AssetType>().AsNoTracking().OrderBy(x=>x.Id).Select(x=>new{x.Id,x.Name,x.DefaultUsefulLifeMonths,x.IsActive}).ToListAsync());
        Assert.Equal(references,await db.Set<Asset>().AsNoTracking().OrderBy(x=>x.Id).Select(x=>new{x.Id,x.AssetTypeId,x.PurchaseCost,x.AssetCode}).ToListAsync());
        Assert.Equal(changes.Count,await db.Set<AuditLog>().CountAsync(x=>x.CorrelationId==actor.CorrelationId&&x.Action=="asset-types.code.convert"));
        Assert.Empty(await converter.RunAsync());
        var next=await generator.NextAsync(default); Assert.DoesNotContain(changes,x=>x.NewCode==next);
        var suffix=Guid.NewGuid().ToString("N");
        var user=new User { Username=suffix, NormalizedUsername=suffix.ToUpperInvariant(), Email=suffix+"@fixture.test", NormalizedEmail=(suffix+"@fixture.test").ToUpperInvariant(), FullName="Type test", PasswordHash="not-a-login", IsEmailConfirmed=true };
        await new AuditedUnitOfWork(db).RunAsync(async()=>{db.Add(user);await db.SaveChangesAsync();audit.Record("test.create",user,null,"SYSTEM");await db.SaveChangesAsync();return true;});
        var userActor=new Actor(user.Id);
        var service=new MasterService(new EfRepository(db),new AuditedUnitOfWork(db),new AuditWriter(db,userActor),userActor,assetTypeCodes:generator);
        var created=await service.WriteAsync(false,null,new MasterRequest{Code="FORGED",Name="Generated printer"},default);
        Assert.Matches("^LTS[0-9]{6,}$",created.Code);Assert.NotEqual("FORGED",created.Code);
        await Assert.ThrowsAsync<BusinessException>(()=>service.WriteAsync(false,created.Id,new MasterRequest{Code="CHANGED",Name=created.Name,RowVersion=created.RowVersion},default));
    }
}
