namespace AquaMonitor.Domain.Rules;

public enum AlertSeverity { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum AlertStatus { Open = 0, Acknowledged = 1, Resolved = 2 }

public static class AlertRules
{
    public static AlertSeverity Severity(double flow, double threshold)
    {
        ReadingRules.ValidateMeasurement(flow);
        SensorRules.ValidateThreshold(threshold);
        if (flow <= threshold) throw new DomainRuleException("A leitura nao e uma anomalia.");
        return (flow / threshold) switch
        {
            >= 3 => AlertSeverity.Critical,
            >= 2 => AlertSeverity.High,
            >= 1.5 => AlertSeverity.Medium,
            _ => AlertSeverity.Low
        };
    }

    public static string Message(double flow, double threshold, string sensor, string location, DateTime utcNow)
    {
        var severity = Severity(flow, threshold).ToString().ToUpperInvariant();
        return $"[{severity}] Sensor '{sensor}' em '{location}' registrou vazao de {flow:F1} L/h — limite: {threshold:F1} L/h. Possivel vazamento detectado em {utcNow:dd/MM/yyyy HH:mm} UTC.";
    }

    // Existing clients may reopen resolved alerts. Every resolution assigns a new UTC timestamp;
    // reopening clears it. Preserve those transitions, including repeated resolution.
    public static DateTime? Transition(int current, int next, DateTime utcNow)
    {
        if (!Enum.IsDefined((AlertStatus)current) || !Enum.IsDefined((AlertStatus)next))
            throw new DomainRuleException("Status invalido.");
        return next == (int)AlertStatus.Resolved ? utcNow : null;
    }
}
