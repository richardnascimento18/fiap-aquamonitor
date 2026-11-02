namespace AquaMonitor.BddTests.StepDefinitions;

[Binding]
public sealed class ReportSteps(ScenarioWorld world)
{
    [Given("que foram consumidos 2000 litros em maio e 1500 litros em junho")]
    public async Task KnownConsumption()
    {
        world.Actor = await world.App.Admin();
        world.SensorId = await ApiScenario.Sensor(world.Actor);
        using var previous = await ApiScenario.Reading(world.Actor, world.SensorId, 50, 2000, "2025-05-10T12:00:00Z");
        using var current = await ApiScenario.Reading(world.Actor, world.SensorId, 50, 1500);
        Assert.Equal(HttpStatusCode.Created, previous.StatusCode);
        Assert.Equal(HttpStatusCode.Created, current.StatusCode);
    }

    [When("gero o relatório de junho de 2025")]
    public async Task Generate() => world.Response = await ApiScenario.Report(world.Actor);

    [Then("o relatório deve apresentar 1,5 metro cúbico de consumo")]
    public async Task Consumption()
    {
        Assert.Equal(HttpStatusCode.Created, world.Response.StatusCode);
        var report = await ApiScenario.Json(world.Response);
        Assert.Equal(2025, report.GetProperty("year").GetInt32());
        Assert.Equal(6, report.GetProperty("month").GetInt32());
        Assert.Equal(1.5, report.GetProperty("totalConsumptionM3").GetDouble());
        Assert.Equal(world.App.Clock.Now.UtcDateTime, report.GetProperty("generatedAt").GetDateTime());
    }

    [Then("a diferença observada deve ser 0,5 metro cúbico e 25 por cento")]
    public async Task Difference()
    {
        var report = await ApiScenario.Json(world.Response);
        var saved = await world.Actor.GetFromJsonAsync<JsonElement>($"/api/WaterImpactReports/{report.GetProperty("id").GetInt32()}");
        Assert.Equal(.5, saved.GetProperty("savedWaterM3").GetDouble());
        Assert.Equal(25, saved.GetProperty("reductionPercent").GetDouble());
        Assert.Equal(report.GetRawText(), saved.GetRawText());
    }
}
