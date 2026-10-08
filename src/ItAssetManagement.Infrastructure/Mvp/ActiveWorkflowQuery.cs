using System.Data.Common;

namespace ItAssetManagement.Infrastructure.Mvp;

// Compatibility query deliberately independent of the workflow EF model. The caller
// owns the open connection/transaction and locks the asset before changing its state.
public static class ActiveWorkflowQuery
{
    public static async Task<bool> HasActiveWorkflowAsync(DbConnection connection, DbTransaction transaction,
        long assetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var inventory = connection.CreateCommand();
        inventory.Transaction = transaction;
        inventory.CommandText = "SELECT to_regclass('public.asset_assignments') IS NOT NULL, to_regclass('public.maintenance_tickets') IS NOT NULL";
        bool assignments, maintenance;
        await using (var reader = await inventory.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Workflow table inventory did not return a row.");
            assignments = reader.GetBoolean(0);
            maintenance = reader.GetBoolean(1);
        }
        if (!assignments && !maintenance) return false;

        var predicates = new List<string>();
        if (assignments)
            predicates.Add("EXISTS(SELECT 1 FROM public.asset_assignments WHERE asset_id=@asset AND returned_at_utc IS NULL AND NOT is_archived)");
        if (maintenance)
            predicates.Add("EXISTS(SELECT 1 FROM public.maintenance_tickets WHERE asset_id=@asset AND status IN ('PENDING','IN_PROGRESS') AND NOT is_archived)");
        await using var active = connection.CreateCommand();
        active.Transaction = transaction;
        active.CommandText = "SELECT " + string.Join(" OR ", predicates);
        var parameter = active.CreateParameter();
        parameter.ParameterName = "asset";
        parameter.Value = assetId;
        active.Parameters.Add(parameter);
        return (bool)(await active.ExecuteScalarAsync(cancellationToken))!;
    }
}
