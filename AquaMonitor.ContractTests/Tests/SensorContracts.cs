namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class SensorContracts : ContractTest
{
    [Operation("SENSOR-01", "positive")]
    public async Task SENSOR01_Success()
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var id = await ApiScenario.Sensor(owner);
        await ApiScenario.Sensor(owner);
        await ApiScenario.Sensor(other);
        using var response = await owner.GetAsync("/api/WaterSensors?page=1&pageSize=1");
        var body = await Body(response);
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
        Assert.True(body.GetProperty("hasNextPage").GetBoolean());
        Assert.False(body.GetProperty("hasPreviousPage").GetBoolean());
        Assert.Single(body.GetProperty("data").EnumerateArray());
        var page2 = await owner.GetFromJsonAsync<JsonElement>("/api/WaterSensors?page=2&pageSize=1");
        Schemas.AssertValid("paged-sensors", page2);
        Assert.True(page2.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(page2.GetProperty("hasNextPage").GetBoolean());
        Assert.Contains(id, body.GetProperty("data").EnumerateArray().Concat(page2.GetProperty("data").EnumerateArray()).Select(row => row.GetProperty("id").GetInt32()));
        await Complete(response);
    }

    [Operation("SENSOR-01", "negative")]
    public async Task SENSOR01_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/WaterSensors?page=0");
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("page", out _));
        await Complete(response);
    }

    [Operation("SENSOR-02", "positive")]
    public async Task SENSOR02_Success()
    {
        using var client = await App.User();
        var id = await ApiScenario.Sensor(client);
        using var response = await client.GetAsync($"/api/WaterSensors/{id}");
        var body = await Body(response);
        Assert.Equal(id, body.GetProperty("id").GetInt32());
        Assert.Equal("Reservatório", body.GetProperty("name").GetString());
        Assert.Equal(100, body.GetProperty("alertThresholdLitersPerHour").GetDouble());
        Assert.Equal(App.Clock.Now.UtcDateTime, body.GetProperty("installedAt").GetDateTime());
        await Complete(response);
    }

    [Operation("SENSOR-02", "negative")]
    public async Task SENSOR02_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/WaterSensors/99999");
        Assert.Equal("Sensor nao encontrado.", (await Body(response)).GetProperty("message").GetString());
        await Complete(response);
    }

    [Operation("SENSOR-03", "positive")]
    public async Task SENSOR03_Success()
    {
        using var client = await App.User();
        using var response = await client.PostAsJsonAsync("/api/WaterSensors", new { name = "Entrada", location = "Campus", alertThresholdLitersPerHour = 100 });
        var body = await Body(response);
        Assert.True(body.GetProperty("isActive").GetBoolean());
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{body.GetProperty("id").GetInt32()}");
        Schemas.AssertValid("sensor-response", saved);
        Assert.Equal("Entrada", saved.GetProperty("name").GetString());
        Assert.Equal(body.GetProperty("userId").GetInt32(), saved.GetProperty("userId").GetInt32());
        await Complete(response);
    }

    [Operation("SENSOR-03", "negative")]
    public async Task SENSOR03_Failure()
    {
        using var client = await App.User();
        using var response = await client.PostAsJsonAsync("/api/WaterSensors", new { name = "Entrada", location = "Campus", alertThresholdLitersPerHour = -1 });
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("AlertThresholdLitersPerHour", out _));
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/WaterSensors")).GetProperty("totalCount").GetInt32());
        await Complete(response);
    }

    [Operation("SENSOR-04", "positive")]
    public async Task SENSOR04_Success()
    {
        using var client = await App.User();
        var id = await ApiScenario.Sensor(client);
        using var response = await client.PutAsJsonAsync($"/api/WaterSensors/{id}", new { name = "Atualizado", isActive = false, alertThresholdLitersPerHour = 200 });
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{id}");
        Schemas.AssertValid("sensor-response", saved);
        Assert.Equal("Atualizado", saved.GetProperty("name").GetString());
        Assert.Equal("Campus", saved.GetProperty("location").GetString());
        Assert.False(saved.GetProperty("isActive").GetBoolean());
        Assert.Equal(200, saved.GetProperty("alertThresholdLitersPerHour").GetDouble());
        Assert.Equal(saved.GetRawText(), (await Body(response)).GetRawText());
        await Complete(response);
    }

    [Operation("SENSOR-04", "negative")]
    public async Task SENSOR04_Failure()
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var id = await ApiScenario.Sensor(owner);
        using var response = await other.PutAsJsonAsync($"/api/WaterSensors/{id}", new { name = "Intruso" });
        Assert.Equal("Sensor nao encontrado.", (await Body(response)).GetProperty("message").GetString());
        Assert.Equal("Reservatório", (await owner.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{id}")).GetProperty("name").GetString());
        await Complete(response);
    }

    [Operation("SENSOR-05", "positive")]
    public async Task SENSOR05_Success()
    {
        using var owner = await App.User();
        using var admin = await App.Admin();
        var id = await ApiScenario.Sensor(owner);
        using var response = await admin.DeleteAsync($"/api/WaterSensors/{id}");
        using var missing = await owner.GetAsync($"/api/WaterSensors/{id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Schemas.AssertValid("message-error", await Body(missing));
        Assert.Equal(0, (await owner.GetFromJsonAsync<JsonElement>("/api/WaterSensors")).GetProperty("totalCount").GetInt32());
        await Complete(response);
    }

    [Operation("SENSOR-05", "negative")]
    public async Task SENSOR05_Failure()
    {
        using var owner = await App.User();
        var id = await ApiScenario.Sensor(owner);
        using var response = await owner.DeleteAsync($"/api/WaterSensors/{id}");
        Assert.Equal(id, (await owner.GetFromJsonAsync<JsonElement>($"/api/WaterSensors/{id}")).GetProperty("id").GetInt32());
        await Complete(response);
    }

    [Fact]
    public async Task DependentHistory_PreventsDeletion_AndPreservesReadingAndAlert()
    {
        using var owner = await App.User();
        using var admin = await App.Admin();
        var id = await ApiScenario.Sensor(owner);
        await ApiScenario.Alert(owner, id);
        using var response = await admin.DeleteAsync($"/api/WaterSensors/{id}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Schemas.AssertValid("problem-error", await Body(response));
        Assert.Equal(1, (await owner.GetFromJsonAsync<JsonElement>("/api/WaterReadings")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await owner.GetFromJsonAsync<JsonElement>("/api/LeakAlerts")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/WaterSensors/{id}")).StatusCode);
    }
}
