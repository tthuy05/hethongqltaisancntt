using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using ItAssetManagement.Infrastructure.Mvp;

namespace ItAssetManagement.IntegrationTests;

// Offline ADO contract tests: no EF workflow model, provider, connection or cloud writes.
public sealed class ActiveWorkflowQueryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Only_existing_tables_are_queried_and_result_is_asset_scoped(
        bool assignments, bool maintenance, bool hasActive)
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new ScriptedConnection(assignments, maintenance, hasActive);
        var transaction = new BorrowedTransaction(connection);
        var result = await ActiveWorkflowQuery.HasActiveWorkflowAsync(connection, transaction, 987654321, cancellation.Token);

        Assert.Equal(hasActive, result);
        Assert.Equal(assignments || maintenance ? 2 : 1, connection.Commands.Count);
        var inventory = connection.Commands[0];
        Assert.Contains("to_regclass('public.asset_assignments')", inventory.CommandText);
        Assert.Contains("to_regclass('public.maintenance_tickets')", inventory.CommandText);
        Assert.Empty(inventory.Parameters);
        Assert.All(connection.Commands, command =>
        {
            Assert.Same(transaction, command.Transaction);
            Assert.Same(connection, command.Connection);
            Assert.Equal(cancellation.Token, command.ExecutedWith);
            Assert.True(command.WasDisposed);
            Assert.StartsWith("SELECT ", command.CommandText);
        });
        Assert.True(connection.InventoryReader!.IsClosed);
        Assert.False(transaction.WasDisposed);
        Assert.Equal(0, connection.OpenCalls);
        Assert.Equal(0, connection.CloseCalls);
        if (!assignments && !maintenance) return;

        var active = connection.Commands[1];
        Assert.Equal(assignments, active.CommandText.Contains("FROM public.asset_assignments", StringComparison.Ordinal));
        Assert.Equal(maintenance, active.CommandText.Contains("FROM public.maintenance_tickets", StringComparison.Ordinal));
        Assert.DoesNotContain("987654321", active.CommandText);
        var parameter = Assert.Single(active.Parameters.Cast<DbParameter>());
        Assert.Equal("asset", parameter.ParameterName);
        Assert.Equal(987654321L, parameter.Value);
        if (assignments)
            Assert.Contains("asset_id=@asset AND returned_at_utc IS NULL AND NOT is_archived", active.CommandText);
        if (maintenance)
            Assert.Contains("asset_id=@asset AND status IN ('PENDING','IN_PROGRESS') AND NOT is_archived", active.CommandText);
        Assert.Equal(assignments && maintenance, active.CommandText.Contains(" OR ", StringComparison.Ordinal));
        Assert.DoesNotContain("'RESOLVED'", active.CommandText);
        Assert.DoesNotContain("'FAILED'", active.CommandText);
        Assert.DoesNotContain("'CANCELLED'", active.CommandText);
    }

    [Fact]
    public async Task Cancelled_request_does_not_create_or_execute_a_command()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connection = new ScriptedConnection(true, true, true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ActiveWorkflowQuery.HasActiveWorkflowAsync(connection, new BorrowedTransaction(connection), 7, cancellation.Token));
        Assert.Empty(connection.Commands);
    }

    [Fact]
    public async Task Inventory_cancellation_propagates_and_disposes_command_without_active_query()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new ScriptedConnection(true, true, true) { CancelInventory = true };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ActiveWorkflowQuery.HasActiveWorkflowAsync(connection, new BorrowedTransaction(connection), 7, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(Assert.Single(connection.Commands).WasDisposed);
    }

    [Fact]
    public async Task Missing_inventory_row_fails_closed_instead_of_allowing_archive()
    {
        var connection = new ScriptedConnection(true, true, true) { EmptyInventory = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ActiveWorkflowQuery.HasActiveWorkflowAsync(connection, new BorrowedTransaction(connection), 7, CancellationToken.None));
        Assert.True(Assert.Single(connection.Commands).WasDisposed);
        Assert.True(connection.InventoryReader!.IsClosed);
    }

    private sealed class ScriptedConnection(bool assignments, bool maintenance, bool hasActive) : DbConnection
    {
        public List<ScriptedCommand> Commands { get; } = [];
        public DbDataReader? InventoryReader { get; private set; }
        public bool CancelInventory { get; init; }
        public bool EmptyInventory { get; init; }
        public int OpenCalls { get; private set; }
        public int CloseCalls { get; private set; }
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "offline";
        public override string DataSource => "offline";
        public override string ServerVersion => "offline";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Open() { OpenCalls++; throw new NotSupportedException(); }
        public override void Close() { CloseCalls++; throw new NotSupportedException(); }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand()
        {
            var command = new ScriptedCommand(this, Commands.Count == 0);
            Commands.Add(command); return command;
        }
        public DbDataReader Inventory(CancellationToken token)
        {
            if (CancelInventory) throw new OperationCanceledException(token);
            var table = new DataTable();
            table.Columns.Add("assignments", typeof(bool)); table.Columns.Add("maintenance", typeof(bool));
            if (!EmptyInventory) table.Rows.Add(assignments, maintenance);
            InventoryReader = table.CreateDataReader(); return InventoryReader;
        }
        public bool Active()
        {
            Assert.True(InventoryReader!.IsClosed);
            return hasActive;
        }
    }

    private sealed class BorrowedTransaction(DbConnection connection) : DbTransaction
    {
        public bool WasDisposed { get; private set; }
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() => throw new NotSupportedException();
        public override void Rollback() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }

    private sealed class ScriptedCommand(ScriptedConnection connection, bool inventory) : DbCommand
    {
        private readonly ParametersList _parameters = new();
        public CancellationToken ExecutedWith { get; private set; }
        public bool WasDisposed { get; private set; }
        [AllowNull] public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        public override void Cancel() => throw new NotSupportedException();
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() => throw new NotSupportedException();
        protected override DbParameter CreateDbParameter() => new Parameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            Assert.True(inventory); ExecutedWith = cancellationToken;
            return Task.FromResult(connection.Inventory(cancellationToken));
        }
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            Assert.False(inventory); ExecutedWith = cancellationToken;
            return Task.FromResult<object?>(connection.Active());
        }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }

    private sealed class Parameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = "";
        [AllowNull] public override string SourceColumn { get; set; } = "";
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() => DbType = DbType.Object;
    }

    private sealed class ParametersList : DbParameterCollection
    {
        private readonly List<DbParameter> _values = [];
        public override int Count => _values.Count;
        public override object SyncRoot => ((ICollection)_values).SyncRoot;
        public override int Add(object value) { _values.Add((DbParameter)value); return _values.Count - 1; }
        public override void AddRange(Array values) { foreach (var value in values) Add(value!); }
        public override void Clear() => _values.Clear();
        public override bool Contains(object value) => _values.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_values).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _values.GetEnumerator();
        public override int IndexOf(object value) => _values.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _values.FindIndex(x => x.ParameterName == parameterName);
        public override void Insert(int index, object value) => _values.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _values.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _values.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _values[index];
        protected override DbParameter GetParameter(string parameterName) => _values[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _values[IndexOf(parameterName)] = value;
    }
}
