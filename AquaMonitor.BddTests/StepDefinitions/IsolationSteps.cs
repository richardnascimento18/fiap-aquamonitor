namespace AquaMonitor.BddTests.StepDefinitions;

[Binding]
public sealed class IsolationSteps(ScenarioWorld world)
{
    [Given("que um sensor e seu alerta pertencem a outra pessoa")]
    public async Task ForeignSensor()
    {
        world.Owner = await world.App.User("owner@example.test");
        world.SensorId = await ApiScenario.Sensor(world.Owner);
        world.AlertId = await ApiScenario.Alert(world.Owner, world.SensorId);
        world.Actor = await world.App.User("other@example.test");
    }

    [When("tento alterar esse sensor e registrar consumo nele")]
    public async Task AttemptChanges()
    {
        using var update = await world.Actor.PutAsJsonAsync($"/api/WaterSensors/{world.SensorId}", new { name = "Intruso" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        using var reading = await ApiScenario.Reading(world.Actor, world.SensorId, 350);
        Assert.Equal(HttpStatusCode.Forbidden, reading.StatusCode);
        Assert.Empty(await reading.Content.ReadAsStringAsync());
        using var alert = await world.Actor.PatchAsJsonAsync($"/api/LeakAlerts/{world.AlertId}/status", new { status = 2 });
        Assert.Equal(HttpStatusCode.NotFound, alert.StatusCode);
    }

    [Then("os registros da proprietária devem permanecer intactos")]
    public async Task Unchanged()
    {
        var owner = Assert.IsType<HttpClient>(world.Owner);
        var sensor = await owner.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{world.SensorId}");
        Assert.Equal("Reservatório", sensor.GetProperty("name").GetString());
        Assert.Equal(1, (await owner.GetFromJsonAsync<JsonElement>("/api/WaterReadings")).GetProperty("totalCount").GetInt32());
        Assert.Equal("Open", (await owner.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{world.AlertId}")).GetProperty("status").GetString());
        foreach (var route in new[] { "WaterSensors", "WaterReadings", "LeakAlerts" })
            Assert.Empty((await world.Actor.GetFromJsonAsync<JsonElement>($"/api/{route}")).GetProperty("data").EnumerateArray());
    }
}
