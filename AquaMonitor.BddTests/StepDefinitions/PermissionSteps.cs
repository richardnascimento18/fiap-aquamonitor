namespace AquaMonitor.BddTests.StepDefinitions;

[Binding]
public sealed class PermissionSteps(ScenarioWorld world)
{
    [Given("que sou administrador e existe um sensor sem histórico")]
    public async Task Administrator()
    {
        world.Owner = await world.App.User();
        world.SensorId = await ApiScenario.Sensor(world.Owner);
        world.Actor = await world.App.Admin();
    }

    [When("solicito a remoção desse sensor")]
    public async Task Delete() => world.Response = await world.Actor.DeleteAsync($"/api/WaterSensors/{world.SensorId}");

    [Then("o sensor deve ser removido")]
    public async Task Deleted()
    {
        Assert.Equal(HttpStatusCode.NoContent, world.Response.StatusCode);
        Assert.Empty(await world.Response.Content.ReadAsStringAsync());
        using var lookup = await world.Actor.GetAsync($"/api/WaterSensors/{world.SensorId}");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
        Assert.Equal(0, (await world.Owner!.GetFromJsonAsync<JsonElement>("/api/WaterSensors")).GetProperty("totalCount").GetInt32());
    }

    [Then("a remoção deve ser negada e o sensor preservado")]
    public async Task Denied()
    {
        Assert.Equal(HttpStatusCode.Forbidden, world.Response.StatusCode);
        Assert.Empty(await world.Response.Content.ReadAsStringAsync());
        var sensor = await world.Actor.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{world.SensorId}");
        Assert.Equal("Reservatório", sensor.GetProperty("name").GetString());
    }
}
