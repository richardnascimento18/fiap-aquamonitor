using AquaMonitor.Domain.Entities;
using AquaMonitor.Domain.Rules;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Security;

public interface IAlertService
{
    int? EvaluateReading(SqliteConnection db, int sensorId, double flowRate, double threshold, string sensorName, string location);
}

public sealed class AlertService : IAlertService
{
    public int? EvaluateReading(SqliteConnection db, int sensorId, double flowRate, double threshold, string sensorName, string location)
    {
        if (flowRate <= threshold) return null;

        var severity = (int)AlertRules.Severity(flowRate, threshold);
        var message = AlertRules.Message(flowRate, threshold, sensorName, location, DateTime.UtcNow);

        db.Execute(
            "INSERT INTO LeakAlerts (SensorId, Message, Severity, Status, TriggerFlowRate, CreatedAt) VALUES (@sid,@msg,@sev,0,@tfr,@ca)",
            new Dictionary<string, object?>
            {
                ["@sid"] = sensorId,
                ["@msg"] = message,
                ["@sev"] = severity,
                ["@tfr"] = flowRate,
                ["@ca"] = DateTime.UtcNow.ToString("o")
            });

        var id = (int)db.LastInsertRowId();
        return id;
    }

}
