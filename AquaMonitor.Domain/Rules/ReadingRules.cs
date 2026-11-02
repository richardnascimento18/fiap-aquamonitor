namespace AquaMonitor.Domain.Rules;

public static class ReadingRules
{
    public static bool Classify(int sensorId, bool active, double liters, double flow, double threshold)
    {
        if (sensorId <= 0) throw new DomainRuleException("SensorId inválido.");
        ValidateMeasurement(liters);
        ValidateMeasurement(flow);
        SensorRules.ValidateThreshold(threshold);
        SensorRules.EnsureActive(active);
        return flow > threshold;
    }

    public static void ValidateMeasurement(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new DomainRuleException("Consumo ou vazao invalido.");
    }
}
