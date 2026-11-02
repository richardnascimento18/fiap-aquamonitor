using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Domain.Entities;

public sealed class WaterSensor
{
    public int Id { get; }
    public string Name { get; }
    public string Location { get; }
    public double AlertThresholdLitersPerHour { get; }
    public bool IsActive { get; }
    public DateTime InstalledAt { get; }
    public int UserId { get; }

    public WaterSensor(int id, string name, string location, double threshold, bool isActive, DateTime installedAt, int userId)
    {
        if (id <= 0 || userId <= 0) throw new DomainRuleException("Sensor ou proprietario invalido.");
        SensorRules.ValidateThreshold(threshold);
        Id = id;
        Name = name;
        Location = location;
        AlertThresholdLitersPerHour = threshold;
        IsActive = isActive;
        InstalledAt = installedAt;
        UserId = userId;
    }

    public bool ClassifyReading(double liters, double flow)
        => ReadingRules.Classify(Id, IsActive, liters, flow, AlertThresholdLitersPerHour);
}
