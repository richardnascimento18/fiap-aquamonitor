using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Sensors;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Tests;

public class ApplicationServiceTests
{
    [Fact]
    public void Sensors_EnforceOwnershipAndAdministratorRoleWithoutHttp()
    {
        using var database = new ServiceDatabase();
        var service = new SensorService(database.Sessions, database.Clock);
        var owner = new Actor(1, false);
        var other = new Actor(2, false);
        var sensor = service.Create(owner, new CreateSensorVM { Name = "Test", Location = "Room" });
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime, sensor.InstalledAt);
        Assert.Equal(1, sensor.UserId);
        Assert.Empty(service.List(other, 1, 10).Data);
        Assert.Equal(404, Assert.Throws<ApplicationFailure>(() => service.Update(other, sensor.Id, new UpdateSensorVM { Name = "stolen" })).StatusCode);
        Assert.Equal(403, Assert.Throws<ApplicationFailure>(() => service.Delete(owner, sensor.Id)).StatusCode);
        Assert.Equal("Test", service.Get(owner, sensor.Id).Name);
        service.Update(new Actor(2, true), sensor.Id, new UpdateSensorVM { IsActive = false });
        Assert.False(service.Get(owner, sensor.Id).IsActive);
        service.Delete(new Actor(2, true), sensor.Id);
        Assert.Equal(404, Assert.Throws<ApplicationFailure>(() => service.Get(owner, sensor.Id)).StatusCode);
    }

    [Fact]
    public void Sensors_ValidateRequestsOutsideHttp()
    {
        using var database = new ServiceDatabase();
        var service = new SensorService(database.Sessions, database.Clock);
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => service.Create(new Actor(1, false), new CreateSensorVM { Name = "Test", Location = "Room", AlertThresholdLitersPerHour = double.NaN }));
        Assert.Equal(400, Assert.Throws<ApplicationFailure>(() => service.List(new Actor(1, false), 0, 10)).StatusCode);
        Assert.Throws<ApplicationFailure>(() => new Actor(0, false));
    }
}

internal sealed class ServiceDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aquaservice_{Guid.NewGuid():N}.db");
    public IDbConnectionFactory Connections { get; }
    public IWorkSessionFactory Sessions { get; }
    public TimeProvider Clock { get; } = new FixedTimeProvider(new DateTimeOffset(2024, 2, 29, 10, 30, 0, TimeSpan.Zero));
    public ServiceDatabase()
    {
        Connections = new SqliteConnectionFactory(_path);
        DatabaseInitializer.Initialize(Connections);
        using var db = Connections.Create();
        db.Execute("INSERT INTO Users (Name,Email,PasswordHash) VALUES ('Owner','owner@test.com','test'),('Other','other@test.com','test')");
        Sessions = new SqliteWorkSessionFactory(Connections);
    }
    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_path + suffix);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
