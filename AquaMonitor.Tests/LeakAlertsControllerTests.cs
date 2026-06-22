using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AquaMonitor.Tests;

public class LeakAlertsControllerTests : IClassFixture<AquaMonitorWebApplicationFactory>
{
    private readonly HttpClient _client;
    public LeakAlertsControllerTests(AquaMonitorWebApplicationFactory factory)
        => _client = factory.CreateClient();

    private async Task AuthAsync()
    {
        var email = $"u{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/Auth/register", new{name="T",email,password="Senha@123"});
        var r = await _client.PostAsJsonAsync("/api/Auth/login", new{email,password="Senha@123"});
        var token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetAlerts_ReturnsHttpStatusCode200()
    {
        await AuthAsync();
        var response = await _client.GetAsync("/api/LeakAlerts");
        response.EnsureSuccessStatusCode(); // verifica status code 200
    }

    [Fact]
    public async Task GetAlertById_InvalidId_Returns404()
    {
        await AuthAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/LeakAlerts/999999")).StatusCode);
    }
}
