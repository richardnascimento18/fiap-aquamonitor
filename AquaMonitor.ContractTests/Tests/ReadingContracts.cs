namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class ReadingContracts : ContractTest
{
    [Operation("READING-01", "positive")]
    public async Task READING01_Success()
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var sensor = await ApiScenario.Sensor(owner);
        using var normal = await ApiScenario.Reading(owner, sensor, 100, 50);
        using var anomaly = await ApiScenario.Reading(owner, sensor, 300, 70);
        await ApiScenario.Alert(other, await ApiScenario.Sensor(other));
        using var response = await owner.GetAsync($"/api/WaterReadings?sensorId={sensor}&anomaliesOnly=true&from=2025-06-01&to=2025-07-01&pageSize=1");
        var body = await Body(response);
        var row = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(sensor, row.GetProperty("sensorId").GetInt32());
        Assert.True(row.GetProperty("isAnomaly").GetBoolean());
        Assert.Equal(70, row.GetProperty("litersConsumed").GetDouble());
        await Complete(response);
    }

    [Operation("READING-01", "negative")]
    public async Task READING01_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/WaterReadings?pageSize=101");
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("PageSize", out _));
        await Complete(response);
    }

    [Operation("READING-02", "positive")]
    public async Task READING02_Success()
    {
        using var client = await App.User();
        var sensor = await ApiScenario.Sensor(client);
        using var response = await ApiScenario.Reading(client, sensor, 300, 120);
        var body = await Body(response);
        Assert.True(body.GetProperty("alertGenerated").GetBoolean());
        var rows = await client.GetFromJsonAsync<JsonElement>($"/api/WaterReadings?sensorId={sensor}");
        Schemas.AssertValid("paged-readings", rows);
        var saved = Assert.Single(rows.GetProperty("data").EnumerateArray());
        Assert.Equal(body.GetProperty("readingId").GetInt32(), saved.GetProperty("id").GetInt32());
        Assert.True(saved.GetProperty("isAnomaly").GetBoolean());
        Assert.Equal(120, saved.GetProperty("litersConsumed").GetDouble());
        var alert = await client.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{body.GetProperty("alertId").GetInt32()}");
        Schemas.AssertValid("alert-response", alert);
        Assert.Equal("Critical", alert.GetProperty("severity").GetString());
        Assert.Equal(sensor, alert.GetProperty("sensorId").GetInt32());
        Assert.Equal(App.Clock.Now.UtcDateTime, alert.GetProperty("createdAt").GetDateTime());
        await Complete(response);
    }

    [Operation("READING-02", "negative")]
    public async Task READING02_Failure()
    {
        using var client = await App.User();
        var sensor = await ApiScenario.Sensor(client);
        using var response = await ApiScenario.Reading(client, sensor, -1);
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("FlowRateLitersPerHour", out _));
        await AssertNoMeasurements(client);
        await Complete(response);
    }

    [Operation("READING-03", "positive")]
    public async Task READING03_Success()
    {
        using var client = await App.User();
        var sensor = await ApiScenario.Sensor(client);
        using var first = await ApiScenario.Reading(client, sensor, 50, 100);
        using var second = await ApiScenario.Reading(client, sensor, 150, 300);
        using var outside = await ApiScenario.Reading(client, sensor, 80, 5000, "2025-05-10T12:00:00Z");
        using var response = await client.GetAsync($"/api/WaterReadings/summary?sensorId={sensor}&from=2025-06-01&to=2025-07-01");
        var row = Assert.Single((await Body(response)).EnumerateArray());
        Assert.Equal(sensor, row.GetProperty("sensorId").GetInt32());
        Assert.Equal(400, row.GetProperty("totalLiters").GetDouble());
        Assert.Equal(.4, row.GetProperty("totalM3").GetDouble());
        Assert.Equal(100, row.GetProperty("averageFlowRate").GetDouble());
        Assert.Equal(150, row.GetProperty("maxFlowRate").GetDouble());
        Assert.Equal(1, row.GetProperty("anomalyCount").GetInt32());
        await Complete(response);
    }

    [Operation("READING-03", "negative")]
    public async Task READING03_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/WaterReadings/summary?from=2025-07-01&to=2025-06-01");
        Assert.Equal("Periodo ou sensor invalido.", (await Body(response)).GetProperty("message").GetString());
        await Complete(response);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public async Task AtOrBelowThreshold_PersistsNormalReadingWithoutAlert(double flow)
    {
        using var client = await App.User();
        var sensor = await ApiScenario.Sensor(client);
        using var response = await ApiScenario.Reading(client, sensor, flow);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Body(response);
        Schemas.AssertValid("reading-created", created);
        Assert.False(created.GetProperty("alertGenerated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("alertId").ValueKind);
        var row = Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/WaterReadings")).GetProperty("data").EnumerateArray());
        Assert.False(row.GetProperty("isAnomaly").GetBoolean());
        Assert.Equal(flow, row.GetProperty("flowRateLitersPerHour").GetDouble());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/LeakAlerts")).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("missing", 404)]
    [InlineData("foreign", 403)]
    [InlineData("inactive", 400)]
    public async Task InvalidSensorState_PreservesMeasurements(string state, int status)
    {
        using var owner = await App.User();
        var sensor = await ApiScenario.Sensor(owner);
        var client = owner;
        if (state == "foreign") client = await App.User("other@example.test");
        if (state == "inactive")
        {
            using var update = await owner.PutAsJsonAsync($"/api/WaterSensors/{sensor}", new { isActive = false });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        }
        using var response = await ApiScenario.Reading(client, state == "missing" ? 99999 : sensor, 350);
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 403) Assert.Empty(await response.Content.ReadAsStringAsync());
        else Schemas.AssertValid("message-error", await Body(response));
        await AssertNoMeasurements(owner);
    }

    private static async Task AssertNoMeasurements(HttpClient client)
    {
        foreach (var route in new[] { "WaterReadings", "LeakAlerts" })
            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"/api/{route}")).GetProperty("totalCount").GetInt32());
    }
}
