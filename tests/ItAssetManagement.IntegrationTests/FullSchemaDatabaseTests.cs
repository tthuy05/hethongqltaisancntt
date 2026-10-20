using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ItAssetManagement.Domain.Entities;
using ItAssetManagement.Infrastructure.Data;
using ItAssetManagement.Infrastructure.Data.Migrations;
using ItAssetManagement.Infrastructure.Mvp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Abstractions;

namespace ItAssetManagement.IntegrationTests;

public sealed class FullSchemaFactAttribute : FactAttribute
{
    public FullSchemaFactAttribute()
    { if (Environment.GetEnvironmentVariable("ITAM_FULL_SCHEMA_TESTS") != "1") Skip="Explicit fresh isolated database opt-in required."; }
}

// This test creates exactly one NEW database. It never applies migrations/seed to neondb,
// reuses an old test database, or deletes a database on success/failure.
public sealed class FullSchemaDatabaseTests(ITestOutputHelper output)
{
    private static readonly string[] OldTables = NeonM1Setup.Tables.Concat(NeonAssignmentMaintenanceSetup.NewTables).Order().ToArray();
    private readonly List<string> _checks=[];
    private readonly Dictionary<string,object?> _proof = new();
    private const string Token="decode(repeat('01',16),'hex')";
    private const string Missing="9223372036854775807";

