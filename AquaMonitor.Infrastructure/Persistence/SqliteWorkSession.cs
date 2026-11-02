using AquaMonitor.Application.Abstractions;
using AquaMonitor.Infrastructure.Repositories;

namespace AquaMonitor.Infrastructure.Persistence;

public sealed class SqliteWorkSessionFactory(IDbConnectionFactory connections) : IWorkSessionFactory
{
    public IWorkSession Open() => new SqliteWorkSession(connections.Create());
}

internal sealed class SqliteWorkSession(SqliteConnection db) : IWorkSession
{
    public IReportRepository Reports { get; } = new SqliteReportRepository(db);
    public ISensorRepository Sensors { get; } = new SqliteSensorRepository(db);
    public IReadingRepository Readings { get; } = new SqliteReadingRepository(db);
    public IAlertRepository Alerts { get; } = new SqliteAlertRepository(db);
    public T InTransaction<T>(Func<T> operation) => db.InTransaction(operation);
    public void Dispose() => db.Dispose();
}
