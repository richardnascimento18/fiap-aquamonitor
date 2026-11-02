using AquaMonitor.Application.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class SecurityContracts : ContractTest
{
    public static IEnumerable<object[]> ProtectedOperations() => Inventory.Load().Operations
        .Where(op => !op.Id.StartsWith("AUTH", StringComparison.Ordinal)).Select(op => new object[] { op.Method, op.Route });

    [Theory]
    [MemberData(nameof(ProtectedOperations))]
    public async Task ProtectedOperation_WithoutJwt_HasEmptyChallenge(string method, string route)
    {
        using var client = App.Client();
        route = route.Replace("{id:int}", "1", StringComparison.Ordinal);
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route)
        {
            Content = method is "POST" or "PUT" or "PATCH" ? JsonContent.Create(new { }) : null
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrExpiredJwt_HasEmptyChallenge(bool expired)
    {
        using var client = await App.User();
        if (expired) App.Clock.Now = App.Clock.Now.AddHours(9);
        else client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid.jwt.signature");
        using var response = await client.GetAsync("/api/WaterSensors");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task UnexpectedFailure_UsesConfidentialProblemContract()
    {
        using var actor = await App.User();
        using var failing = App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWorkSessionFactory>();
            services.AddSingleton<IWorkSessionFactory>(new FailingSessions());
        }));
        using var client = failing.CreateClient();
        client.DefaultRequestHeaders.Authorization = actor.DefaultRequestHeaders.Authorization;
        using var response = await client.GetAsync("/api/WaterSensors");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await Body(response);
        Schemas.AssertValid("problem-error", body);
        Assert.Equal("Contate o suporte.", body.GetProperty("detail").GetString());
        foreach (var secret in new[] { "sensitive-password", "SELECT", "/private/", "StackTrace", "ConnectionString" })
            Assert.DoesNotContain(secret, body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FailingSessions : IWorkSessionFactory
    {
        public IWorkSession Open() => throw new InvalidOperationException("SELECT secret FROM /private/database; ConnectionString=sensitive-password");
    }
}
