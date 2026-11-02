using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Readings;
using AquaMonitor.Application.Sensors;
using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Tests;

public class ReadingServiceTests
{
    [Fact]
    public void Creation_RequiresExistingActiveOwnedSensor_OutsideHttp()
    {
        using var database = new ServiceDatabase();
        var sensors = new SensorService(database.Sessions, database.Clock);
        var readings = new ReadingService(database.Sessions, database.Clock);
        var actor = new Actor(1, false);
        var id = sensors.Create(actor, new CreateSensorVM { Name = "Test", Location = "Room" }).Id;
        Assert.Equal(404, Assert.Throws<ApplicationFailure>(() => readings.Create(actor, new CreateReadingVM { SensorId = 999 })).StatusCode);
        Assert.Equal(403, Assert.Throws<ApplicationFailure>(() => readings.Create(new Actor(2, false), new CreateReadingVM { SensorId = id })).StatusCode);
        sensors.Update(actor, id, new UpdateSensorVM { IsActive = false });
        Assert.Throws<DomainRuleException>(() => readings.Create(actor, new CreateReadingVM { SensorId = id }));
        using var db = database.Connections.Create();
        Assert.Empty(db.Query("SELECT * FROM WaterReadings"));
    }

    [Fact]
    public void Creation_UsesOneClockInstant_AndRollsBackOnAlertFailure()
    {
        using var database = new ServiceDatabase();
        var actor = new Actor(1, false);
        var id = new SensorService(database.Sessions, database.Clock).Create(actor, new CreateSensorVM { Name = "Test", Location = "Room" }).Id;
        var readings = new ReadingService(database.Sessions, database.Clock);
        var request = new CreateReadingVM { SensorId = id, LitersConsumed = 10, FlowRateLitersPerHour = 1500 };
        using (var db = database.Connections.Create()) db.Execute("CREATE TRIGGER fail_alert BEFORE INSERT ON LeakAlerts BEGIN SELECT RAISE(ABORT,'failure'); END");
        Assert.ThrowsAny<Exception>(() => readings.Create(actor, request));
        using (var db = database.Connections.Create())
        {
            Assert.Empty(db.Query("SELECT * FROM WaterReadings"));
            Assert.Empty(db.Query("SELECT * FROM LeakAlerts"));
            db.Execute("DROP TRIGGER fail_alert");
        }
        var result = readings.Create(actor, request);
        Assert.True(result.AlertGenerated);
        using var verify = database.Connections.Create();
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime.ToString("o"), Assert.Single(verify.Query("SELECT * FROM WaterReadings"))["RecordedAt"]);
        var alert = Assert.Single(verify.Query("SELECT * FROM LeakAlerts"));
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime.ToString("o"), alert["CreatedAt"]);
        Assert.Contains("29/02/2024 10:30 UTC", (string)alert["Message"]!);
    }
}
