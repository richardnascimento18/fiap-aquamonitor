using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Alerts;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Readings;
using AquaMonitor.Application.Sensors;

namespace AquaMonitor.Tests;

[Trait("Category", "Application")]
public class AlertServiceTests
{
    [Fact]
    public void LifecycleAndStatistics_EnforceOwnershipAndUseInjectedClock()
    {
        using var database = new ServiceDatabase();
        var actor = new Actor(1, false);
        var sensor = new SensorService(database.Sessions, database.Clock).Create(actor, new CreateSensorVM { Name = "Test", Location = "Room" });
        var reading = new ReadingService(database.Sessions, database.Clock).Create(actor, new CreateReadingVM { SensorId = sensor.Id, FlowRateLitersPerHour = 1500 });
        var service = new AlertService(database.Sessions, database.Clock);
        var id = reading.AlertId!.Value;
        Assert.Equal(404, Assert.Throws<ApplicationFailure>(() => service.UpdateStatus(new Actor(2, false), id, new UpdateAlertStatusVM { Status = 2 })).StatusCode);
        Assert.Equal(403, Assert.Throws<ApplicationFailure>(() => service.Statistics(actor)).StatusCode);
        var resolved = service.UpdateStatus(actor, id, new UpdateAlertStatusVM { Status = 2 });
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime, resolved.ResolvedAt);
        Assert.Equal("Resolved", resolved.Status);
        var reopened = service.UpdateStatus(actor, id, new UpdateAlertStatusVM { Status = 0 });
        Assert.Null(reopened.ResolvedAt);
        var stats = service.Statistics(new Actor(2, true));
        Assert.Equal(1, stats.TotalAlerts);
        Assert.Equal(1, stats.OpenAlerts);
        Assert.Equal(0, stats.ResolvedAlerts);
        Assert.Equal(new SeverityCount("Critical", 1), Assert.Single(stats.BySeverity));
    }
}
