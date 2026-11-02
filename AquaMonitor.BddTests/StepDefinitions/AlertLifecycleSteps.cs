namespace AquaMonitor.BddTests.StepDefinitions;

[Binding]
public sealed class AlertLifecycleSteps(ScenarioWorld world)
{
    [Given("que possuo um alerta de possível vazamento")]
    public async Task AlertExists()
    {
        world.Actor = await world.App.User();
        world.SensorId = await ApiScenario.Sensor(world.Actor);
        world.AlertId = await ApiScenario.Alert(world.Actor, world.SensorId);
    }

    [When("reconheço e depois resolvo o alerta")]
    public async Task Resolve()
    {
        using var acknowledged = await world.Actor.PatchAsJsonAsync($"/api/LeakAlerts/{world.AlertId}/status", new { status = 1 });
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        Assert.Equal("Acknowledged", (await ApiScenario.Json(acknowledged)).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, (await ApiScenario.Json(acknowledged)).GetProperty("resolvedAt").ValueKind);
        world.App.Clock.Now = world.App.Clock.Now.AddMinutes(10);
        using var resolved = await world.Actor.PatchAsJsonAsync($"/api/LeakAlerts/{world.AlertId}/status", new { status = 2 });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
    }

    [Then("a resolução deve registrar o instante correto")]
    public async Task ResolutionTime()
    {
        var alert = await world.Actor.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{world.AlertId}");
        Assert.Equal("Resolved", alert.GetProperty("status").GetString());
        Assert.Equal(world.App.Clock.Now.UtcDateTime, alert.GetProperty("resolvedAt").GetDateTime());
    }

    [When("reabro o alerta para nova investigação")]
    public async Task Reopen()
    {
        using var response = await world.Actor.PatchAsJsonAsync($"/api/LeakAlerts/{world.AlertId}/status", new { status = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Then("o alerta deve ficar aberto sem data de resolução")]
    public async Task Reopened()
    {
        var alert = await world.Actor.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{world.AlertId}");
        Assert.Equal("Open", alert.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, alert.GetProperty("resolvedAt").ValueKind);
    }
}
