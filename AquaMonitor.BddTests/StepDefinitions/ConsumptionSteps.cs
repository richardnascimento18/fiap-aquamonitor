namespace AquaMonitor.BddTests.StepDefinitions;

[Binding]
public sealed class ConsumptionSteps(ScenarioWorld world)
{
    [Given("que possuo um sensor ativo com limite de {int} litros por hora")]
    public async Task ActiveSensor(int threshold)
    {
        world.Actor = await world.App.User();
        world.SensorId = await ApiScenario.Sensor(world.Actor, threshold);
    }

    [When("registro uma leitura de {int} litros por hora")]
    public async Task Record(int flow) => world.Response = await ApiScenario.Reading(world.Actor, world.SensorId, flow);

    [Then("a leitura deve ser persistida sem anomalia")]
    public async Task Normal() => await AssertReading(false);

    [Then("a leitura deve ser persistida como anômala")]
    public async Task Anomalous() => await AssertReading(true);

    private async Task AssertReading(bool anomaly)
    {
        Assert.Equal(HttpStatusCode.Created, world.Response.StatusCode);
        var created = await ApiScenario.Json(world.Response);
        Assert.Equal(anomaly, created.GetProperty("alertGenerated").GetBoolean());
        var readings = await world.Actor.GetFromJsonAsync<JsonElement>($"/api/WaterReadings?sensorId={world.SensorId}");
        var row = Assert.Single(readings.GetProperty("data").EnumerateArray());
        Assert.Equal(created.GetProperty("readingId").GetInt32(), row.GetProperty("id").GetInt32());
        Assert.Equal(anomaly, row.GetProperty("isAnomaly").GetBoolean());
        Assert.Equal(100, row.GetProperty("litersConsumed").GetDouble());
    }

    [Then("nenhum alerta deve ser criado")]
    public async Task NoAlert()
    {
        var alerts = await world.Actor.GetFromJsonAsync<JsonElement>("/api/LeakAlerts");
        Assert.Equal(0, alerts.GetProperty("totalCount").GetInt32());
        Assert.Empty(alerts.GetProperty("data").EnumerateArray());
    }

    [Then("um alerta de severidade {string} deve ser criado")]
    public async Task AlertSeverity(string severity)
    {
        var created = await ApiScenario.Json(world.Response);
        var alerts = await world.Actor.GetFromJsonAsync<JsonElement>("/api/LeakAlerts");
        var alert = Assert.Single(alerts.GetProperty("data").EnumerateArray());
        Assert.Equal(created.GetProperty("alertId").GetInt32(), alert.GetProperty("id").GetInt32());
        Assert.Equal(world.SensorId, alert.GetProperty("sensorId").GetInt32());
        Assert.Equal(severity, alert.GetProperty("severity").GetString());
        Assert.Equal("Open", alert.GetProperty("status").GetString());
        Assert.Equal(world.App.Clock.Now.UtcDateTime, alert.GetProperty("createdAt").GetDateTime());
    }

    [Then("a medição inválida deve ser recusada")]
    public async Task InvalidMeasurement()
    {
        Assert.Equal(HttpStatusCode.BadRequest, world.Response.StatusCode);
        Assert.True((await ApiScenario.Json(world.Response)).GetProperty("errors").TryGetProperty("FlowRateLitersPerHour", out _));
        var readings = await world.Actor.GetFromJsonAsync<JsonElement>("/api/WaterReadings");
        Assert.Empty(readings.GetProperty("data").EnumerateArray());
    }
}
