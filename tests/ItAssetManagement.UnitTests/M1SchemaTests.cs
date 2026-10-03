using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ItAssetManagement.UnitTests;

public sealed class M1SchemaTests
{
    [Fact]
    public void Offline_factory_has_no_connection_and_model_matches_migration()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        Assert.Null(context.Database.GetConnectionString());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(NeonM1Setup.Tables.Order(),context.Model.GetEntityTypes().Select(e => e.GetTableName()).Order());
        Assert.All(context.Model.GetEntityTypes(),e =>
        {
            Assert.Equal("public",e.GetSchema());
            Assert.Equal(typeof(long),e.FindPrimaryKey()!.Properties.Single().ClrType);
            Assert.All(e.GetProperties(),p => Assert.Matches("^[a-z][a-z0-9_]*$",p.GetColumnName()));
            Assert.All(e.GetIndexes(),i => Assert.Matches("^[a-z][a-z0-9_]*$",i.GetDatabaseName()));
        });
    }

    [Fact]
    public void Tokens_are_six_application_managed_columns_not_native_rowversion()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;
        var tokens = model.GetEntityTypes().SelectMany(e => e.GetProperties()).Where(p => p.IsConcurrencyToken).ToArray();
        Assert.Equal(6,tokens.Length);
        Assert.All(tokens,p =>
        {
            Assert.Equal("row_version",p.GetColumnName()); Assert.Equal("bytea",p.GetColumnType());
            Assert.False(p.IsNullable); Assert.Equal(ValueGenerated.Never,p.ValueGenerated);
            Assert.Contains(p.DeclaringType as IReadOnlyEntityType is { } entity ? entity.GetCheckConstraints() : [],
                c => c.Sql == "octet_length(row_version) = 16");
        });
    }

    [Fact]
    public void Initial_migration_is_additive_and_preserves_custom_database_objects()
    {
        var migration = new InitialM1();
        Assert.Equal(10,migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.DoesNotContain(migration.UpOperations,o => o is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        var sql = migration.UpOperations.OfType<SqlOperation>().Single().Sql;
        Assert.Equal(7,System.Text.RegularExpressions.Regex.Matches(sql,"CREATE UNIQUE INDEX").Count);
        Assert.Contains("WHERE serial_number IS NOT NULL",sql);
        Assert.Contains("WHERE employee_code IS NOT NULL",sql);
        Assert.Contains("BEFORE UPDATE OR DELETE OR TRUNCATE",sql);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public async Task Setup_and_inspection_fail_closed_without_configuration()
    {
        Assert.Equal("Refused",(await NeonM1Setup.RunAsync(null,"neondb","it_asset_management_m1_verify_fixture")).Status);
        Assert.Equal("NotConfiguredOrInvalid",(await NeonSchemaInspection.CheckAsync(null)).Status);
    }
}
