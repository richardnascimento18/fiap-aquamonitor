using AquaMonitor.Application.Abstractions;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Repositories;

internal sealed class SqliteAlertRepository(SqliteConnection db) : IAlertRepository
{
    public int Insert(int sensorId, string message, int severity, double flow, DateTime utcNow)
    {
        db.Execute("INSERT INTO LeakAlerts (SensorId, Message, Severity, Status, TriggerFlowRate, CreatedAt) VALUES (@sid,@msg,@sev,0,@tfr,@ca)",
            new Dictionary<string, object?> { ["@sid"] = sensorId, ["@msg"] = message, ["@sev"] = severity, ["@tfr"] = flow, ["@ca"] = utcNow.ToString("o") });
        return (int)db.LastInsertRowId();
    }
}
