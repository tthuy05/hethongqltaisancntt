using System.Text.RegularExpressions;
using ItAssetManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItAssetManagement.Infrastructure.Data;

internal static class M1Model
{
    private const string Statuses = "'IN_STOCK','IN_USE','MAINTENANCE','BROKEN','RETIRED'";
    public static void Configure(ModelBuilder model)
    {
        var d = Table<Department>(model, "departments");
        Strings(d, ("Code",50),("Name",200),("Description",1000));
        Check(d,"parent_not_self","parent_department_id <> id");
        Foreign<Department,Department>(d,"ParentDepartmentId");
        Actors(d); Mutable(d);
        d.HasIndex(x => x.ParentDepartmentId).HasDatabaseName("ix_departments_parent");
        d.HasIndex(x => new { x.IsActive, x.Name }).HasDatabaseName("ix_departments_active_name");

        var u = Table<User>(model, "users");
        Strings(u,("Username",100),("NormalizedUsername",100),("Email",320),("NormalizedEmail",320),
            ("PasswordHash",500),("FullName",200),("EmployeeCode",50),("Phone",30));
        Foreign<User,Department>(u,"DepartmentId"); Actors(u); Mutable(u);
        u.Property(x => x.IsAdminLocked).HasDefaultValue(false);
        u.Property(x => x.FailedLoginCount).HasDefaultValue(0);
        u.Property(x => x.TokenVersion).HasDefaultValue(0);
        Check(u,"failed_login_nonnegative","failed_login_count >= 0");
        Check(u,"token_version_nonnegative","token_version >= 0");
        Check(u,"admin_lock_time","(NOT is_admin_locked AND admin_locked_at_utc IS NULL) OR (is_admin_locked AND admin_locked_at_utc IS NOT NULL)");
        u.HasIndex(x => x.NormalizedUsername).IsUnique().HasDatabaseName("uq_users_normalized_username");
        u.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("uq_users_normalized_email");
        u.HasIndex(x => new { x.DepartmentId, x.IsActive }).HasDatabaseName("ix_users_department_active");

        var r = Table<Role>(model,"roles");
        Strings(r,("Code",100),("Name",200),("Description",1000)); Mutable(r);
        r.Property(x => x.IsSystem).HasDefaultValue(false);
        r.HasIndex(x => new { x.IsActive, x.Name }).HasDatabaseName("ix_roles_active_name");

        var ur = Table<UserRole>(model,"user_roles");
        Foreign<UserRole,User>(ur,"UserId"); Foreign<UserRole,Role>(ur,"RoleId"); Foreign<UserRole,User>(ur,"AssignedByUserId");
        ur.Property(x => x.AssignedAtUtc).HasDefaultValueSql("clock_timestamp()");
        ur.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique().HasDatabaseName("uq_user_roles_user_role");
        ur.HasIndex(x => new { x.RoleId, x.UserId }).HasDatabaseName("ix_user_roles_role");

        var p = Table<Permission>(model,"permissions");
        Strings(p,("Code",150),("Name",200),("Module",100),("Description",1000)); Mutable(p);
        p.HasIndex(x => new { x.Module, x.IsActive }).HasDatabaseName("ix_permissions_module_active");

        var rp = Table<RolePermission>(model,"role_permissions");
        Foreign<RolePermission,Role>(rp,"RoleId"); Foreign<RolePermission,Permission>(rp,"PermissionId"); Foreign<RolePermission,User>(rp,"GrantedByUserId");
        rp.Property(x => x.GrantedAtUtc).HasDefaultValueSql("clock_timestamp()");
        rp.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("uq_role_permissions_role_permission");
        rp.HasIndex(x => new { x.PermissionId, x.RoleId }).HasDatabaseName("ix_role_permissions_permission");

        var at = Table<AssetType>(model,"asset_types");
        Strings(at,("Code",50),("Name",200),("Description",1000)); Mutable(at);
        Check(at,"useful_life_positive","default_useful_life_months > 0");
        at.HasIndex(x => new { x.IsActive, x.Name }).HasDatabaseName("ix_asset_types_active_name");

        var a = Table<Asset>(model,"assets");
        Strings(a,("AssetCode",50),("Name",200),("SerialNumber",200),("Manufacturer",200),("Model",200),
            ("Specification",4000),("OperatingSystem",250),("Location",500),("CurrentStatus",30),("Notes",2000));
        Foreign<Asset,AssetType>(a,"AssetTypeId"); Foreign<Asset,Department>(a,"OwningDepartmentId"); Actors(a); Mutable(a, active:false);
        a.Property(x => x.PurchaseCost).HasPrecision(18,2);
        a.Property(x => x.CurrentStatus).HasDefaultValue("IN_STOCK");
        a.Property(x => x.IsArchived).HasDefaultValue(false);
        Check(a,"purchase_cost_nonnegative","purchase_cost >= 0 AND purchase_cost <> 'NaN'::numeric");
        Check(a,"status",$"current_status IN ({Statuses})");
        Check(a,"archive_time","(NOT is_archived AND archived_at_utc IS NULL) OR (is_archived AND archived_at_utc IS NOT NULL)");
        Check(a,"warranty_date","warranty_end_date >= purchase_date");
        a.HasIndex(x => new { x.AssetTypeId, x.CurrentStatus }).IncludeProperties(x => new { x.AssetCode, x.Name }).HasDatabaseName("ix_assets_type_status");
        a.HasIndex(x => new { x.OwningDepartmentId, x.CurrentStatus }).HasDatabaseName("ix_assets_department_status");
        a.HasIndex(x => new { x.IsArchived, x.UpdatedAtUtc }).HasDatabaseName("ix_assets_archived");

        var h = Table<AssetStatusHistory>(model,"asset_status_histories");
        Strings(h,("FromStatus",30),("ToStatus",30),("Reason",1000),("Source",30));
        Foreign<AssetStatusHistory,Asset>(h,"AssetId"); Foreign<AssetStatusHistory,User>(h,"ChangedByUserId");
        h.Property(x => x.ChangedAtUtc).HasDefaultValueSql("clock_timestamp()");
        Check(h,"from_status",$"from_status IN ({Statuses})");
        Check(h,"to_status",$"to_status IN ({Statuses})");
        Check(h,"status_changed","from_status IS NULL OR from_status <> to_status");
        Check(h,"source","source IN ('ASSIGNMENT','MAINTENANCE','ADMIN','IMPORT','SYSTEM')");
        h.HasIndex(x => new { x.AssetId, x.ChangedAtUtc, x.Id }).IsDescending(false,true,true).HasDatabaseName("ix_asset_status_histories_asset_time");
        h.HasIndex(x => x.CorrelationId).HasDatabaseName("ix_asset_status_histories_correlation");

        var log = Table<AuditLog>(model,"audit_logs");
        Strings(log,("ActorType",20),("Action",150),("EntityType",100),("EntityId",100),("Outcome",20),
            ("RequestMethod",10),("RequestPath",1000),("IpAddress",45),("UserAgent",1000),("FailureReasonCode",100));
        Foreign<AuditLog,User>(log,"ActorUserId");
        log.Property(x => x.OccurredAtUtc).HasDefaultValueSql("clock_timestamp()");
        log.Property(x => x.ActorType).HasDefaultValue("USER");
        Check(log,"actor_type","actor_type IN ('USER','SYSTEM','ANONYMOUS')");
        Check(log,"actor_identity","(actor_type = 'USER' AND actor_user_id IS NOT NULL) OR (actor_type IN ('SYSTEM','ANONYMOUS') AND actor_user_id IS NULL)");
        Check(log,"outcome","outcome IN ('SUCCESS','FAILURE','DENIED')");
        foreach (var column in new[] { "OldValuesJson", "NewValuesJson", "MetadataJson" })
        {
            log.Property<string?>(column).HasColumnType("jsonb");
            var sql = Snake(column);
            Check(log,sql+"_shape",$"{sql} IS NULL OR jsonb_typeof({sql}) IN ('object','array')");
        }
        Check(log,"previous_hash_length","previous_entry_hash IS NULL OR octet_length(previous_entry_hash) = 32");
        Check(log,"hash_length","entry_hash IS NULL OR octet_length(entry_hash) = 32");
        log.HasIndex(x => new { x.OccurredAtUtc, x.Id }).IsDescending(true,true).HasDatabaseName("ix_audit_logs_time");
        log.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc }).IsDescending(false,true).HasDatabaseName("ix_audit_logs_actor_time");
        log.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc }).IsDescending(false,false,true).HasDatabaseName("ix_audit_logs_entity");
        log.HasIndex(x => x.CorrelationId).HasDatabaseName("ix_audit_logs_correlation");
        log.HasIndex(x => new { x.Action, x.Outcome, x.OccurredAtUtc }).IsDescending(false,false,true).HasDatabaseName("ix_audit_logs_action_outcome");

        AssignmentMaintenanceMappingProposal.Configure(model);

        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(Snake(property.Name));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(index.GetDatabaseName()!.ToLowerInvariant());
        }
        // lower(...) unique indexes are explicitly migration-owned; EF has no expression-index mapping.
    }

    private static EntityTypeBuilder<T> Table<T>(ModelBuilder model, string table) where T : class
    {
        var builder = model.Entity<T>();
        builder.ToTable(table,"public");
        builder.HasKey("Id").HasName("pk_"+table);
        builder.Property<long>("Id").UseIdentityByDefaultColumn();
        return builder;
    }
    private static void Strings<T>(EntityTypeBuilder<T> builder, params (string Name,int Length)[] columns) where T : class
    {
        foreach (var (name,length) in columns) builder.Property(name).HasMaxLength(length);
    }
    private static void Check<T>(EntityTypeBuilder<T> builder, string name, string sql) where T : class =>
        builder.ToTable(t => t.HasCheckConstraint("ck_"+builder.Metadata.GetTableName()+"_"+name, sql));
    private static void Foreign<T,TTarget>(EntityTypeBuilder<T> builder, string property) where T : class where TTarget : class =>
        builder.HasOne<TTarget>().WithMany().HasForeignKey(property).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_"+builder.Metadata.GetTableName()+"_"+Snake(property));
    private static void Actors<T>(EntityTypeBuilder<T> builder) where T : class
    {
        Foreign<T,User>(builder,"CreatedByUserId"); Foreign<T,User>(builder,"UpdatedByUserId");
    }
    private static void Mutable<T>(EntityTypeBuilder<T> builder, bool active = true) where T : class
    {
        builder.Property<byte[]>("RowVersion").IsRequired().IsConcurrencyToken().ValueGeneratedNever();
        builder.Property<DateTime>("CreatedAtUtc").HasDefaultValueSql("clock_timestamp()");
        if (active) builder.Property<bool>("IsActive").HasDefaultValue(true);
        Check(builder,"row_version_length","octet_length(row_version) = 16");
    }
    private static string Snake(string value) => Regex.Replace(value,"([a-z0-9])([A-Z])","$1_$2").ToLowerInvariant();
}
