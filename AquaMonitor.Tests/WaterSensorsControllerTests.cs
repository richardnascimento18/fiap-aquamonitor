using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AquaMonitor.Tests;

public class WaterSensorsControllerTests : IClassFixture<AquaMonitorWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly AquaMonitorWebApplicationFactory _factory;
    public WaterSensorsControllerTests(AquaMonitorWebApplicationFactory factory)
    { _factory = factory; _client = factory.CreateClient(); }

    private async Task<string> GetTokenAsync()
    {
        var email = $"u{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/Auth/register", new{name="T",email,password="Senha@123"});
        var r = await _client.PostAsJsonAsync("/api/Auth/login", new{email,password="Senha@123"});
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task GetSensors_ReturnsHttpStatusCode200()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());
        var response = await _client.GetAsync("/api/WaterSensors");
        response.EnsureSuccessStatusCode(); // verifica status code 200
    }

    [Fact]
    public async Task CreateSensor_ReturnsHttpStatusCode201()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());
        var response = await _client.PostAsJsonAsync("/api/WaterSensors",
            new{name="Sensor A",location="Piso 1",alertThresholdLitersPerHour=300.0});
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetSensorById_InvalidId_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/WaterSensors/999999")).StatusCode);
    }

    [Fact]
    public async Task GetSensors_NoToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/WaterSensors")).StatusCode);
    }
}
