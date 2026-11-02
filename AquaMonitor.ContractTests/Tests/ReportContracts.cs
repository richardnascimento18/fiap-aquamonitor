namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class ReportContracts : ContractTest
{
    [Operation("REPORT-01", "positive")]
    public async Task REPORT01_Success()
    {
        using var admin = await App.Admin();
        using var owner = await App.User();
        using var first = await ApiScenario.Report(admin, 5);
        using var second = await ApiScenario.Report(admin, 6);
        using var response = await admin.GetAsync("/api/WaterImpactReports?pageSize=1");
        var body = await Body(response);
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        Assert.True(body.GetProperty("hasNextPage").GetBoolean());
        var row = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal(6, row.GetProperty("month").GetInt32());
        var page2 = await admin.GetFromJsonAsync<JsonElement>("/api/WaterImpactReports?page=2&pageSize=1");
        Schemas.AssertValid("paged-reports", page2);
        Assert.Equal(5, Assert.Single(page2.GetProperty("data").EnumerateArray()).GetProperty("month").GetInt32());
        var hidden = await owner.GetFromJsonAsync<JsonElement>("/api/WaterImpactReports");
        Schemas.AssertValid("paged-reports", hidden);
        Assert.Empty(hidden.GetProperty("data").EnumerateArray());
        await Complete(response);
    }

    [Operation("REPORT-01", "negative")]
    public async Task REPORT01_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/WaterImpactReports?pageSize=51");
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("pageSize", out _));
        await Complete(response);
    }

    [Operation("REPORT-02", "positive")]
    public async Task REPORT02_Success()
    {
        using var admin = await App.Admin();
        using var generated = await ApiScenario.Report(admin);
        var report = await Body(generated);
        var id = report.GetProperty("id").GetInt32();
        using var response = await admin.GetAsync($"/api/WaterImpactReports/{id}");
        var body = await Body(response);
        Assert.Equal(report.GetRawText(), body.GetRawText());
        Assert.Equal(0, body.GetProperty("totalConsumptionM3").GetDouble());
        Assert.Equal(0, body.GetProperty("reductionPercent").GetDouble());
        Assert.Equal(App.Clock.Now.UtcDateTime, body.GetProperty("generatedAt").GetDateTime());
        await Complete(response);
    }

    [Operation("REPORT-02", "negative")]
    public async Task REPORT02_Failure()
    {
        using var admin = await App.Admin();
        using var owner = await App.User();
        using var generated = await ApiScenario.Report(admin);
        var id = (await Body(generated)).GetProperty("id").GetInt32();
        using var response = await owner.GetAsync($"/api/WaterImpactReports/{id}");
        Assert.Equal("Relatorio nao encontrado.", (await Body(response)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/WaterImpactReports/{id}")).StatusCode);
        await Complete(response);
    }

    [Operation("REPORT-03", "positive")]
    public async Task REPORT03_Success()
    {
        using var owner = await App.User();
        using var admin = await App.Admin();
        var sensor = await ApiScenario.Sensor(owner);
        using var previous = await ApiScenario.Reading(owner, sensor, 50, 2000, "2025-05-10T12:00:00Z");
        using var current = await ApiScenario.Reading(owner, sensor, 300, 1500);
        var alertId = (await Body(current)).GetProperty("alertId").GetInt32();
        using var resolved = await owner.PatchAsJsonAsync($"/api/LeakAlerts/{alertId}/status", new { status = 2 });
        using var response = await ApiScenario.Report(admin);
        var body = await Body(response);
        Assert.Equal(2025, body.GetProperty("year").GetInt32());
        Assert.Equal(6, body.GetProperty("month").GetInt32());
        Assert.Equal(1.5, body.GetProperty("totalConsumptionM3").GetDouble());
        Assert.Equal(.5, body.GetProperty("savedWaterM3").GetDouble());
        Assert.Equal(25, body.GetProperty("reductionPercent").GetDouble());
        Assert.Equal(1, body.GetProperty("leaksDetected").GetInt32());
        Assert.Equal(1, body.GetProperty("leaksResolved").GetInt32());
        Assert.Equal("Inspecionar pontos de consumo", body.GetProperty("mitigationPlan").GetString());
        var saved = await admin.GetFromJsonAsync<JsonElement>($"/api/WaterImpactReports/{body.GetProperty("id").GetInt32()}");
        Assert.Equal(body.GetRawText(), saved.GetRawText());
        await Complete(response);
    }

    [Operation("REPORT-03", "negative")]
    public async Task REPORT03_Failure()
    {
        using var owner = await App.User();
        using var admin = await App.Admin();
        using var response = await ApiScenario.Report(owner);
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/WaterImpactReports")).GetProperty("totalCount").GetInt32());
        await Complete(response);
    }

    [Operation("REPORT-04", "positive")]
    public async Task REPORT04_Success()
    {
        using var admin = await App.Admin();
        using var generated = await ApiScenario.Report(admin);
        var id = (await Body(generated)).GetProperty("id").GetInt32();
        using var response = await admin.DeleteAsync($"/api/WaterImpactReports/{id}");
        using var missing = await admin.GetAsync($"/api/WaterImpactReports/{id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Schemas.AssertValid("message-error", await Body(missing));
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/WaterImpactReports")).GetProperty("totalCount").GetInt32());
        await Complete(response);
    }

    [Operation("REPORT-04", "negative")]
    public async Task REPORT04_Failure()
    {
        using var owner = await App.User();
        using var admin = await App.Admin();
        using var generated = await ApiScenario.Report(admin);
        var id = (await Body(generated)).GetProperty("id").GetInt32();
        using var response = await owner.DeleteAsync($"/api/WaterImpactReports/{id}");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/WaterImpactReports/{id}")).StatusCode);
        await Complete(response);
    }

    [Theory]
    [InlineData(1999, 6)]
    [InlineData(2025, 13)]
    public async Task InvalidPeriod_LeavesNoReport(int year, int month)
    {
        using var admin = await App.Admin();
        using var response = await admin.PostAsJsonAsync("/api/WaterImpactReports/generate", new { year, month, mitigationPlan = "Inspecionar" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Schemas.AssertValid("validation-error", await Body(response));
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/WaterImpactReports")).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task MissingReport_UsesNotFoundContract(string method)
    {
        using var admin = await App.Admin();
        using var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api/WaterImpactReports/99999"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Schemas.AssertValid("message-error", await Body(response));
    }

    [Fact]
    public async Task IncreasedConsumption_ReportsNegativeDifference_WithoutClamping()
    {
        using var admin = await App.Admin();
        var sensor = await ApiScenario.Sensor(admin);
        using var previous = await ApiScenario.Reading(admin, sensor, 50, 1000, "2025-05-10T12:00:00Z");
        using var current = await ApiScenario.Reading(admin, sensor, 50, 1500);
        using var response = await ApiScenario.Report(admin);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Body(response);
        Schemas.AssertValid("report-response", body);
        Assert.Equal(-.5, body.GetProperty("savedWaterM3").GetDouble());
        Assert.Equal(-50, body.GetProperty("reductionPercent").GetDouble());
    }
}
