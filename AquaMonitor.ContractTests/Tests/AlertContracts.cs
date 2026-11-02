namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class AlertContracts : ContractTest
{
    [Operation("ALERT-01", "positive")]
    public async Task ALERT01_Success()
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var sensor = await ApiScenario.Sensor(owner);
        var id = await ApiScenario.Alert(owner, sensor);
        var resolved = await ApiScenario.Alert(owner, sensor);
        using var update = await owner.PatchAsJsonAsync($"/api/LeakAlerts/{resolved}/status", new { status = 2 });
        await ApiScenario.Alert(other, await ApiScenario.Sensor(other));
        using var response = await owner.GetAsync($"/api/LeakAlerts?sensorId={sensor}&status=0&severity=4&pageSize=1");
        var body = await Body(response);
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        var row = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal(id, row.GetProperty("id").GetInt32());
        Assert.Equal("Critical", row.GetProperty("severity").GetString());
        Assert.Equal("Open", row.GetProperty("status").GetString());
        await Complete(response);
    }

    [Operation("ALERT-01", "negative")]
    public async Task ALERT01_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/LeakAlerts?severity=5");
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("Severity", out _));
        await Complete(response);
    }

    [Operation("ALERT-02", "positive")]
    public async Task ALERT02_Success()
    {
        using var client = await App.User();
        var sensor = await ApiScenario.Sensor(client);
        var id = await ApiScenario.Alert(client, sensor);
        using var response = await client.GetAsync($"/api/LeakAlerts/{id}");
        var body = await Body(response);
        Assert.Equal(id, body.GetProperty("id").GetInt32());
        Assert.Equal(sensor, body.GetProperty("sensorId").GetInt32());
        Assert.Equal("Critical", body.GetProperty("severity").GetString());
        Assert.Equal("Open", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("resolvedAt").ValueKind);
        Assert.Contains("Reservatório", body.GetProperty("message").GetString());
        await Complete(response);
    }

    [Operation("ALERT-02", "negative")]
    public async Task ALERT02_Failure()
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var id = await ApiScenario.Alert(owner, await ApiScenario.Sensor(owner));
        using var response = await other.GetAsync($"/api/LeakAlerts/{id}");
        Assert.Equal("Alerta nao encontrado.", (await Body(response)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/LeakAlerts/{id}")).StatusCode);
        await Complete(response);
    }

    [Operation("ALERT-03", "positive")]
    public async Task ALERT03_Success()
    {
        using var client = await App.User();
        var id = await ApiScenario.Alert(client, await ApiScenario.Sensor(client));
        using var response = await client.PatchAsJsonAsync($"/api/LeakAlerts/{id}/status", new { status = 2 });
        var body = await Body(response);
        Assert.Equal("Resolved", body.GetProperty("status").GetString());
        Assert.Equal(App.Clock.Now.UtcDateTime, body.GetProperty("resolvedAt").GetDateTime());
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{id}");
        Schemas.AssertValid("alert-response", saved);
        Assert.Equal(body.GetRawText(), saved.GetRawText());
        await Complete(response);
    }

    [Operation("ALERT-03", "negative")]
    public async Task ALERT03_Failure()
    {
        using var client = await App.User();
        var id = await ApiScenario.Alert(client, await ApiScenario.Sensor(client));
        using var response = await client.PatchAsJsonAsync($"/api/LeakAlerts/{id}/status", new { status = 3 });
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("Status", out _));
        Assert.Equal("Open", (await client.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{id}")).GetProperty("status").GetString());
        await Complete(response);
    }

    [Operation("ALERT-04", "positive")]
    public async Task ALERT04_Success()
    {
        using var owner = await App.User();
        using var admin = await App.Admin();
        var sensor = await ApiScenario.Sensor(owner);
        var resolved = await ApiScenario.Alert(owner, sensor);
        var acknowledged = await ApiScenario.Alert(owner, sensor);
        await ApiScenario.Alert(owner, sensor);
        using var resolve = await owner.PatchAsJsonAsync($"/api/LeakAlerts/{resolved}/status", new { status = 2 });
        using var acknowledge = await owner.PatchAsJsonAsync($"/api/LeakAlerts/{acknowledged}/status", new { status = 1 });
        using var response = await admin.GetAsync("/api/LeakAlerts/stats");
        var body = await Body(response);
        Assert.Equal(3, body.GetProperty("totalAlerts").GetInt32());
        foreach (var property in new[] { "openAlerts", "acknowledgedAlerts", "resolvedAlerts" }) Assert.Equal(1, body.GetProperty(property).GetInt32());
        var severity = Assert.Single(body.GetProperty("bySeverity").EnumerateArray());
        Assert.Equal("Critical", severity.GetProperty("severity").GetString());
        Assert.Equal(3, severity.GetProperty("count").GetInt32());
        await Complete(response);
    }

    [Operation("ALERT-04", "negative")]
    public async Task ALERT04_Failure()
    {
        using var client = await App.User();
        using var response = await client.GetAsync("/api/LeakAlerts/stats");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/LeakAlerts")).StatusCode);
        await Complete(response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingOrForeignAlert_CannotChangeStatus(bool missing)
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var id = await ApiScenario.Alert(owner, await ApiScenario.Sensor(owner));
        var requestedId = missing ? 99999 : id;
        using var response = await other.PatchAsJsonAsync($"/api/LeakAlerts/{requestedId}/status", new { status = 2 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Schemas.AssertValid("message-error", await Body(response));
        Assert.Equal("Open", (await owner.GetFromJsonAsync<JsonElement>($"/api/LeakAlerts/{id}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReopeningClearsResolution_AndAcknowledgementRemainsUnresolved()
    {
        using var client = await App.User();
        var id = await ApiScenario.Alert(client, await ApiScenario.Sensor(client));
        foreach (var status in new[] { 2, 0, 1, 2 })
        {
            App.Clock.Now = App.Clock.Now.AddMinutes(1);
            using var response = await client.PatchAsJsonAsync($"/api/LeakAlerts/{id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await Body(response);
            Schemas.AssertValid("alert-response", body);
            Assert.Equal(new[] { "Open", "Acknowledged", "Resolved" }[status], body.GetProperty("status").GetString());
            if (status == 2) Assert.Equal(App.Clock.Now.UtcDateTime, body.GetProperty("resolvedAt").GetDateTime());
            else Assert.Equal(JsonValueKind.Null, body.GetProperty("resolvedAt").ValueKind);
        }
    }
}