    [FullSchemaFact]
    public async Task Prepared_SQL_script_upgrades_a_second_fresh_database_atomically_and_fails_closed_on_reapply()
    {
        var target=Environment.GetEnvironmentVariable("ITAM_SCRIPT_TEST_DATABASE");
        Assert.Matches("\\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\\z",target ?? "");
        using var config=JsonDocument.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("ITAM_TEST_CONFIG_PATH")!));
        Assert.True(NeonConnectionPolicy.TryCreate(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString(),out var settings));
        Assert.Equal("neondb",settings!.Database);
        settings.Host=settings.Host!.Replace("-pooler.",".",StringComparison.OrdinalIgnoreCase); settings.Pooling=false;
        await using (var shared=new NpgsqlConnection(settings.ConnectionString))
        {
            await shared.OpenAsync();
            Assert.Equal(0L,await Scalar(shared,null,"SELECT count(*) FROM pg_database WHERE datname=@target",new NpgsqlParameter("target",target)));
            await Execute(shared,null,$"CREATE DATABASE \"{target}\" TEMPLATE template0");
        }
        settings.Database=target;
        await using var db=Context(settings.ConnectionString);
        await db.GetService<IMigrator>().MigrateAsync(NeonAssignmentMaintenanceSetup.Migration);
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var script=await File.ReadAllTextAsync(Path.Combine(root,"scripts/full-schema/CompleteBaselineV1.sql"));
        await using var c=new NpgsqlConnection(settings.ConnectionString); await c.OpenAsync();
        var watch=Stopwatch.StartNew(); await Execute(c,null,script); watch.Stop();
        Assert.Equal(18L,await Scalar(c,null,"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history'"));
        Assert.Equal(41L,await Scalar(c,null,"SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f'"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var error=await Assert.ThrowsAsync<PostgresException>(() => Execute(c,null,script));
        Assert.Equal("P0001",error.SqlState); Assert.Equal("EXPECTED_EXACT_12_TABLE_BASELINE",error.MessageText);
        await Execute(c,null,"ROLLBACK"); // The guarded script refused before DDL; no Down/Drop.
        Assert.Equal(3L,await Scalar(c,null,"SELECT count(*) FROM public.ef_migrations_history"));
        var proof=new { status="PASS",target,scriptSeconds=watch.Elapsed.TotalSeconds,checks=new[] { "FRESH_12_TABLE_BASELINE","GUARDED_SQL_18_TABLES_41_FK_HISTORY_MATCH","REAPPLY_REFUSED_WITHOUT_CHANGE" } };
        var path=Path.Combine(root,"test-results/full-schema/script-proof.json"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(proof,new JsonSerializerOptions { WriteIndented=true }));
        output.WriteLine("SQL script PASS; evidence: "+path);
    }

    [FullSchemaFact]
    public async Task Fresh_12_table_database_upgrades_to_18_preserving_baseline_and_enforcing_constraints()
    {
        var target=Environment.GetEnvironmentVariable("ITAM_TEST_DATABASE");
        Assert.Matches("\\Ait_asset_management_full_schema_verify_[a-z0-9_]{1,24}\\z",target ?? "");
        var configPath=Environment.GetEnvironmentVariable("ITAM_TEST_CONFIG_PATH") ?? throw new InvalidOperationException("Private config path required; no secret in arguments.");
        using var config=JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        Assert.True(NeonConnectionPolicy.TryCreate(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString(),out var settings),"Private configuration unavailable.");
        Assert.Equal("neondb",settings!.Database);
        settings.Host=settings.Host!.Replace("-pooler.",".",StringComparison.OrdinalIgnoreCase);
        settings.Pooling=false; settings.IncludeErrorDetail=false; settings.ApplicationName="ItAssetManagement.FullSchema.Isolated";
        _proof["startedUtc"]=DateTime.UtcNow; _proof["target"]=target;
        try
        {
            // Shared baseline probe is repeatable-read READ ONLY, including permissions/counts/history.
            await using (var shared=new NpgsqlConnection(settings.ConnectionString))
            {
                await shared.OpenAsync(); await using var read=await shared.BeginTransactionAsync(IsolationLevel.RepeatableRead);
                await Execute(shared,read,"SET TRANSACTION READ ONLY");
                Assert.Equal("on",await Scalar(shared,read,"SHOW transaction_read_only"));
                _proof["sharedBaseline"]=await Catalog(shared,read);
                Assert.Equal(12L,await Scalar(shared,read,"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history'"));
                Assert.Equal(27L,await Scalar(shared,read,"SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f'"));
                Assert.Equal(2L,await Scalar(shared,read,"SELECT count(*) FROM public.ef_migrations_history"));
                Assert.Equal(NeonAssignmentMaintenanceSetup.Migration,await Scalar(shared,read,"SELECT max(\"MigrationId\") FROM public.ef_migrations_history"));
                Assert.Equal(0L,await Scalar(shared,read,"SELECT count(*) FROM pg_database WHERE datname=@target",new NpgsqlParameter("target",target)));
                Assert.Equal(true,await Scalar(shared,read,"SELECT rolcreatedb FROM pg_roles WHERE rolname=current_user"));
                await read.RollbackAsync();
                // New test database is authorized, not shared schema DDL. Name guard above prevents reuse.
                await Execute(shared,null,$"CREATE DATABASE \"{target}\" TEMPLATE template0");
                Pass("SHARED_READ_ONLY_12_TABLES_27_FK_HISTORY_B_AND_FRESH_TARGET");
            }
            settings.Database=target;
            await using var db=Context(settings.ConnectionString);
            await db.GetService<IMigrator>().MigrateAsync(NeonAssignmentMaintenanceSetup.Migration);
            Assert.Equal(12,(await db.Database.GetAppliedMigrationsAsync()).Count() == 2 ? db.GetService<IMigrationsAssembly>().CreateMigration(db.GetService<IMigrationsAssembly>().Migrations[NeonAssignmentMaintenanceSetup.Migration],db.Database.ProviderName!).TargetModel.GetEntityTypes().Count() : 0);
            await using var connection=new NpgsqlConnection(settings.ConnectionString); await connection.OpenAsync();
            // Synthetic fixture populates every one of the twelve old tables; no production data copied.
            await Execute(connection,null,$"""
                INSERT INTO public.departments(id,code,name,row_version) VALUES(1,'BASE','Baseline',{Token});
                INSERT INTO public.users(id,username,normalized_username,email,normalized_email,password_hash,full_name,department_id,row_version)
                  VALUES(1,'base','BASE','base@fixture.invalid','BASE@FIXTURE.INVALID','NOT_A_USABLE_HASH','Baseline',1,{Token});
                INSERT INTO public.roles(id,code,name,row_version) VALUES(1,'BASE','Baseline',{Token});
                INSERT INTO public.user_roles(user_id,role_id,assigned_by_user_id) VALUES(1,1,1);
                INSERT INTO public.permissions(id,code,name,module,row_version) VALUES(1,'baseline.read','Baseline','baseline',{Token});
                INSERT INTO public.role_permissions(role_id,permission_id,granted_by_user_id) VALUES(1,1,1);
                INSERT INTO public.asset_types(id,code,name,row_version) VALUES(1,'BASE','Baseline',{Token});
                INSERT INTO public.assets(id,asset_code,name,asset_type_id,owning_department_id,purchase_cost,row_version) VALUES(1,'BASE','Baseline',1,1,123456789.12,{Token});
                INSERT INTO public.asset_status_histories(asset_id,to_status,source,changed_by_user_id,correlation_id) VALUES(1,'IN_STOCK','SYSTEM',1,'11111111-1111-1111-1111-111111111111');
                INSERT INTO public.audit_logs(action,outcome,actor_type,correlation_id) VALUES('test.baseline','SUCCESS','SYSTEM','11111111-1111-1111-1111-111111111111');
                INSERT INTO public.asset_assignments(asset_id,assigned_user_id,assigned_by_user_id,returned_at_utc,row_version) VALUES(1,1,1,clock_timestamp()+interval '1 day',{Token});
                INSERT INTO public.maintenance_tickets(ticket_code,asset_id,title,description,requested_by_user_id,row_version) VALUES('BASE',1,'Baseline','Baseline',1,{Token});
                """);
            // Explicit fixture IDs must advance their sequences before other fixture/API tests use them.
            foreach (var table in new[] { "departments","users","roles","permissions","asset_types","assets" })
                await Execute(connection,null,$"SELECT setval(pg_get_serial_sequence('public.{table}','id'),(SELECT max(id) FROM public.{table}))");
            var before=await Fingerprint(connection); _proof["before12"]=before;
            // SET must stay on the SAME physical EF connection through migration execution.
            await db.Database.OpenConnectionAsync();
            await LockFailureDrill(settings.ConnectionString,db);
            Assert.Equal(before,await Fingerprint(connection));
            var watch=Stopwatch.StartNew();
            await db.Database.ExecuteSqlRawAsync("SET lock_timeout='3s'; SET statement_timeout='60s'");
            await db.GetService<IMigrator>().MigrateAsync(FullSchemaBaseline.Migration);
            watch.Stop(); _proof["migrationSeconds"]=watch.Elapsed.TotalSeconds;
            var after=await Fingerprint(connection); Assert.Equal(before,after);
            _proof["after18Legacy"]=after; Pass("ALL_12_TABLE_ROW_SCHEMA_SEQUENCE_FINGERPRINTS_EXACT");
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(new[] { NeonAssignmentMaintenanceSetup.InitialMigration,NeonAssignmentMaintenanceSetup.Migration,FullSchemaBaseline.Migration },await db.Database.GetAppliedMigrationsAsync());
            await db.Database.MigrateAsync(); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(after,await Fingerprint(connection)); Pass("MIGRATION_REAPPLY_ZERO_AND_MODEL_HISTORY_MATCH");
            await VerifyCatalog(connection,db); _proof["isolatedCatalog"]=await Catalog(connection,null);
            await ConstraintDrill(connection,db);
            // Rollback-based negative tests can consume identity sequences; row preservation is checked separately.
            await TokenConcurrency(settings.ConnectionString);
            var inspection=await NeonSchemaInspection.CheckAsync(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString(),target);
            Assert.Equal("Verified",inspection.Status); Assert.Equal(18,inspection.PrimaryKeys); Assert.Equal(41,inspection.ForeignKeys);
            Pass("INSPECTION_RECOGNIZES_18_AND_WORKFLOW_READ_QUERY");
            _proof["status"]="PASS";
        }
        catch (Exception error)
        {
            _proof["status"]="FAIL"; _proof["failureType"]=error.GetType().Name;
            if (error is PostgresException pg) _proof["sqlState"]=pg.SqlState;
            throw;
        }
        finally
        {
            _proof["finishedUtc"]=DateTime.UtcNow; _proof["checks"]=_checks;
            var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
            var evidence=Path.Combine(root,"test-results","full-schema","database-proof.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
            await File.WriteAllTextAsync(evidence,JsonSerializer.Serialize(_proof,new JsonSerializerOptions { WriteIndented=true }));
            output.WriteLine("Sanitized evidence: "+evidence); output.WriteLine("Checks passed: "+_checks.Count);
        }
    }

    private async Task LockFailureDrill(string secret,AppDbContext db)
    {
        await using var blocker=new NpgsqlConnection(secret); await blocker.OpenAsync();
        await using var tx=await blocker.BeginTransactionAsync();
        await Execute(blocker,tx,"LOCK TABLE public.users IN ACCESS EXCLUSIVE MODE");
        await db.Database.ExecuteSqlRawAsync("SET lock_timeout='1s'; SET statement_timeout='10s'");
        var error=await Record.ExceptionAsync(() => db.GetService<IMigrator>().MigrateAsync(FullSchemaBaseline.Migration));
        Assert.NotNull(error);
        // Npgsql's non-retrying execution strategy wraps transient SQLSTATE 55P03.
        Assert.Equal("55P03",Assert.IsType<PostgresException>(error.GetBaseException()).SqlState); await tx.RollbackAsync();
        Assert.Equal(2,(await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(12L,await Scalar(blocker,null,"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history'"));
        Pass("BOUNDED_LOCK_TIMEOUT_ROLLS_BACK_DDL_AND_HISTORY");
    }

    private async Task VerifyCatalog(NpgsqlConnection c,AppDbContext db)
    {
        Assert.Equal(18L,await Scalar(c,null,"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history'"));
        Assert.Equal(41L,await Scalar(c,null,"SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f' AND confdeltype='a'"));
        Assert.Equal(0L,await Scalar(c,null,"SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND NOT convalidated"));
        var model=db.GetService<IDesignTimeModel>().Model;
        foreach (var e in model.GetEntityTypes())
        {
            var table=e.GetTableName()!;
            Assert.Equal(1L,await Scalar(c,null,"SELECT count(*) FROM pg_constraint WHERE conrelid=@table::regclass AND contype='p'",new NpgsqlParameter("table","public."+table)));
            foreach (var fk in e.GetForeignKeys())
            {
                var name=fk.GetConstraintName()!;
                var column=Assert.Single(fk.Properties).GetColumnName()!;
                var expected=$"FOREIGN KEY ({column}) REFERENCES {fk.PrincipalEntityType.GetTableName()}(id)";
                var definition=(string)(await Scalar(c,null,"SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid=@table::regclass AND conname=@name",new("table","public."+table),new("name",name)))!;
                Assert.Equal(expected,definition);
            }
            foreach (var check in e.GetCheckConstraints())
                Assert.Equal(1L,await Scalar(c,null,"SELECT count(*) FROM pg_constraint WHERE conrelid=@table::regclass AND conname=@name AND contype='c'",new("table","public."+table),new("name",check.Name)));
            foreach (var index in e.GetIndexes())
                Assert.Equal(1L,await Scalar(c,null,"SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename=@table AND indexname=@name",new("table",table),new("name",index.GetDatabaseName())));
            foreach (var p in e.GetProperties())
            {
                Assert.Equal(p.IsNullable ? "YES":"NO",await Scalar(c,null,"SELECT is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name=@table AND column_name=@column",new("table",table),new("column",p.GetColumnName())));
                Assert.Equal(p.GetColumnType(),await Scalar(c,null,"SELECT format_type(a.atttypid,a.atttypmod) FROM pg_attribute a WHERE a.attrelid=@table::regclass AND a.attname=@column",new("table","public."+table),new("column",p.GetColumnName())));
            }
        }
        foreach (var name in new[] { "uq_softwares_code","uq_software_licenses_code","uq_replacement_rules_code_version","uq_replacement_rules_one_current","maintenance_histories_append_only" })
            Assert.Equal(1L,await Scalar(c,null,name.EndsWith("append_only",StringComparison.Ordinal)
                ? "SELECT count(*) FROM pg_trigger WHERE tgname=@name AND NOT tgisinternal"
                : "SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND indexname=@name",new NpgsqlParameter("name",name)));
        _proof["relationships"]=model.GetEntityTypes().OrderBy(e => e.GetTableName()).SelectMany(e => e.GetForeignKeys().OrderBy(f => f.GetConstraintName()).Select(f => new { table=e.GetTableName(),column=Assert.Single(f.Properties).GetColumnName(),parent=f.PrincipalEntityType.GetTableName(),required=f.IsRequired,name=f.GetConstraintName() })).ToArray();
        Pass("ALL_18_PK_41_FK_EXACT_ENDPOINTS_COLUMNS_TYPES_NULLABILITY_CHECK_INDEX_NAMES");
    }

    private async Task ConstraintDrill(NpgsqlConnection c,AppDbContext db)
    {
        var rowsBefore=await RowsFingerprint(c);
        await using var tx=await c.BeginTransactionAsync();
        await Execute(c,tx,$"""
            INSERT INTO public.softwares(id,code,name,row_version) VALUES(1,'SOFT','Software',{Token});
            INSERT INTO public.software_licenses(id,software_id,license_code,license_type,total_quantity,purchase_cost,row_version) VALUES(1,1,'LIC','PER_USER',1,123456789.12,{Token});
            INSERT INTO public.license_assignments(id,software_license_id,assigned_user_id,assigned_by_user_id,row_version) VALUES(1,1,1,1,{Token});
            INSERT INTO public.replacement_rules(id,code,name,minimum_age_months,asset_type_id,estimated_unit_cost,row_version) VALUES(1,'RULE','Rule',36,1,999999.99,{Token});
            INSERT INTO public.replacement_recommendations(id,asset_id,replacement_rule_id,priority,reason,evaluation_snapshot_json,row_version) VALUES(1,1,1,'HIGH','Reason','[]',{Token});
            INSERT INTO public.maintenance_histories(id,maintenance_ticket_id,event_type,performed_by_user_id,from_assigned_to_user_id,to_assigned_to_user_id,correlation_id) VALUES(1,1,'CREATED',1,1,1,'11111111-1111-1111-1111-111111111111');
            """);
        foreach (var table in FullSchemaBaseline.NewTables)
            await Execute(c,tx,$"SELECT setval(pg_get_serial_sequence('public.{table}','id'),1,true)");
        Pass("SIX_VALID_FIXTURES_DEFAULTS_AND_DECIMAL_PRECISION");
        async Task Reject(string name,string sql,string state="23514",string? constraint=null)
        {
            await tx.SaveAsync("negative_case");
            var error=await Assert.ThrowsAsync<PostgresException>(() => Execute(c,tx,sql));
            Assert.Equal(state,error.SqlState); if (constraint is not null) Assert.Equal(constraint,error.ConstraintName);
            await tx.RollbackAsync("negative_case"); await tx.ReleaseAsync("negative_case"); Pass(name);
        }
        var changes=new Dictionary<string,string[]>
        {
            ["softwares"]=["row_version=decode('01','hex')"],
            ["software_licenses"]=["row_version=decode('01','hex')","license_type='INVALID'","total_quantity=0","license_key_last4='12',license_key_ciphertext=decode('01','hex'),key_version='v1'","license_key_last4='ABCD'","expires_at_utc=clock_timestamp(),starts_at_utc=clock_timestamp()+interval '1 day'","purchase_cost=-1","purchase_cost='NaN'::numeric"],
            ["license_assignments"]=["row_version=decode('01','hex')","assigned_user_id=NULL","assigned_asset_id=1","revoked_by_user_id=1","revoked_at_utc=assigned_at_utc-interval '1 second'","is_archived=true"],
            ["replacement_rules"]=["row_version=decode('01','hex')","version=0","priority=-1","minimum_age_months=0","minimum_maintenance_count=0","minimum_failure_count=0","maximum_maintenance_cost_ratio=1.0001","maximum_maintenance_cost_ratio='NaN'::numeric","estimated_unit_cost=-1","estimated_unit_cost='NaN'::numeric","effective_to_utc=effective_from_utc","minimum_age_months=NULL"],
            ["replacement_recommendations"]=["row_version=decode('01','hex')","disposition='INVALID'","priority='INVALID'","score=-1","score='NaN'::numeric","estimated_replacement_cost=-1","estimated_replacement_cost='NaN'::numeric","planned_replacement_year=1999","planned_replacement_year=2101","evaluation_snapshot_json='null'::jsonb","evaluation_snapshot_json='1'::jsonb","disposition_at_utc=recommended_at_utc-interval '1 second'","disposition_by_user_id=1","disposition='PLANNED'","disposition='DISMISSED',is_current=false","disposition='SUPERSEDED',is_current=false","disposition='SUPERSEDED',disposition_at_utc=recommended_at_utc","is_archived=true"]
        };
        foreach (var (table,updates) in changes)
            for (var i=0;i<updates.Length;i++) await Reject(table+"_CHECK_"+(i+1),$"UPDATE public.{table} SET {updates[i]} WHERE id=1");
        foreach (var table in FullSchemaBaseline.NewTables.Where(t => t != "maintenance_histories"))
        {
            var entity=db.GetService<IDesignTimeModel>().Model.GetEntityTypes().Single(e => e.GetTableName()==table);
            foreach (var fk in entity.GetForeignKeys())
            {
                var col=Assert.Single(fk.Properties).GetColumnName()!;
                var extra=col=="assigned_asset_id" ? ",assigned_user_id=NULL" : col=="revoked_by_user_id" ? ",revoked_at_utc=assigned_at_utc" : col=="disposition_by_user_id" ? ",disposition='PLANNED',disposition_at_utc=recommended_at_utc" : "";
                await Reject(fk.GetConstraintName()!,$"UPDATE public.{table} SET {col}={Missing}{extra} WHERE id=1","23503",fk.GetConstraintName());
            }
            foreach (var property in entity.GetProperties().Where(p => !p.IsNullable && p.Name is not ("Id" or "RowVersion")))
                await Reject(table+"_NOT_NULL_"+property.GetColumnName(),$"UPDATE public.{table} SET {property.GetColumnName()}=NULL WHERE id=1","23502");
            foreach (var property in entity.GetProperties().Where(p => p.GetMaxLength() is not null))
                await Reject(table+"_LENGTH_"+property.GetColumnName(),$"UPDATE public.{table} SET {property.GetColumnName()}=repeat('X',{property.GetMaxLength()+1}) WHERE id=1","22001");
        }
        const string correlation="'11111111-1111-1111-1111-111111111111'";
        foreach (var col in new[] { "maintenance_ticket_id","performed_by_user_id","from_assigned_to_user_id","to_assigned_to_user_id" })
        {
            var values=new Dictionary<string,string> { ["maintenance_ticket_id"]="1",["performed_by_user_id"]="1",["from_assigned_to_user_id"]="1",["to_assigned_to_user_id"]="1" }; values[col]=Missing;
            await Reject("history_FK_"+col,$"INSERT INTO public.maintenance_histories(maintenance_ticket_id,performed_by_user_id,from_assigned_to_user_id,to_assigned_to_user_id,event_type,correlation_id) VALUES({string.Join(',',values.Values)},'CREATED',{correlation})","23503","fk_maintenance_histories_"+col);
        }
        foreach (var (column,value,state) in new[] { ("event_type","'INVALID'","23514"),("from_status","'INVALID'","23514"),("to_status","'INVALID'","23514"),("cost","-1","23514"),("cost","'NaN'::numeric","23514"),("comment","repeat('X',4001)","22001"),("correlation_id","NULL","23502"),("performed_at_utc","NULL","23502") })
        {
            var cols="maintenance_ticket_id,event_type,correlation_id"; var vals=$"1,'CREATED',{correlation}";
            if (column=="event_type") vals=$"1,{value},{correlation}";
            else if (column=="correlation_id") vals="1,'CREATED',NULL";
            else { cols+=","+column; vals+=","+value; }
            await Reject("history_"+column+"_"+state,$"INSERT INTO public.maintenance_histories({cols}) VALUES({vals})",state);
        }
        foreach (var operation in new[] { "UPDATE public.maintenance_histories SET comment='rewrite' WHERE id=1","DELETE FROM public.maintenance_histories WHERE id=1","TRUNCATE public.maintenance_histories" }) await Reject("history_APPEND_ONLY",operation,"55000");
        await Reject("software_CODE_CASE_UNIQUE",$"INSERT INTO public.softwares(code,name,row_version) VALUES('soft','Test',{Token})","23505","uq_softwares_code");
        await Reject("license_CODE_CASE_UNIQUE",$"INSERT INTO public.software_licenses(software_id,license_code,license_type,total_quantity,row_version) VALUES(1,'lic','OTHER',1,{Token})","23505","uq_software_licenses_code");
        await Reject("rule_CODE_VERSION_UNIQUE",$"INSERT INTO public.replacement_rules(code,version,name,minimum_age_months,is_active,row_version) VALUES('rule',1,'Rule',1,false,{Token})","23505","uq_replacement_rules_code_version");
        await Reject("rule_ONE_CURRENT",$"INSERT INTO public.replacement_rules(code,version,name,minimum_age_months,row_version) VALUES('rule',2,'Rule',1,{Token})","23505","uq_replacement_rules_one_current");
        await Reject("license_ACTIVE_USER_UNIQUE",$"INSERT INTO public.license_assignments(software_license_id,assigned_user_id,assigned_by_user_id,row_version) VALUES(1,1,1,{Token})","23505","uq_license_assignments_active_user");
        await Execute(c,tx,$"INSERT INTO public.license_assignments(software_license_id,assigned_asset_id,assigned_by_user_id,row_version) VALUES(1,1,1,{Token})");
        await Reject("license_ACTIVE_ASSET_UNIQUE",$"INSERT INTO public.license_assignments(software_license_id,assigned_asset_id,assigned_by_user_id,row_version) VALUES(1,1,1,{Token})","23505","uq_license_assignments_active_asset");
        await Reject("recommendation_ONE_CURRENT",$"INSERT INTO public.replacement_recommendations(asset_id,replacement_rule_id,priority,reason,evaluation_snapshot_json,row_version) VALUES(1,1,'HIGH','R','{{}}',{Token})","23505","uq_replacement_recommendations_current_asset");
        foreach (var table in new[] { "softwares","software_licenses","replacement_rules","maintenance_tickets","assets","users" })
            await Reject(table+"_PARENT_NO_CASCADE",$"DELETE FROM public.{table} WHERE id=1","23503");
        await Execute(c,tx,"UPDATE public.license_assignments SET revoked_at_utc=assigned_at_utc WHERE id=1");
        await Execute(c,tx,$"INSERT INTO public.license_assignments(software_license_id,assigned_user_id,assigned_by_user_id,row_version) VALUES(1,1,1,{Token})");
        await Execute(c,tx,"UPDATE public.replacement_recommendations SET disposition='SUPERSEDED',disposition_at_utc=recommended_at_utc,is_current=false WHERE id=1");
        await Execute(c,tx,$"INSERT INTO public.replacement_recommendations(asset_id,replacement_rule_id,priority,reason,evaluation_snapshot_json,row_version) VALUES(1,1,'HIGH','R','[]',{Token})");
        await Execute(c,tx,"UPDATE public.software_licenses SET license_key_ciphertext=decode('01','hex'),license_key_last4='ABCD',key_version='test-fixture-only'");
        Pass("VALID_HISTORY_REUSE_ARRAY_JSON_AND_COMPLETE_KEY_TUPLE");
        await tx.RollbackAsync(); Assert.Equal(rowsBefore,await RowsFingerprint(c)); Pass("ROLLBACK_PROBES_PRESERVE_ALL_LEGACY_ROWS");
    }

    private async Task TokenConcurrency(string secret)
    {
        await using var c=new NpgsqlConnection(secret); await c.OpenAsync();
        await Execute(c,null,$"INSERT INTO public.softwares(code,name,row_version) VALUES('TOKEN','Token test',{Token})");
        await using var one=Context(secret); await using var two=Context(secret);
        var a=await one.Set<Software>().SingleAsync(x => x.Code=="TOKEN"); var b=await two.Set<Software>().SingleAsync(x => x.Code=="TOKEN");
        a.Name="first winner"; b.Name="stale loser";
        Begin(one); Begin(two);
        await one.SaveChangesAsync(); Assert.Equal(16,a.RowVersion.Length); Assert.NotNull(a.UpdatedAtUtc); Assert.NotEqual(b.RowVersion,a.RowVersion);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => two.SaveChangesAsync());
        Assert.Equal("first winner",await Scalar(c,null,"SELECT name FROM public.softwares WHERE code='TOKEN'"));
        Pass("EF_REAL_POSTGRES_ROW_VERSION_STALE_WRITER_REJECTED");
    }

    private static void Begin(AppDbContext db) => typeof(AppDbContext).GetMethod("BeginAuditedWrite",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(db,null);
    private static AppDbContext Context(string secret) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(secret,p => p.MigrationsHistoryTable("ef_migrations_history","public")).Options);
    private void Pass(string name) { _checks.Add(name); }
    private static async Task<object?> Scalar(NpgsqlConnection c,NpgsqlTransaction? t,string sql,params NpgsqlParameter[] parameters)
    { await using var cmd=new NpgsqlCommand(sql,c,t); cmd.Parameters.AddRange(parameters); return await cmd.ExecuteScalarAsync(); }
    private static async Task Execute(NpgsqlConnection c,NpgsqlTransaction? t,string sql)
    { await using var cmd=new NpgsqlCommand(sql,c,t); await cmd.ExecuteNonQueryAsync(); }
    private static async Task<string> RowsFingerprint(NpgsqlConnection c)
    {
        var parts=new List<string>();
        foreach (var table in OldTables) parts.Add(table+":"+await Scalar(c,null,$"SELECT count(*)::text || ':' || md5(COALESCE(string_agg(to_jsonb(t)::text,E'\\n' ORDER BY id),'')) FROM public.{table} t"));
        return string.Join("\n",parts);
    }
    private static async Task<string> Fingerprint(NpgsqlConnection c)
    {
        var rows=await RowsFingerprint(c); var names=string.Join(',',OldTables.Select(t => "'"+t+"'"));
        var meta=await Scalar(c,null,$"""
            SELECT md5(string_agg(item,E'\n' ORDER BY item)) FROM (
              SELECT 'C:'||c.relname||':'||a.attnum||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull||':'||COALESCE(pg_get_expr(d.adbin,d.adrelid),'') AS item
                FROM pg_class c JOIN pg_attribute a ON a.attrelid=c.oid LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
                WHERE c.relnamespace='public'::regnamespace AND c.relname IN ({names}) AND a.attnum>0 AND NOT a.attisdropped
              UNION ALL SELECT 'K:'||c.relname||':'||k.conname||':'||pg_get_constraintdef(k.oid) FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid WHERE c.relnamespace='public'::regnamespace AND c.relname IN ({names})
              UNION ALL SELECT 'I:'||tablename||':'||indexname||':'||indexdef FROM pg_indexes WHERE schemaname='public' AND tablename IN ({names})
              UNION ALL SELECT 'T:'||c.relname||':'||pg_get_triggerdef(t.oid) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid WHERE c.relnamespace='public'::regnamespace AND c.relname IN ({names}) AND NOT t.tgisinternal
            ) metadata
            """);
        var sequences=new List<string>();
        foreach (var table in OldTables) sequences.Add(table+":"+await Scalar(c,null,$"SELECT last_value::text || ':' || is_called::text FROM public.{table}_id_seq"));
        return rows+"\nmetadata:"+meta+"\n"+string.Join("\n",sequences);
    }
    private static async Task<Dictionary<string,object?>> Catalog(NpgsqlConnection c,NpgsqlTransaction? t)
    {
        var counts=new Dictionary<string,object?>();
        foreach (var (name,sql) in new[] {
            ("tables","SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'ef_migrations_history'"),
            ("foreignKeys","SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='f'"),
            ("checks","SELECT count(*) FROM pg_constraint WHERE connamespace='public'::regnamespace AND contype='c'"),
            ("indexes","SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename<>'ef_migrations_history'"),
            ("permissions","SELECT count(*) FROM public.permissions"),("rolePermissions","SELECT count(*) FROM public.role_permissions"),
            ("version","SHOW server_version"),("migration","SELECT max(\"MigrationId\") FROM public.ef_migrations_history") }) counts[name]=await Scalar(c,t,sql);
        return counts;
    }
}
