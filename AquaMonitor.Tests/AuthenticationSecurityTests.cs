using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AquaMonitor.Tests;

public class AuthenticationSecurityTests
{
    [Theory]
    [InlineData("valid", HttpStatusCode.OK)]
    [InlineData("signature", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("no-exp", HttpStatusCode.Unauthorized)]
    [InlineData("issuer", HttpStatusCode.Unauthorized)]
    [InlineData("no-iss", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)]
    [InlineData("no-aud", HttpStatusCode.Unauthorized)]
    [InlineData("malformed", HttpStatusCode.Unauthorized)]
    [InlineData("algorithm", HttpStatusCode.Unauthorized)]
    [InlineData("unsigned", HttpStatusCode.Unauthorized)]
    [InlineData("no-sub", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-sub", HttpStatusCode.Unauthorized)]
    [InlineData("no-role", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-role", HttpStatusCode.Unauthorized)]
    [InlineData("future", HttpStatusCode.Unauthorized)]
    public async Task Tokens_EnforceRequiredValidation(string scenario, HttpStatusCode expected)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "1",
            ["role"] = "User",
            ["iss"] = "AquaMonitor.Tests",
            ["aud"] = "AquaMonitor.Tests.Client",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()
        };
        if (scenario.StartsWith("no-")) claims.Remove(scenario[3..]);
        switch (scenario)
        {
            case "expired": claims["exp"] = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds(); break;
            case "issuer": claims["iss"] = "untrusted"; break;
            case "audience": claims["aud"] = "untrusted"; break;
            case "invalid-sub": claims["sub"] = "not-an-id"; break;
            case "invalid-role": claims["role"] = "SuperAdmin"; break;
            case "future": claims["nbf"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(); break;
        }
        var token = Sign(claims, scenario == "algorithm" ? "HS512" : scenario == "unsigned" ? "none" : "HS256",
            scenario == "signature" ? "Wrong-test-key-wrong-test-key-wrong-key" : AquaMonitorWebApplicationFactory.TestKey);
        if (scenario == "malformed") token = "this.is.invalid";
        if (scenario == "unsigned") token = token[..(token.LastIndexOf('.') + 1)];
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/WaterSensors");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("/api/WaterSensors")]
    [InlineData("/api/WaterReadings")]
    [InlineData("/api/LeakAlerts")]
    [InlineData("/api/WaterImpactReports")]
    public async Task ProtectedEndpoints_RejectAnonymousRequests(string route)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Registration_CannotGrantAdministratorRole()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/register",
            new { name = "User", email = "user@test.com", password = "Test-password-123", role = "Admin" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User", json.GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/LeakAlerts/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/WaterSensors/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/WaterImpactReports/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/WaterImpactReports/generate",
            new { year = 2026, month = 10, mitigationPlan = "Plan" })).StatusCode);
    }

    private static string Sign(Dictionary<string, object> claims, string algorithm, string key)
    {
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var input = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = algorithm, typ = "JWT" })) + "." + Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return input + "." + Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(input)));
    }
}
