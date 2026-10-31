using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ItAssetManagement.UnitTests;

public sealed class ReplacementSeverityMigrationTests
{
    [Fact]
    public void Migration_only_adds_nullable_severity_and_its_constraint_without_backfill()
    {
        var migration=new AddReplacementRuleSeverity();
        Assert.Equal(2,migration.UpOperations.Count);
        var column=Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("replacement_rules",column.Table);Assert.Equal("severity",column.Name);
        Assert.True(column.IsNullable);Assert.Null(column.DefaultValue);Assert.Null(column.DefaultValueSql);
        var check=Assert.Single(migration.UpOperations.OfType<AddCheckConstraintOperation>());
        Assert.Contains("severity IS NULL",check.Sql);Assert.Contains("CRITICAL",check.Sql);
        using var db=new AppDbContextFactory().CreateDbContext([]);
        var sql=db.GetService<IMigrator>().GenerateScript("20261024043835_AddUserPermissions","20261030024812_AddReplacementRuleSeverity");
        Assert.Contains("START TRANSACTION",sql);Assert.DoesNotContain("UPDATE ",sql);Assert.DoesNotContain("DELETE FROM",sql);Assert.DoesNotContain("DROP TABLE",sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
