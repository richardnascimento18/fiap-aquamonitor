namespace AquaMonitor.Domain.Rules;

public static class SensorRules
{
    public static void ValidateThreshold(double threshold)
    {
        if (!double.IsFinite(threshold) || threshold is < 0.1 or > 100000)
            throw new DomainRuleException("Limite de alerta deve ser entre 0.1 e 100.000 L/h.");
    }

    public static void EnsureActive(bool active)
    {
        if (!active) throw new DomainRuleException("Sensor inativo.");
    }
}
