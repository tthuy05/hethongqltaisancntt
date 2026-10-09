using Npgsql;

namespace ItAssetManagement.Infrastructure.Data;

internal static class AssignmentMaintenanceConstraintVerification
{
    public static async Task RunAsync(NpgsqlConnection connection, List<string> checks)
    {
        if (connection.Database != NeonAssignmentMaintenanceSetup.ValidationDatabase)
            throw new InvalidOperationException("ISOLATION_REQUIRED");
        await using var transaction = await connection.BeginTransactionAsync();
        const string token = "decode(repeat('01',16),'hex')";
        var prefix = "AM" + Guid.NewGuid().ToString("N")[..16];
        async Task<long> Id(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        async Task Execute(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync();
        }
        // Source-controlled SQL and generated hexadecimal prefix, never any credential/input.
        var department = await Id($"INSERT INTO public.departments(code,name,row_version) VALUES('{prefix}','Assignment fixture',{token}) RETURNING id");
        var type = await Id($"INSERT INTO public.asset_types(code,name,row_version) VALUES('{prefix}','Assignment fixture',{token}) RETURNING id");
        var user = await Id($"""
            INSERT INTO public.users(username,normalized_username,email,normalized_email,password_hash,full_name,row_version)
            VALUES('{prefix}','{prefix.ToUpperInvariant()}','{prefix}@fixture.invalid','{prefix.ToUpperInvariant()}@FIXTURE.INVALID',
                'NOT_A_USABLE_PASSWORD_HASH','Assignment fixture',{token}) RETURNING id
            """);
        var asset = await Id($"INSERT INTO public.assets(asset_code,name,asset_type_id,owning_department_id,row_version) VALUES('{prefix}','Thiết bị thử',{type},{department},{token}) RETURNING id");
        var assignment = await Id($"INSERT INTO public.asset_assignments(asset_id,assigned_user_id,assigned_by_user_id,row_version) VALUES({asset},{user},{user},{token}) RETURNING id");
        var ticket = await Id($"INSERT INTO public.maintenance_tickets(ticket_code,asset_id,title,description,requested_by_user_id,estimated_cost,row_version) VALUES('{prefix}',{asset},'Test','Test',{user},123456789.12,{token}) RETURNING id");
        checks.Add("ISOLATED_VALID_ASSIGNMENT_MAINTENANCE_FIXTURES_PASS");
        async Task Reject(string name, string sql, string sqlState, string? constraint = null)
        {
            await transaction.SaveAsync("negative_case");
            try { await Execute(sql); }
            catch (PostgresException error) when (error.SqlState == sqlState && (constraint is null || error.ConstraintName == constraint))
            {
                await transaction.RollbackAsync("negative_case"); await transaction.ReleaseAsync("negative_case");
                checks.Add("ISOLATED_" + name + "_PASS"); return;
            }
            throw new InvalidOperationException("CONSTRAINT_EXPECTATION_FAILED");
        }
        var a = $"UPDATE public.asset_assignments SET "; var aw = $" WHERE id={assignment}";
        var t = $"UPDATE public.maintenance_tickets SET "; var tw = $" WHERE id={ticket}";
        const string missing = "9223372036854775807";
        await Reject("ASSIGNMENT_ASSET_FK", a + "asset_id=" + missing + aw, "23503", "fk_asset_assignments_asset_id");
        await Reject("ASSIGNMENT_USER_FK", a + "assigned_user_id=" + missing + aw, "23503", "fk_asset_assignments_assigned_user_id");
        await Reject("ASSIGNMENT_DEPARTMENT_FK", a + "assigned_user_id=NULL,assigned_department_id=" + missing + aw, "23503", "fk_asset_assignments_assigned_department_id");
        await Reject("ASSIGNMENT_ASSIGNED_ACTOR_FK", a + "assigned_by_user_id=" + missing + aw, "23503", "fk_asset_assignments_assigned_by_user_id");
        await Reject("ASSIGNMENT_RETURNED_ACTOR_FK", a + "returned_at_utc=assigned_at_utc,returned_by_user_id=" + missing + aw, "23503", "fk_asset_assignments_returned_by_user_id");
        await Reject("MAINTENANCE_ASSET_FK", t + "asset_id=" + missing + tw, "23503", "fk_maintenance_tickets_asset_id");
        await Reject("MAINTENANCE_REQUESTER_FK", t + "requested_by_user_id=" + missing + tw, "23503", "fk_maintenance_tickets_requested_by_user_id");
        await Reject("MAINTENANCE_ASSIGNEE_FK", t + "assigned_to_user_id=" + missing + tw, "23503", "fk_maintenance_tickets_assigned_to_user_id");
        await Reject("FK_NO_CASCADE", $"DELETE FROM public.assets WHERE id={asset}", "23503");
        await Reject("ASSIGNMENT_TARGET_XOR_NONE", a + "assigned_user_id=NULL" + aw, "23514", "ck_asset_assignments_target_xor");
        await Reject("ASSIGNMENT_TARGET_XOR_BOTH", a + $"assigned_department_id={department}" + aw, "23514", "ck_asset_assignments_target_xor");
        await Reject("ASSIGNMENT_RETURNED_PAIR", a + $"returned_by_user_id={user}" + aw, "23514", "ck_asset_assignments_returned_actor");
        await Reject("ASSIGNMENT_ARCHIVE_ONLY_CLOSED", a + "is_archived=true" + aw, "23514", "ck_asset_assignments_closed_before_archive");
        await Reject("ASSIGNMENT_EXPECTED_DATE", a + "expected_return_at_utc=assigned_at_utc-interval '1 second'" + aw, "23514", "ck_asset_assignments_expected_after_assigned");
        await Reject("ASSIGNMENT_RETURN_DATE", a + "returned_at_utc=assigned_at_utc-interval '1 second'" + aw, "23514", "ck_asset_assignments_returned_after_assigned");
        await Reject("ASSIGNMENT_TOKEN", a + "row_version=decode('01','hex')" + aw, "23514", "ck_asset_assignments_row_version_length");
        await Reject("ASSIGNMENT_NOTE_LENGTH", a + "assignment_note=repeat('X',1001)" + aw, "22001");
        await Reject("ASSIGNMENT_REQUIRED_ASSET", a + "asset_id=NULL" + aw, "23502");
        await Reject("ASSIGNMENT_ONE_ACTIVE", $"INSERT INTO public.asset_assignments(asset_id,assigned_department_id,assigned_by_user_id,row_version) VALUES({asset},{department},{user},{token})", "23505", "uq_asset_assignments_one_active");
        await Reject("MAINTENANCE_CASE_INSENSITIVE_CODE", $"INSERT INTO public.maintenance_tickets(ticket_code,asset_id,title,description,requested_by_user_id,row_version) VALUES('{prefix.ToLowerInvariant()}',{asset},'Test','Test',{user},{token})", "23505", "uq_maintenance_tickets_code");
        foreach (var (name, update, constraint) in new[]
        {
            ("MAINTENANCE_STATUS", "status='INVALID'", "ck_maintenance_tickets_status"),
            ("MAINTENANCE_PRIORITY", "priority='INVALID'", "ck_maintenance_tickets_priority"),
            ("MAINTENANCE_STARTED_DATE", "started_at_utc=opened_at_utc-interval '1 second'", "ck_maintenance_tickets_started_after_opened"),
            ("MAINTENANCE_DUE_DATE", "due_at_utc=opened_at_utc-interval '1 second'", "ck_maintenance_tickets_due_after_opened"),
            ("MAINTENANCE_RESOLVED_DATE", "resolved_at_utc=opened_at_utc-interval '1 second'", "ck_maintenance_tickets_resolved_after_started"),
            ("MAINTENANCE_ESTIMATED_NEGATIVE", "estimated_cost=-1", "ck_maintenance_tickets_estimated_cost_nonnegative"),
            ("MAINTENANCE_ESTIMATED_NAN", "estimated_cost='NaN'::numeric", "ck_maintenance_tickets_estimated_cost_nonnegative"),
            ("MAINTENANCE_ACTUAL_NEGATIVE", "actual_cost=-1", "ck_maintenance_tickets_actual_cost_nonnegative"),
            ("MAINTENANCE_ACTUAL_NAN", "actual_cost='NaN'::numeric", "ck_maintenance_tickets_actual_cost_nonnegative"),
            ("MAINTENANCE_IN_PROGRESS_ASSIGNEE", "status='IN_PROGRESS'", "ck_maintenance_tickets_in_progress_requires_assignee"),
            ("MAINTENANCE_RESOLUTION_REQUIRED", $"status='RESOLVED',assigned_to_user_id={user},started_at_utc=opened_at_utc", "ck_maintenance_tickets_resolved_requires_resolution"),
            ("MAINTENANCE_ARCHIVE_TERMINAL", "is_archived=true", "ck_maintenance_tickets_archive_terminal"),
            ("MAINTENANCE_TOKEN", "row_version=decode('01','hex')", "ck_maintenance_tickets_row_version_length")
        }) await Reject(name, t + update + tw, "23514", constraint);
        await Execute(t + $"status='IN_PROGRESS',assigned_to_user_id={user},started_at_utc=opened_at_utc" + tw);
        await Reject("MAINTENANCE_ONE_IN_PROGRESS", $"INSERT INTO public.maintenance_tickets(ticket_code,asset_id,title,description,requested_by_user_id,assigned_to_user_id,status,opened_at_utc,started_at_utc,row_version) VALUES('{prefix}2',{asset},'Test','Test',{user},{user},'IN_PROGRESS','2026-10-08T00:00:00Z','2026-10-08T00:00:00Z',{token})", "23505", "uq_maintenance_one_in_progress");
        await Execute(a + $"returned_at_utc=assigned_at_utc,returned_by_user_id={user}" + aw);
        await Execute($"INSERT INTO public.asset_assignments(asset_id,assigned_department_id,assigned_by_user_id,row_version) VALUES({asset},{department},{user},{token})");
        await using (var defaults = new NpgsqlCommand($"SELECT estimated_cost=123456789.12 AND priority='MEDIUM' AND NOT is_archived AND created_at_utc IS NOT NULL FROM public.maintenance_tickets WHERE id={ticket}", connection, transaction))
            if (!Equals(await defaults.ExecuteScalarAsync(), true)) throw new InvalidOperationException("CONSTRAINT_EXPECTATION_FAILED");
        checks.Add("ISOLATED_PRECISION_DEFAULTS_AND_REASSIGN_PASS");
        await transaction.RollbackAsync();
        await using var remains = new NpgsqlCommand("SELECT (SELECT count(*) FROM public.assets WHERE asset_code=@prefix)+(SELECT count(*) FROM public.maintenance_tickets WHERE ticket_code=@prefix)", connection);
        remains.Parameters.AddWithValue("prefix", prefix);
        if (Convert.ToInt64(await remains.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("CONSTRAINT_EXPECTATION_FAILED");
        checks.Add("ISOLATED_CONSTRAINT_FIXTURES_ROLLED_BACK_PASS");
    }
}
