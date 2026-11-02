using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Tests;

public class DomainRulesTests
{
    [Theory]
    [InlineData(199.99, false)]
    [InlineData(200, false)]
    [InlineData(200.00000000000003, true)]
    public void ReadingThreshold_IsStrict(double flow, bool expected)
        => Assert.Equal(expected, ReadingRules.Classify(1, true, 10, flow, 200));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidMeasurements_AreRejected(double value)
    {
        Assert.Throws<DomainRuleException>(() => ReadingRules.Classify(1, true, value, 0, 200));
        Assert.Throws<DomainRuleException>(() => ReadingRules.Classify(1, true, 0, value, 200));
    }

    [Fact]
    public void Reading_RequiresValidActiveSensor()
    {
        Assert.Throws<DomainRuleException>(() => ReadingRules.Classify(0, true, 0, 0, 200));
        Assert.Throws<DomainRuleException>(() => ReadingRules.Classify(1, false, 0, 0, 200));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.09)]
    [InlineData(100000.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidThresholds_AreRejected(double value)
        => Assert.Throws<DomainRuleException>(() => SensorRules.ValidateThreshold(value));

    [Theory]
    [InlineData(0.1)]
    [InlineData(100000)]
    public void ThresholdEndpoints_AreValid(double value) => SensorRules.ValidateThreshold(value);

    [Theory]
    [InlineData(200.00000000000003, AlertSeverity.Low)]
    [InlineData(299.99999999999994, AlertSeverity.Low)]
    [InlineData(300, AlertSeverity.Medium)]
    [InlineData(399.99999999999994, AlertSeverity.Medium)]
    [InlineData(400, AlertSeverity.High)]
    [InlineData(599.9999999999999, AlertSeverity.High)]
    [InlineData(600, AlertSeverity.Critical)]
    public void Severity_BoundariesArePreserved(double flow, AlertSeverity expected)
        => Assert.Equal(expected, AlertRules.Severity(flow, 200));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public void Lifecycle_PreservesAllExistingTransitions(int current, int next)
    {
        var now = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        Assert.Equal(next == 2 ? now : (DateTime?)null, AlertRules.Transition(current, next, now));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    public void InvalidStatuses_AreRejected(int current, int next)
        => Assert.Throws<DomainRuleException>(() => AlertRules.Transition(current, next, DateTime.UnixEpoch));

    [Fact]
    public void AlertMessage_UsesProvidedTimeAndSeverity()
    {
        var message = AlertRules.Message(600, 200, "Sensor", "Test", new DateTime(2024, 2, 29, 10, 30, 0, DateTimeKind.Utc));
        Assert.StartsWith("[CRITICAL] Sensor 'Sensor' em 'Test'", message);
        Assert.EndsWith("29/02/2024 10:30 UTC.", message);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(1000, 0, 1, -1, 0)]
    [InlineData(1000, 1000, 1, 0, 0)]
    [InlineData(2000, 1000, 2, -1, -100)]
    [InlineData(500, 1000, 0.5, 0.5, 50)]
    [InlineData(1234.5678, 3000, 1.235, 1.765, 58.85)]
    public void Impact_PreservesDifferenceAndRounding(double current, double previous, double total, double difference, double reduction)
        => Assert.Equal(new ImpactCalculation(total, difference, reduction), ImpactCalculation.Calculate(current, previous));

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(double.MaxValue, double.Epsilon)]
    public void InvalidImpactValues_AreRejected(double current, double previous)
        => Assert.Throws<DomainRuleException>(() => ImpactCalculation.Calculate(current, previous));

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 12, 31)]
    public void ReportingPeriods_HaveExactUtcMonthBoundaries(int year, int month, int days)
    {
        var period = new ReportingPeriod(year, month);
        Assert.Equal(DateTimeKind.Utc, period.Start.Kind);
        Assert.Equal(days, (period.EndExclusive - period.Start).Days);
        Assert.Equal(period.Start, period.PreviousStart.AddMonths(1));
        Assert.Equal(period.Start.AddMonths(1), period.EndExclusive);
    }

    [Theory]
    [InlineData(1999, 1)]
    [InlineData(2101, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void InvalidPeriods_AreRejected(int year, int month)
        => Assert.Throws<DomainRuleException>(() => new ReportingPeriod(year, month));
}

public class SensorEntityTests
{
    [Theory]
    [InlineData(0, 1, 500)]
    [InlineData(1, 0, 500)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, double.NaN)]
    public void SensorState_EnforcesItsIdentityOwnershipAndThreshold(int id, int owner, double threshold)
        => Assert.Throws<DomainRuleException>(() => new AquaMonitor.Domain.Entities.WaterSensor(id, "Test", "Room", threshold, true, DateTime.UnixEpoch, owner));

    [Fact]
    public void SensorState_ClassifiesUsingItsOwnActivationAndThreshold()
    {
        var sensor = new AquaMonitor.Domain.Entities.WaterSensor(1, "Test", "Room", 500, true, DateTime.UnixEpoch, 1);
        Assert.False(sensor.ClassifyReading(0, 500));
        Assert.True(sensor.ClassifyReading(0, double.BitIncrement(500)));
        var inactive = new AquaMonitor.Domain.Entities.WaterSensor(1, "Test", "Room", 500, false, DateTime.UnixEpoch, 1);
        Assert.Throws<DomainRuleException>(() => inactive.ClassifyReading(0, 1000));
    }
}
