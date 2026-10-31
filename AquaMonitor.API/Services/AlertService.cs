using AquaMonitor.API.Data;
using AquaMonitor.API.Models;

namespace AquaMonitor.API.Services;

public interface IAlertService
{
    int? EvaluateReading(SqliteConnection db, int sensorId, double flowRate, double threshold, string sensorName, string location);
}

public sealed class AlertService : IAlertService
{
    public int? EvaluateReading(SqliteConnection db, int sensorId, double flowRate, double threshold, string sensorName, string location)
    {
        if (flowRate <= threshold) return null;

        var severity = DetermineSeverity(flowRate, threshold);
        var message = $"[{SeverityLabel(severity)}] Sensor '{sensorName}' em '{location}' registrou vazao de {flowRate:F1} L/h — limite: {threshold:F1} L/h. Possivel vazamento detectado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC.";

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

    private static int DetermineSeverity(double flowRate, double threshold)
    {
        var ratio = flowRate / threshold;
        return ratio switch { >= 3.0 => 4, >= 2.0 => 3, >= 1.5 => 2, _ => 1 };
    }

    private static string SeverityLabel(int s) => s switch { 4 => "CRITICAL", 3 => "HIGH", 2 => "MEDIUM", _ => "LOW" };
}
