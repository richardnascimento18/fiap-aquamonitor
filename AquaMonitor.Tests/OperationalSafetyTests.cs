using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AquaMonitor.API.Data;
using AquaMonitor.API.ViewModels;

namespace AquaMonitor.Tests;

public class OperationalSafetyTests
{
    [Fact]
    public async Task Cors_AllowsOnlyConfiguredOrigin_AndDirectRequestsWork()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        foreach (var (origin, allowed) in new[] { ("https://client.example.test", true), ("https://untrusted.example.test", false) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/WaterSensors");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            var response = await client.SendAsync(request);
            Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
            if (allowed) Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/WaterSensors")).StatusCode);
    }

    [Fact]
    public async Task Cors_DefaultDeniesBrowserOrigins()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        factory.Settings.Remove("Cors:AllowedOrigins:0");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "https://untrusted.example.test");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://client.example.test/path")]
    [InlineData("https://client.example.test/")]
    public void InvalidCorsOrigin_FailsAtStartup(string origin)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        factory.Settings["Cors:AllowedOrigins:0"] = origin;
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("-1")]
    [InlineData("1e999")]
    public async Task InvalidReadingNumbers_ReturnBadRequest_WithoutPersistence(string number)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var sensorId = await PersistenceIntegrityTests.CreateSensor(client);
        using var body = new StringContent($"{{\"sensorId\":{sensorId},\"litersConsumed\":\"{number}\",\"flowRateLitersPerHour\":20}}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/WaterReadings", body)).StatusCode);
        using var db = new SqliteConnection(factory.DatabasePath);
        Assert.Empty(db.Query("SELECT * FROM WaterReadings"));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FiniteNumberValidation_RejectsNonFiniteValues(double value)
        => Assert.False(new FiniteNumberAttribute().IsValid(value));

    [Theory]
    [InlineData("/api/WaterSensors?page=0")]
    [InlineData("/api/WaterReadings?pageSize=101")]
    [InlineData("/api/LeakAlerts?status=3")]
    [InlineData("/api/LeakAlerts?severity=0")]
    [InlineData("/api/WaterImpactReports?pageSize=51")]
    [InlineData("/api/WaterReadings?from=2026-10-31&to=2026-01-01")]
    [InlineData("/api/WaterReadings/summary?from=2026-10-31&to=2026-01-01")]
    [InlineData("/api/WaterReadings/summary?sensorId=0")]
    public async Task InvalidFilters_ReturnBadRequest(string route)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        await PersistenceIntegrityTests.CreateSensor(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route)).StatusCode);
    }

    [Theory]
    [InlineData("/api/WaterSensors")]
    [InlineData("/api/WaterReadings")]
    [InlineData("/api/LeakAlerts")]
    [InlineData("/api/WaterImpactReports")]
    public async Task LargePage_DoesNotOverflowOffset(string route)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        await PersistenceIntegrityTests.CreateSensor(client);
        var response = await client.GetAsync(route + "?page=2147483647");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public void DisposedFactory_RemovesDatabaseAndSidecars()
    {
        var factory = new AquaMonitorWebApplicationFactory();
        using (var client = factory.CreateClient()) Assert.True(File.Exists(factory.DatabasePath));
        factory.Dispose();
        Assert.All(new[] { "", "-wal", "-shm" }, suffix => Assert.False(File.Exists(factory.DatabasePath + suffix)));
    }
}
