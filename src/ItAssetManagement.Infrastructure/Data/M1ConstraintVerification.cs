using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

internal static class M1ConstraintVerification
{
    public static async Task RunAsync(NpgsqlConnection connection, List<string> checks)
    {
        if (!connection.Database.StartsWith("it_asset_management_m1_verify_", StringComparison.Ordinal))
            throw new InvalidOperationException("ISOLATION_REQUIRED");
        await using var transaction = await connection.BeginTransactionAsync();
        // Fixtures never touch the shared database and are always rolled back. No seed accounts.
        const string token = "decode(repeat('01',16),'hex')";
        async Task Execute(string sql)
        {
            await using var command = new NpgsqlCommand(sql,connection,transaction);
            await command.ExecuteNonQueryAsync();
        }
        await Execute($"""
            INSERT INTO public.departments(id,code,name,row_version) VALUES(10001,'DEPT','Phòng CNTT',{token});
            INSERT INTO public.asset_types(id,code,name,row_version) VALUES(10001,'LAPTOP','Máy tính',{token});
            INSERT INTO public.users(id,username,normalized_username,email,normalized_email,password_hash,full_name,employee_code,row_version)
              VALUES(10001,'fixture','FIXTURE','fixture@example.invalid','FIXTURE@EXAMPLE.INVALID','NOT_A_USABLE_PASSWORD_HASH','Fixture','EMP',{token});
            INSERT INTO public.roles(id,code,name,row_version) VALUES(10001,'TEST','Fixture',{token});
            INSERT INTO public.permissions(id,code,name,module,row_version) VALUES(10001,'test.read','Fixture','test',{token});
            INSERT INTO public.user_roles(user_id,role_id) VALUES(10001,10001);
            INSERT INTO public.role_permissions(role_id,permission_id) VALUES(10001,10001);
            INSERT INTO public.assets(id,asset_code,serial_number,name,asset_type_id,owning_department_id,purchase_cost,row_version)
              VALUES(10001,'ASSET','SERIAL','Thiết bị',10001,10001,123456789.12,{token});
            INSERT INTO public.asset_status_histories(asset_id,to_status,source,correlation_id)
              VALUES(10001,'IN_STOCK','SYSTEM','00000000-0000-0000-0000-000000000001');
            INSERT INTO public.audit_logs(actor_type,action,outcome,correlation_id,metadata_json)
              VALUES('SYSTEM','test','SUCCESS','00000000-0000-0000-0000-000000000001',jsonb_build_object('safe',true));
            """);
        checks.Add("ISOLATED_VALID_FIXTURES_ALL_10_TABLES_PASS");
        async Task Reject(string name, string sql, string sqlState)
        {
            await transaction.SaveAsync("negative_case");
            try { await Execute(sql); }
            catch (PostgresException error) when (error.SqlState == sqlState)
            {
                await transaction.RollbackAsync("negative_case");
                await transaction.ReleaseAsync("negative_case");
                checks.Add("ISOLATED_"+name+"_PASS");
                return;
            }
            throw new InvalidOperationException("CONSTRAINT_EXPECTATION_FAILED");
        }
        await Reject("CASE_INSENSITIVE_DEPARTMENT",$"INSERT INTO public.departments(code,name,row_version) VALUES('dept','Duplicate',{token})","23505");
        await Reject("CASE_INSENSITIVE_ASSET",$"INSERT INTO public.assets(asset_code,name,asset_type_id,owning_department_id,row_version) VALUES('asset','Duplicate',10001,10001,{token})","23505");
        await Reject("CASE_INSENSITIVE_SERIAL",$"INSERT INTO public.assets(asset_code,serial_number,name,asset_type_id,owning_department_id,row_version) VALUES('OTHER','serial','Duplicate',10001,10001,{token})","23505");
        await Reject("ROLE_LINK_UNIQUE","INSERT INTO public.user_roles(user_id,role_id) VALUES(10001,10001)","23505");
        await Reject("PERMISSION_LINK_UNIQUE","INSERT INTO public.role_permissions(role_id,permission_id) VALUES(10001,10001)","23505");
        await Reject("FK_ENFORCED","UPDATE public.assets SET asset_type_id=999999 WHERE id=10001","23503");
        await Reject("FK_NO_CASCADE","DELETE FROM public.departments WHERE id=10001","23503");
        await Reject("NOT_NULL","UPDATE public.assets SET name=NULL WHERE id=10001","23502");
        await Reject("VARCHAR_LENGTH","UPDATE public.assets SET asset_code=repeat('X',51) WHERE id=10001","22001");
        await Reject("PRICE_NONNEGATIVE","UPDATE public.assets SET purchase_cost=-1 WHERE id=10001","23514");
        await Reject("PRICE_NOT_NAN","UPDATE public.assets SET purchase_cost='NaN'::numeric WHERE id=10001","23514");
        await Reject("ASSET_STATUS","UPDATE public.assets SET current_status='INVALID' WHERE id=10001","23514");
        await Reject("ARCHIVE_PAIR","UPDATE public.assets SET is_archived=true WHERE id=10001","23514");
        await Reject("WARRANTY_DATE","UPDATE public.assets SET purchase_date='2026-10-02',warranty_end_date='2026-10-01' WHERE id=10001","23514");
        await Reject("CONCURRENCY_TOKEN_LENGTH","UPDATE public.assets SET row_version=decode('01','hex') WHERE id=10001","23514");
        await Reject("DEPARTMENT_NOT_SELF","UPDATE public.departments SET parent_department_id=id WHERE id=10001","23514");
        await Reject("USEFUL_LIFE","UPDATE public.asset_types SET default_useful_life_months=0 WHERE id=10001","23514");
        await Reject("LOGIN_COUNTER","UPDATE public.users SET failed_login_count=-1 WHERE id=10001","23514");
        await Reject("TOKEN_VERSION","UPDATE public.users SET token_version=-1 WHERE id=10001","23514");
        await Reject("ADMIN_LOCK_PAIR","UPDATE public.users SET is_admin_locked=true WHERE id=10001","23514");
        await Reject("HISTORY_STATUS_CHANGE","INSERT INTO public.asset_status_histories(asset_id,from_status,to_status,source,correlation_id) VALUES(10001,'IN_STOCK','IN_STOCK','SYSTEM','00000000-0000-0000-0000-000000000001')","23514");
        await Reject("HISTORY_SOURCE","INSERT INTO public.asset_status_histories(asset_id,to_status,source,correlation_id) VALUES(10001,'IN_USE','INVALID','00000000-0000-0000-0000-000000000001')","23514");
        await Reject("AUDIT_ACTOR","INSERT INTO public.audit_logs(action,outcome,correlation_id) VALUES('test','SUCCESS','00000000-0000-0000-0000-000000000001')","23514");
        await Reject("AUDIT_OUTCOME","INSERT INTO public.audit_logs(actor_type,action,outcome,correlation_id) VALUES('SYSTEM','test','INVALID','00000000-0000-0000-0000-000000000001')","23514");
        await Reject("AUDIT_JSON_SHAPE","INSERT INTO public.audit_logs(actor_type,action,outcome,correlation_id,metadata_json) VALUES('SYSTEM','test','SUCCESS','00000000-0000-0000-0000-000000000001','123')","23514");
        await Reject("AUDIT_HASH_LENGTH","INSERT INTO public.audit_logs(actor_type,action,outcome,correlation_id,entry_hash) VALUES('SYSTEM','test','SUCCESS','00000000-0000-0000-0000-000000000001',decode('01','hex'))","23514");
        await Reject("AUDIT_APPEND_ONLY_UPDATE","UPDATE public.audit_logs SET action='changed'","55000");
        await Reject("HISTORY_APPEND_ONLY_DELETE","DELETE FROM public.asset_status_histories","55000");
        await using (var verify = new NpgsqlCommand("SELECT purchase_cost=123456789.12 AND current_status='IN_STOCK' AND NOT is_archived AND created_at_utc IS NOT NULL FROM public.assets WHERE id=10001",connection,transaction))
            if (!Equals(await verify.ExecuteScalarAsync(),true)) throw new InvalidOperationException("DEFAULT_OR_PRECISION_MISMATCH");
        checks.Add("ISOLATED_PRECISION_DEFAULTS_UNICODE_PASS");
        await transaction.RollbackAsync();
        checks.Add("ISOLATED_FIXTURES_ROLLED_BACK_PASS");
    }
}
