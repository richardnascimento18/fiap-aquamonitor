using AquaMonitor.API.Data;
using AquaMonitor.API.Models;

namespace AquaMonitor.API.Services;

public interface IAlertService
{
    Task<int?> EvaluateReadingAsync(int sensorId, double flowRate, double threshold, string sensorName, string location);
}

public sealed class AlertService : IAlertService
{
    private readonly IDbConnectionFactory _factory;

    public AlertService(IDbConnectionFactory factory) => _factory = factory;

    public Task<int?> EvaluateReadingAsync(int sensorId, double flowRate, double threshold, string sensorName, string location)
    {
        if (flowRate <= threshold) return Task.FromResult<int?>(null);

        var severity = DetermineSeverity(flowRate, threshold);
        var message  = $"[{SeverityLabel(severity)}] Sensor '{sensorName}' em '{location}' registrou vazao de {flowRate:F1} L/h — limite: {threshold:F1} L/h. Possivel vazamento detectado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC.";

        using var db = _factory.Create();
        db.Execute(
            "INSERT INTO LeakAlerts (SensorId, Message, Severity, Status, TriggerFlowRate, CreatedAt) VALUES (@sid,@msg,@sev,0,@tfr,@ca)",
            new Dictionary<string, object?>
            {
                ["@sid"] = sensorId,
                ["@msg"] = message,
                ["@sev"] = severity,
                ["@tfr"] = flowRate,
                ["@ca"]  = DateTime.UtcNow.ToString("o")
            });

        var id = (int)db.LastInsertRowId();
        return Task.FromResult<int?>(id);
    }

    private static int DetermineSeverity(double flowRate, double threshold)
    {
        var ratio = flowRate / threshold;
        return ratio switch { >= 3.0 => 4, >= 2.0 => 3, >= 1.5 => 2, _ => 1 };
    }

    private static string SeverityLabel(int s) => s switch { 4 => "CRITICAL", 3 => "HIGH", 2 => "MEDIUM", _ => "LOW" };
}
