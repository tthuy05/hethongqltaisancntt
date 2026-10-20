using System.Reflection;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ItAssetManagement.UnitTests;

public sealed class FullSchemaTests
{
    [Theory]
    [InlineData("maintenance_histories",4)]
    [InlineData("softwares",0)]
    [InlineData("software_licenses",1)]
    [InlineData("license_assignments",5)]
    [InlineData("replacement_rules",1)]
    [InlineData("replacement_recommendations",3)]
    public void New_tables_match_baseline_keys_and_nullable_relationships(string table,int foreignKeys)
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var entity = db.GetService<IDesignTimeModel>().Model.GetEntityTypes().Single(e => e.GetTableName() == table);
        Assert.Equal(foreignKeys,entity.GetForeignKeys().Count());
        Assert.Equal("public",entity.GetSchema());
        Assert.Equal(typeof(long),Assert.Single(entity.FindPrimaryKey()!.Properties).ClrType);
        Assert.All(entity.GetForeignKeys(),fk => Assert.Equal(DeleteBehavior.NoAction,fk.DeleteBehavior));
        Assert.Equal(table != "maintenance_histories",entity.FindProperty("RowVersion") is not null);
        foreach (var property in entity.GetProperties().Where(p => p.Name.EndsWith("AtUtc",StringComparison.Ordinal) || p.Name.StartsWith("Effective",StringComparison.Ordinal)))
            Assert.Equal("timestamp with time zone",property.GetColumnType());
    }

    [Fact]
    public void New_migration_matches_six_table_model_diff_and_never_mutates_old_objects()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var old = db.GetService<IModelRuntimeInitializer>().Initialize(new AddAssignmentMaintenance().TargetModel,designTime:true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var diff = db.GetService<IMigrationsModelDiffer>().GetDifferences(old.GetRelationalModel(),current.GetRelationalModel());
        var migration = new CompleteBaselineV1();
        var creates = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(FullSchemaBaseline.NewTables.Order(),creates.Select(t => t.Name).Order());
        Assert.Equal(14,creates.Sum(t => t.ForeignKeys.Count));
        Assert.All(diff.Concat(migration.UpOperations),operation => Assert.True(operation switch
        {
            CreateTableOperation t => t.Schema == "public" && FullSchemaBaseline.NewTables.Contains(t.Name),
            CreateIndexOperation i => i.Schema == "public" && FullSchemaBaseline.NewTables.Contains(i.Table),
            SqlOperation s => !s.SuppressTransaction && s.Sql.Contains("CREATE TRIGGER maintenance_histories_append_only") && !s.Sql.Contains("CREATE FUNCTION"),
            _ => false
        },"Non-additive operation: "+operation.GetType().Name));
        Assert.Equal(diff.Count+1,migration.UpOperations.Count);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(FullSchemaBaseline.Migration,db.Database.GetMigrations().Last());
    }

    [Fact]
    public void Offline_SQL_contains_partial_expression_indexes_and_history_trigger_only_for_new_tables()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var sql = db.GetService<IMigrator>().GenerateScript(NeonAssignmentMaintenanceSetup.Migration,FullSchemaBaseline.Migration);
        foreach (var index in new[] { "uq_softwares_code", "uq_software_licenses_code", "uq_replacement_rules_code_version", "uq_replacement_rules_one_current", "uq_license_assignments_active_user", "uq_license_assignments_active_asset", "uq_replacement_recommendations_current_asset" })
            Assert.Contains("CREATE UNIQUE INDEX "+index,sql);
        Assert.Contains("FOR EACH STATEMENT EXECUTE FUNCTION public.reject_history_mutation()",sql);
        Assert.Contains("INCLUDE (estimated_replacement_cost)",sql);
        foreach (var forbidden in new[] { "DROP ","DELETE FROM","UPDATE public.","ALTER TABLE public.assets","ALTER TABLE public.users","ALTER TABLE public.maintenance_tickets" }) Assert.DoesNotContain(forbidden,sql);
        Assert.Null(db.Database.GetConnectionString());
    }

    [Fact]
    public void Key_capacity_and_derived_status_are_not_cached_or_exposed_as_new_services()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var license = db.Model.FindEntityType(typeof(SoftwareLicense))!;
        Assert.Null(license.FindProperty("UsedQuantity")); Assert.Null(license.FindProperty("LicenseStatus"));
        Assert.Equal("bytea",license.FindProperty("LicenseKeyCiphertext")!.GetColumnType());
        Assert.Equal("jsonb",db.Model.FindEntityType(typeof(ReplacementRecommendation))!.FindProperty("EvaluationSnapshotJson")!.GetColumnType());
        Assert.Equal(18,license.FindProperty("PurchaseCost")!.GetPrecision()); Assert.Equal(2,license.FindProperty("PurchaseCost")!.GetScale());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Maintenance_history_is_append_only_even_in_an_audited_write(bool delete)
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var history = new MaintenanceHistory { Id=1,MaintenanceTicketId=1,EventType="CREATED",CorrelationId=Guid.NewGuid() };
        db.Attach(history);
        if (delete) db.Remove(history); else history.Comment="rewrite";
        typeof(AppDbContext).GetMethod("BeginAuditedWrite",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(db,null);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Null(db.Database.GetConnectionString());
    }

    [Theory]
    [InlineData("Notes")]
    [InlineData("RevokedAtUtc")]
    [InlineData("RevokedByUserId")]
    public void Revoked_license_allocation_cannot_be_rewritten(string property)
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var row = new LicenseAssignment { Id=1,SoftwareLicenseId=1,AssignedUserId=1,AssignedByUserId=1,
            AssignedAtUtc=DateTime.UtcNow.AddDays(-2),RevokedAtUtc=DateTime.UtcNow.AddDays(-1),RowVersion=new byte[16] };
        var entry=db.Attach(row);
        entry.Property(property).CurrentValue=property switch { "Notes" => "rewrite", "RevokedByUserId" => (object)2L, _ => DateTime.UtcNow };
        typeof(AppDbContext).GetMethod("BeginAuditedWrite",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(db,null);
        var error=Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Equal("Revoked license allocation history is immutable.",error.Message);
        Assert.Null(db.Database.GetConnectionString());
    }
}
