using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AquaMonitor.Tests;

public class AuthControllerTests : IClassFixture<AquaMonitorWebApplicationFactory>
{
    private readonly HttpClient _client;
    public AuthControllerTests(AquaMonitorWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Register_ReturnsHttpStatusCode201()
    {
        var request = "/api/Auth/register";
        var response = await _client.PostAsJsonAsync(request,
            new { name = "Teste", email = $"u{Guid.NewGuid():N}@test.com", password = "Senha@123" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsHttpStatusCode200()
    {
        var email = $"u{Guid.NewGuid():N}@test.com"; var pwd = "Senha@123";
        await _client.PostAsJsonAsync("/api/Auth/register", new { name = "T", email, password = pwd });
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new { email, password = pwd });
        response.EnsureSuccessStatusCode(); // verifica status code 200
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login",
            new { email = "ghost@x.com", password = "Wrong@123" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var payload = new { name = "T", email = $"dup{Guid.NewGuid():N}@test.com", password = "Senha@123" };
        await _client.PostAsJsonAsync("/api/Auth/register", payload);
        var response = await _client.PostAsJsonAsync("/api/Auth/register", payload);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
