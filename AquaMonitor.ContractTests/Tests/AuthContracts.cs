namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class AuthContracts : ContractTest
{
    [Operation("AUTH-01", "positive")]
    public async Task AUTH01_Success()
    {
        using var client = App.Client();
        using var response = await client.PostAsJsonAsync("/api/Auth/register", new { name = "Pessoa", email = "person@example.test", password = ApiScenario.Password, role = "Admin" });
        var body = await Body(response);
        Assert.Equal("User", body.GetProperty("role").GetString());
        Assert.Equal("person@example.test", body.GetProperty("email").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));
        // The issued token must pass the real authentication middleware and retain User privileges.
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/WaterSensors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/LeakAlerts/stats")).StatusCode);
        await Complete(response);
    }

    [Operation("AUTH-01", "negative")]
    public async Task AUTH01_Failure()
    {
        using var client = await App.User();
        using var response = await client.PostAsJsonAsync("/api/Auth/register", new { name = "Outra pessoa", email = "owner@example.test", password = ApiScenario.Password });
        Assert.Equal("E-mail ja cadastrado.", (await Body(response)).GetProperty("message").GetString());
        await ApiScenario.Login(client, "owner@example.test");
        await Complete(response);
    }

    [Operation("AUTH-02", "positive")]
    public async Task AUTH02_Success()
    {
        using var client = await App.User();
        using var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = "owner@example.test", password = ApiScenario.Password });
        var body = await Body(response);
        Assert.Equal("User", body.GetProperty("role").GetString());
        Assert.Equal("owner@example.test", body.GetProperty("email").GetString());
        Assert.Equal(App.Clock.Now.UtcDateTime.AddHours(8), body.GetProperty("expiresAt").GetDateTime());
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/WaterSensors")).StatusCode);
        await Complete(response);
    }

    [Operation("AUTH-02", "negative")]
    public async Task AUTH02_Failure()
    {
        using var client = await App.User();
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = "owner@example.test", password = "incorrect-password" });
        var body = await Body(response);
        Assert.Equal("Credenciais invalidas.", body.GetProperty("message").GetString());
        Assert.False(body.TryGetProperty("token", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/WaterSensors")).StatusCode);
        await Complete(response);
    }

    [Theory]
    [InlineData("register", "{}")]
    [InlineData("login", "{}")]
    [InlineData("register", "{\"email\":")]
    public async Task InvalidAuthenticationRequest_UsesValidationContract(string operation, string json)
    {
        using var client = App.Client();
        using var response = await client.PostAsync($"/api/Auth/{operation}", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Body(response);
        Schemas.AssertValid("validation-error", body);
        Assert.False(body.TryGetProperty("token", out _));
    }
}
