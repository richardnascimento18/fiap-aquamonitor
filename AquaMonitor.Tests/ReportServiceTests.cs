using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Readings;
using AquaMonitor.Application.Reports;
using AquaMonitor.Application.Sensors;

namespace AquaMonitor.Tests;

[Trait("Category", "Application")]
public class ReportServiceTests
{
    [Fact]
    public void Reports_PreserveFormulasOwnershipAndUtcGenerationTime()
    {
        using var database = new ServiceDatabase();
        var admin = new Actor(1, true);
        var service = new ReportService(database.Sessions, database.Clock);
        var request = new GenerateReportVM { Year = 2024, Month = 2, MitigationPlan = "Inspect" };
        var empty = service.Generate(admin, request);
        Assert.Equal(0, empty.TotalConsumptionM3);
        Assert.Equal(0, empty.ReductionPercent);
        Assert.Equal(403, Assert.Throws<ApplicationFailure>(() => service.Generate(new Actor(2, false), request)).StatusCode);
        Assert.Equal(404, Assert.Throws<ApplicationFailure>(() => service.Get(new Actor(2, false), empty.Id)).StatusCode);
        var sensor = new SensorService(database.Sessions, database.Clock).Create(admin, new CreateSensorVM { Name = "Test", Location = "Room" });
        var readings = new ReadingService(database.Sessions, database.Clock);
        readings.Create(admin, new CreateReadingVM { SensorId = sensor.Id, LitersConsumed = 1000, RecordedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc) });
        readings.Create(admin, new CreateReadingVM { SensorId = sensor.Id, LitersConsumed = 2000, FlowRateLitersPerHour = 1500 });
        var report = service.Generate(admin, request);
        Assert.Equal(2, report.TotalConsumptionM3);
        Assert.Equal(-1, report.SavedWaterM3);
        Assert.Equal(-100, report.ReductionPercent);
        Assert.Equal(1, report.LeaksDetected);
        Assert.Equal(0, report.LeaksResolved);
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime, report.GeneratedAt);
        Assert.Equal(2, service.List(admin, 1, 10).TotalCount);
        service.Delete(admin, report.Id);
        Assert.Equal(404, Assert.Throws<ApplicationFailure>(() => service.Get(admin, report.Id)).StatusCode);
    }
}
