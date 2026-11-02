using AquaMonitor.Application.Abstractions;
using AquaMonitor.Infrastructure.Repositories;

namespace AquaMonitor.Infrastructure.Persistence;

public sealed class SqliteWorkSessionFactory(IDbConnectionFactory connections) : IWorkSessionFactory
{
    public IWorkSession Open() => new SqliteWorkSession(connections.Create());
}

internal sealed class SqliteWorkSession(SqliteConnection db) : IWorkSession
{
    public ISensorRepository Sensors { get; } = new SqliteSensorRepository(db);
    public T InTransaction<T>(Func<T> operation) => db.InTransaction(operation);
    public void Dispose() => db.Dispose();
}
