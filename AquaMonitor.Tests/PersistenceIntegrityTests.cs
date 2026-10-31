using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AquaMonitor.API.Data;

namespace AquaMonitor.Tests;

public class PersistenceIntegrityTests
{
    [Fact]
    public void NewConnections_EnforceForeignKeys_AndInitializationPreservesData()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var connections = new SqliteConnectionFactory(factory.DatabasePath);
        using (var db = connections.Create())
        {
            Assert.Equal(1L, db.Query("PRAGMA foreign_keys")[0]["foreign_keys"]);
            Assert.ThrowsAny<Exception>(() => db.Execute("INSERT INTO WaterReadings (SensorId) VALUES (999999)"));
            Assert.ThrowsAny<Exception>(() => db.Execute("INSERT INTO LeakAlerts (SensorId,Message) VALUES (999999,'test')"));
            Assert.ThrowsAny<Exception>(() => db.Execute("INSERT INTO WaterSensors (Name,Location,UserId) VALUES ('test','test',999999)"));
            Assert.ThrowsAny<Exception>(() => db.Execute("INSERT INTO WaterImpactReports (Title,Year,Month,GeneratedByUserId) VALUES ('test',2026,10,999999)"));
            db.Execute("INSERT INTO Users (Name,Email,PasswordHash) VALUES ('Preserved','preserved@test.com','legacy-hash')");
        }
        DatabaseInitializer.Initialize(connections);
        DatabaseInitializer.Initialize(connections);
        using var reopened = connections.Create();
        Assert.Equal(1L, reopened.Query("PRAGMA foreign_keys")[0]["foreign_keys"]);
        var user = Assert.Single(reopened.Query("SELECT * FROM Users"));
        Assert.Equal("preserved@test.com", user["Email"]);
        Assert.Equal("legacy-hash", user["PasswordHash"]);
        Assert.Empty(reopened.Query("PRAGMA foreign_key_check"));
    }

    [Theory]
    [InlineData(100.0, false, null)]
    [InlineData(200.0, false, null)]
    [InlineData(201.0, true, 1)]
    [InlineData(300.0, true, 2)]
    [InlineData(400.0, true, 3)]
    [InlineData(600.0, true, 4)]
    public async Task Reading_PersistsExpectedAnomalyAndSeverity(double flow, bool anomaly, int? severity)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var sensorId = await CreateSensor(client);
        var response = await client.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 50.0, flowRateLitersPerHour = flow });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(anomaly, result.GetProperty("alertGenerated").GetBoolean());
        using var db = new SqliteConnection(factory.DatabasePath);
        var reading = Assert.Single(db.Query("SELECT * FROM WaterReadings"));
        Assert.Equal((long)result.GetProperty("readingId").GetInt32(), reading["Id"]);
        Assert.Equal(50.0, reading["LitersConsumed"]);
        Assert.Equal(flow, reading["FlowRateLitersPerHour"]);
        Assert.Equal(anomaly ? 1L : 0L, reading["IsAnomaly"]);
        var alerts = db.Query("SELECT * FROM LeakAlerts");
        if (anomaly)
        {
            var alert = Assert.Single(alerts);
            Assert.Equal((long)severity!.Value, alert["Severity"]);
            Assert.Equal((long)sensorId, alert["SensorId"]);
            Assert.Equal((long)result.GetProperty("alertId").GetInt32(), alert["Id"]);
            Assert.Equal(flow, alert["TriggerFlowRate"]);
        }
        else
        {
            Assert.Empty(alerts);
            Assert.Equal(JsonValueKind.Null, result.GetProperty("alertId").ValueKind);
        }
        DatabaseInitializer.Initialize(new SqliteConnectionFactory(factory.DatabasePath));
        Assert.Single(db.Query("SELECT * FROM WaterReadings"));
    }

    [Fact]
    public async Task AlertInsertFailure_RollsBackReading_AndHidesDiagnostics()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var sensorId = await CreateSensor(client);
        using (var db = new SqliteConnection(factory.DatabasePath))
            db.Execute("CREATE TRIGGER fail_alert BEFORE INSERT ON LeakAlerts BEGIN SELECT RAISE(ABORT, 'sensitive-sql-test-detail'); END");
        var response = await client.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 50.0, flowRateLitersPerHour = 600.0 });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-sql-test-detail", body);
        Assert.DoesNotContain("INSERT", body);
        Assert.DoesNotContain(factory.DatabasePath, body);
        using var verify = new SqliteConnection(factory.DatabasePath);
        Assert.Empty(verify.Query("SELECT * FROM WaterReadings"));
        Assert.Empty(verify.Query("SELECT * FROM LeakAlerts"));
        Assert.Single(verify.Query("SELECT * FROM WaterSensors"));
    }

    [Fact]
    public async Task DeletingSensorWithHistory_ReturnsConflict_AndPreservesRecords()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var sensorId = await CreateSensor(client);
        (await client.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = 50, flowRateLitersPerHour = 600 })).EnsureSuccessStatusCode();
        using (var db = new SqliteConnection(factory.DatabasePath)) db.Execute("UPDATE Users SET Role='Admin'");
        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email = "owner@test.com", password = "Test-password-123" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/WaterSensors/{sensorId}")).StatusCode);
        using var verify = new SqliteConnection(factory.DatabasePath);
        Assert.Single(verify.Query("SELECT * FROM WaterSensors"));
        Assert.Single(verify.Query("SELECT * FROM WaterReadings"));
        Assert.Single(verify.Query("SELECT * FROM LeakAlerts"));
    }

    internal static async Task<int> CreateSensor(HttpClient client, string email = "owner@test.com")
    {
        var auth = await client.PostAsJsonAsync("/api/Auth/register", new { name = "Owner", email, password = "Test-password-123" });
        Assert.Equal(HttpStatusCode.Created, auth.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await auth.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var response = await client.PostAsJsonAsync("/api/WaterSensors", new { name = "Sensor", location = "Test", alertThresholdLitersPerHour = 200.0 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
}
