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
    public void Offline_factory_has_no_connection_and_full_model_preserves_m1_tables()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        Assert.Null(context.Database.GetConnectionString());
        var originalTables = new InitialM1().TargetModel.GetEntityTypes().Select(e => e.GetTableName());
        Assert.Equal(originalTables.Concat(["asset_assignments", "maintenance_tickets"]).Order(),
            context.Model.GetEntityTypes().Select(e => e.GetTableName()).Order());
        Assert.Equal(12,context.Model.GetEntityTypes().Count());
        Assert.Equal(27,context.Model.GetEntityTypes().Sum(e => e.GetForeignKeys().Count()));
        Assert.All(context.Model.GetEntityTypes(),e =>
        {
            Assert.Equal("public",e.GetSchema());
            Assert.Equal(typeof(long),e.FindPrimaryKey()!.Properties.Single().ClrType);
            Assert.All(e.GetProperties(),p => Assert.Matches("^[a-z][a-z0-9_]*$",p.GetColumnName()));
            Assert.All(e.GetIndexes(),i => Assert.Matches("^[a-z][a-z0-9_]*$",i.GetDatabaseName()));
        });
    }

    [Fact]
    public void Tokens_are_eight_application_managed_columns_not_native_rowversion()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;
        var tokens = model.GetEntityTypes().SelectMany(e => e.GetProperties()).Where(p => p.IsConcurrencyToken).ToArray();
        Assert.Equal(8,tokens.Length);
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
        Assert.Equal(19,migration.UpOperations.OfType<CreateTableOperation>().Sum(t => t.ForeignKeys.Count)
            + migration.UpOperations.OfType<AddForeignKeyOperation>().Count());
        Assert.Equal(6,migration.TargetModel.GetEntityTypes().SelectMany(e => e.GetProperties()).Count(p => p.IsConcurrencyToken));
        Assert.DoesNotContain(migration.UpOperations,o => o is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        var sql = migration.UpOperations.OfType<SqlOperation>().Single().Sql;
        Assert.Equal(7,System.Text.RegularExpressions.Regex.Matches(sql,"CREATE UNIQUE INDEX").Count);
        Assert.Contains("WHERE serial_number IS NOT NULL",sql);
        Assert.Contains("WHERE employee_code IS NOT NULL",sql);
        Assert.Contains("BEFORE UPDATE OR DELETE OR TRUNCATE",sql);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public void Model_difference_from_initial_m1_only_adds_the_two_workflow_tables()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var initialModel = context.GetService<IModelRuntimeInitializer>().Initialize(new InitialM1().TargetModel, designTime: true);
        var currentModel = context.GetService<IDesignTimeModel>().Model;
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(initialModel.GetRelationalModel(), currentModel.GetRelationalModel());
        var tables = differences.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "asset_assignments", "maintenance_tickets" }, tables.Select(t => t.Name).Order());
        Assert.Equal(8,tables.Sum(t => t.ForeignKeys.Count));
        Assert.All(differences, operation => Assert.True(operation switch
        {
            CreateTableOperation table => table.Schema == "public" && table.Name is "asset_assignments" or "maintenance_tickets",
            CreateIndexOperation index => index.Schema == "public" && index.Table is "asset_assignments" or "maintenance_tickets",
            _ => false
        }, $"Unexpected operation against the immutable M1 schema: {operation.GetType().Name}"));
    }

    [Fact]
    public void Workflow_migration_and_snapshot_match_the_full_model_without_pending_changes()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        Assert.Null(context.Database.GetConnectionString());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(new[] { "20261002151601_InitialM1", "20261008080630_AddAssignmentMaintenance" }, context.Database.GetMigrations());
        var target = new AddAssignmentMaintenance().TargetModel;
        Assert.Equal(12,target.GetEntityTypes().Count());
        Assert.Equal(27,target.GetEntityTypes().Sum(entity => entity.GetForeignKeys().Count()));
        Assert.Equal(8,target.GetEntityTypes().SelectMany(entity => entity.GetProperties()).Count(property => property.IsConcurrencyToken));
    }

    [Fact]
    public void Workflow_migration_only_adds_two_tables_eight_foreign_keys_seventeen_checks_and_indexes()
    {
        var migration = new AddAssignmentMaintenance();
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "asset_assignments", "maintenance_tickets" }, tables.Select(table => table.Name).Order());
        Assert.All(tables, table => Assert.Equal("public", table.Schema));
        Assert.Equal(8,tables.Sum(table => table.ForeignKeys.Count));
        Assert.Equal(17,tables.Sum(table => table.CheckConstraints.Count));
        Assert.All(tables.SelectMany(table => table.ForeignKeys), fk => Assert.Equal(ReferentialAction.NoAction, fk.OnDelete));
        Assert.Equal(11,migration.UpOperations.OfType<CreateIndexOperation>().Count());
        const string expressionIndex = "CREATE UNIQUE INDEX uq_maintenance_tickets_code ON public.maintenance_tickets (lower(ticket_code));";
        Assert.Equal(expressionIndex,Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql);
        Assert.Equal(14,migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation => Assert.True(operation switch
        {
            CreateTableOperation table => table.Schema == "public" && table.Name is "asset_assignments" or "maintenance_tickets",
            CreateIndexOperation index => index.Schema == "public" && index.Table is "asset_assignments" or "maintenance_tickets",
            SqlOperation sql => sql.Sql == expressionIndex,
            _ => false
        }, $"Unexpected workflow migration operation: {operation.GetType().Name}"));
        Assert.All(tables.SelectMany(table => table.Columns), column => Assert.Matches("^[a-z][a-z0-9_]*$", column.Name));
        Assert.All(tables.SelectMany(table => table.CheckConstraints), check => Assert.Matches("^[a-z][a-z0-9_]*$", check.Name));
        Assert.All(tables.SelectMany(table => table.ForeignKeys), fk => Assert.Matches("^[a-z][a-z0-9_]*$", fk.Name));
    }

    [Fact]
    public void Workflow_sql_generation_is_offline_and_preserves_existing_database_objects()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var sql = context.GetService<IMigrator>().GenerateScript("20261002151601_InitialM1", "20261008080630_AddAssignmentMaintenance");
        Assert.Contains("CREATE TABLE public.asset_assignments",sql);
        Assert.Contains("CREATE TABLE public.maintenance_tickets",sql);
        Assert.Contains("CREATE UNIQUE INDEX uq_maintenance_tickets_code ON public.maintenance_tickets (lower(ticket_code));",sql);
        foreach (var forbidden in new[] { "DROP TABLE", "DROP COLUMN", "DROP INDEX", "ALTER TABLE public.assets", "ALTER TABLE public.users", "ALTER TABLE public.departments", "DELETE FROM" })
            Assert.DoesNotContain(forbidden,sql);
        Assert.Null(context.Database.GetConnectionString());
    }

    [Fact]
    public async Task Setup_and_inspection_fail_closed_without_configuration()
    {
        Assert.Equal("Refused",(await NeonM1Setup.RunAsync(null,"neondb","it_asset_management_m1_verify_fixture")).Status);
        Assert.Equal("NotConfiguredOrInvalid",(await NeonSchemaInspection.CheckAsync(null)).Status);
    }
}
