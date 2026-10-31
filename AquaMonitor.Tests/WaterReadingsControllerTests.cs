using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AquaMonitor.Tests;

public class WaterReadingsControllerTests : IClassFixture<AquaMonitorWebApplicationFactory>
{
    private readonly HttpClient _client;
    public WaterReadingsControllerTests(AquaMonitorWebApplicationFactory factory)
        => _client = factory.CreateClient();

    private async Task<(string token, int sensorId)> SetupAsync()
    {
        var email = $"u{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/Auth/register", new { name = "T", email, password = "Senha@123" });
        var r = await _client.PostAsJsonAsync("/api/Auth/login", new { email, password = "Senha@123" });
        var token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sr = await _client.PostAsJsonAsync("/api/WaterSensors",
            new { name = "S", location = "L", alertThresholdLitersPerHour = 200.0 });
        var sid = (await sr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        return (token, sid);
    }

    [Fact]
    public async Task GetReadings_ReturnsHttpStatusCode200()
    {
        await SetupAsync();
        var response = await _client.GetAsync("/api/WaterReadings");
        response.EnsureSuccessStatusCode(); // verifica status code 200
    }

    [Fact]
    public async Task CreateReading_ReturnsHttpStatusCode201()
    {
        var (_, sid) = await SetupAsync();
        var response = await _client.PostAsJsonAsync("/api/WaterReadings",
            new { sensorId = sid, litersConsumed = 50.0, flowRateLitersPerHour = 100.0 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateReading_ExceedThreshold_GeneratesAlert()
    {
        var (_, sid) = await SetupAsync();
        var response = await _client.PostAsJsonAsync("/api/WaterReadings",
            new { sensorId = sid, litersConsumed = 80.0, flowRateLitersPerHour = 500.0 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("alertGenerated").GetBoolean());
    }

    [Fact]
    public async Task GetSummary_ReturnsHttpStatusCode200()
    {
        await SetupAsync();
        var response = await _client.GetAsync("/api/WaterReadings/summary");
        response.EnsureSuccessStatusCode(); // verifica status code 200
    }
}
