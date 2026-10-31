using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AquaMonitor.API.Data;

namespace AquaMonitor.Tests;

public class AuthorizationIsolationTests
{
    [Fact]
    public async Task OtherUser_CannotReadOrModifySensorsReadingsAndAlerts()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        var sensorId = await PersistenceIntegrityTests.CreateSensor(owner);
        var reading = await owner.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 50, flowRateLitersPerHour = 600 });
        reading.EnsureSuccessStatusCode();
        var alertId = (await reading.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alertId").GetInt32();
        await PersistenceIntegrityTests.CreateSensor(other, "other@test.com");

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/WaterSensors/{sensorId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/WaterSensors/{sensorId}", new { name = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 12, flowRateLitersPerHour = 20 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/LeakAlerts/{alertId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"/api/LeakAlerts/{alertId}/status", new { status = 2 })).StatusCode);
        foreach (var route in new[] { $"/api/WaterReadings?sensorId={sensorId}", $"/api/LeakAlerts?sensorId={sensorId}" })
        {
            var result = await other.GetFromJsonAsync<JsonElement>(route);
            Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
            Assert.Empty(result.GetProperty("data").EnumerateArray());
        }
        var summary = await other.GetFromJsonAsync<JsonElement>($"/api/WaterReadings/summary?sensorId={sensorId}");
        Assert.Empty(summary.EnumerateArray());
        using var db = new SqliteConnection(factory.DatabasePath);
        Assert.Equal("Sensor", db.Query("SELECT Name FROM WaterSensors ORDER BY Id")[0]["Name"]);
        Assert.Single(db.Query("SELECT * FROM WaterReadings"));
        Assert.Equal(0L, Assert.Single(db.Query("SELECT Status FROM LeakAlerts"))["Status"]);
    }

    [Fact]
    public async Task Administrator_CanManageOtherUsersResources_WhileReportsRemainScoped()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var owner = factory.CreateClient();
        using var admin = factory.CreateClient();
        var sensorId = await PersistenceIntegrityTests.CreateSensor(owner);
        await PersistenceIntegrityTests.CreateSensor(admin, "administrator@test.com");
        using (var db = new SqliteConnection(factory.DatabasePath))
            db.Execute("UPDATE Users SET Role='Admin' WHERE Email='administrator@test.com'");
        var login = await admin.PostAsJsonAsync("/api/Auth/login", new { email = "administrator@test.com", password = "Test-password-123" });
        login.EnsureSuccessStatusCode();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/WaterSensors/{sensorId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/WaterSensors/{sensorId}", new { name = "Admin updated" })).StatusCode);
        var reading = await admin.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 50, flowRateLitersPerHour = 600 });
        Assert.Equal(HttpStatusCode.Created, reading.StatusCode);
        var alertId = (await reading.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alertId").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/LeakAlerts/{alertId}/status", new { status = 2 })).StatusCode);
        var report = await admin.PostAsJsonAsync("/api/WaterImpactReports/generate", new { year = DateTime.UtcNow.Year, month = DateTime.UtcNow.Month, mitigationPlan = "Test mitigation" });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var json = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.05, json.GetProperty("totalConsumptionM3").GetDouble());
        Assert.Equal(1, json.GetProperty("leaksDetected").GetInt32());
        Assert.Equal(1, json.GetProperty("leaksResolved").GetInt32());
        var reportId = json.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/WaterImpactReports/{reportId}")).StatusCode);
        Assert.Equal(0, (await owner.GetFromJsonAsync<JsonElement>("/api/WaterImpactReports")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/WaterImpactReports/{reportId}")).StatusCode);
    }
}
