using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Readings;
using AquaMonitor.Application.Reports;
using AquaMonitor.Application.Sensors;

namespace AquaMonitor.Tests;

[Trait("Category", "Persistence")]
public class HistoricalTimestampTests
{
    [Theory]
    [InlineData("2024-02-01 00:00:00")]
    [InlineData("2024-02-01T00:00:00Z")]
    [InlineData("2024-02-01T00:00:00.0000000Z")]
    [InlineData("2024-01-31T21:00:00-03:00")]
    public void MixedTimestampFormats_PreserveExactUtcPeriodsWithoutRewritingHistory(string first)
    {
        using var database = new ServiceDatabase();
        var admin = new Actor(1, true);
        var sensor = new SensorService(database.Sessions, database.Clock).Create(admin, new CreateSensorVM { Name = "Test", Location = "Room" });
        var start = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        using (var db = database.Connections.Create())
        {
            foreach (var (timestamp, liters) in new[] { ("2024-01-01 00:00:00", 2000), (first, 1000), (end.AddTicks(-1).ToString("o"), 500), (end.ToString("o"), 4000) })
                db.Execute("INSERT INTO WaterReadings (SensorId,LitersConsumed,RecordedAt) VALUES (@id,@liters,@at)", new Dictionary<string, object?> { ["@id"] = sensor.Id, ["@liters"] = liters, ["@at"] = timestamp });
            db.Execute("INSERT INTO LeakAlerts (SensorId,Message,CreatedAt,ResolvedAt) VALUES (@id,'Test',@created,@resolved)", new Dictionary<string, object?> { ["@id"] = sensor.Id, ["@created"] = first, ["@resolved"] = end.AddTicks(-1).ToString("o") });
        }
        var report = new ReportService(database.Sessions, database.Clock).Generate(admin, new GenerateReportVM { Year = 2024, Month = 2, MitigationPlan = "Inspect" });
        Assert.Equal(1.5, report.TotalConsumptionM3);
        Assert.Equal(0.5, report.SavedWaterM3);
        Assert.Equal(25, report.ReductionPercent);
        Assert.Equal(1, report.LeaksDetected);
        Assert.Equal(1, report.LeaksResolved);
        var readings = new ReadingService(database.Sessions, database.Clock);
        var list = readings.List(admin, new ReadingFilterVM { From = start, To = end.AddTicks(-1) });
        Assert.Equal(2, list.TotalCount);
        var summary = Assert.Single(readings.Summary(admin, start, end.AddTicks(-1), sensor.Id));
        Assert.Equal(1500, summary.TotalLiters);
        Assert.Equal(start, summary.PeriodStart);
        Assert.Equal(end.AddTicks(-1), summary.PeriodEnd);
        using var verify = database.Connections.Create();
        Assert.Equal(first, verify.Query("SELECT RecordedAt FROM WaterReadings WHERE Id=2")[0]["RecordedAt"]);
    }
}
