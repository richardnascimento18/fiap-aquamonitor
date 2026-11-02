using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AquaMonitor.Tests;

[Trait("Category", "Integration")]
public class ApiCompatibilityTests
{
    [Fact]
    public async Task SensorPaginationAndReadingCreation_PreservePublicShapes()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var sensorId = await PersistenceIntegrityTests.CreateSensor(client);
        var sensor = await client.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{sensorId}");
        Assert.Equal(new[] { "alertThresholdLitersPerHour", "id", "installedAt", "isActive", "location", "name", "openAlerts", "totalReadings", "userId", "userName" }, Keys(sensor));
        var page = await client.GetFromJsonAsync<JsonElement>("/api/WaterSensors?pageSize=1");
        Assert.Equal(new[] { "data", "hasNextPage", "hasPreviousPage", "page", "pageSize", "totalCount", "totalPages" }, Keys(page));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var response = await client.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 10, flowRateLitersPerHour = 300 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "alertGenerated", "alertId", "readingId" }, Keys(reading));
        var alert = await client.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{reading.GetProperty("alertId").GetInt32()}");
        Assert.Equal(new[] { "createdAt", "id", "message", "resolvedAt", "sensorId", "sensorLocation", "sensorName", "severity", "status", "triggerFlowRate" }, Keys(alert));
        Assert.Equal("Medium", alert.GetProperty("severity").GetString());
        Assert.Equal("Open", alert.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ResolvedAlert_CanBeReopened_AndMissingResourcesKeepMessageBody()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var sensorId = await PersistenceIntegrityTests.CreateSensor(client);
        var reading = await client.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 10, flowRateLitersPerHour = 600 });
        var id = (await reading.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alertId").GetInt32();
        foreach (var status in new[] { 2, 1, 0, 2 })
        {
            var response = await client.PatchAsJsonAsync($"/api/LeakAlerts/{id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var alert = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status == 2 ? JsonValueKind.String : JsonValueKind.Null, alert.GetProperty("resolvedAt").ValueKind);
        }
        var missing = await client.GetAsync("/api/WaterSensors/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Sensor nao encontrado.", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }

    private static string[] Keys(JsonElement value) => value.EnumerateObject().Select(p => p.Name).Order().ToArray();
}
