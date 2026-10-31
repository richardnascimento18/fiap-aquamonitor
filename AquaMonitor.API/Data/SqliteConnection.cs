using Microsoft.Data.Sqlite;
using NativeConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace AquaMonitor.API.Data;

/// <summary>Small compatibility adapter preserving the existing parameterized SQL API.</summary>
public sealed class SqliteConnection : IDisposable
{
    private readonly NativeConnection _connection;
    private SqliteTransaction? _transaction;

    public SqliteConnection(string path)
    {
        var settings = path.Contains('=')
            ? new SqliteConnectionStringBuilder(path)
            : new SqliteConnectionStringBuilder { DataSource = path };
        if (string.IsNullOrWhiteSpace(settings.DataSource))
            throw new InvalidOperationException("A persistent SQLite data source is required.");
        settings.ForeignKeys = true;
        // Close native file handles deterministically, including on test-host shutdown.
        settings.Pooling = false;
        settings.DefaultTimeout = 30;
        _connection = new NativeConnection(settings.ToString());
        try { _connection.Open(); }
        catch { _connection.Dispose(); throw; }
    }

    public void Execute(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var command = CreateCommand(sql, parameters);
        command.ExecuteNonQuery();
    }

    public List<Dictionary<string, object?>> Query(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var command = CreateCommand(sql, parameters);
        using var reader = command.ExecuteReader();
        var results = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    public long LastInsertRowId() => (long)Query("SELECT last_insert_rowid() AS Id")[0]["Id"]!;

    public T InTransaction<T>(Func<T> operation)
    {
        if (_transaction is not null) throw new InvalidOperationException("Nested transactions are not supported.");
        using var transaction = _connection.BeginTransaction();
        _transaction = transaction;
        try
        {
            var result = operation();
            transaction.Commit();
            return result;
        }
        finally
        {
            // Disposing an uncommitted transaction rolls it back, including when operation throws.
            _transaction = null;
        }
    }

    private SqliteCommand CreateCommand(string sql, IReadOnlyDictionary<string, object?>? parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _transaction;
        if (parameters is not null)
            foreach (var (key, value) in parameters)
                command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }

    public void Dispose() => _connection.Dispose();
}
