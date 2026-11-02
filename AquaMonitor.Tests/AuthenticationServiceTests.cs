using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Authentication;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace AquaMonitor.Tests;

[Trait("Category", "Application")]
public class AuthenticationServiceTests
{
    [Fact]
    public void RegistrationAndLogin_PreserveSecurityAndUseInjectedTime()
    {
        using var database = new ServiceDatabase();
        var service = Create(database);
        var registration = service.Register(new RegisterVM { Name = "Test", Email = "CASE@test.com", Password = "Test-password-123" });
        Assert.Equal("case@test.com", registration.Email);
        Assert.Equal("User", registration.Role);
        Assert.Equal(database.Clock.GetUtcNow().UtcDateTime.AddHours(8), registration.ExpiresAt);
        var login = service.Login(new LoginVM { Email = "Case@test.com", Password = "Test-password-123" });
        Assert.Equal(registration.Email, login.Email);
        Assert.Equal(401, Assert.Throws<ApplicationFailure>(() => service.Login(new LoginVM { Email = "case@test.com", Password = "incorrect" })).StatusCode);
        Assert.Equal(409, Assert.Throws<ApplicationFailure>(() => service.Register(new RegisterVM { Name = "Test", Email = "case@test.com", Password = "Test-password-123" })).StatusCode);
        using var db = database.Connections.Create();
        Assert.StartsWith("v1$600000$", (string)Assert.Single(db.Query("SELECT PasswordHash FROM Users WHERE Email='case@test.com'"))["PasswordHash"]!);
    }

    [Fact]
    public async Task ConcurrentRegistration_PreservesUniqueEmailAndExpectedConflict()
    {
        using var database = new ServiceDatabase();
        var service = Create(database);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try { service.Register(new RegisterVM { Name = "Test", Email = "concurrent@test.com", Password = "Test-password-123" }); return 201; }
            catch (ApplicationFailure failure) { return failure.StatusCode; }
        })));
        Assert.Equal(new[] { 201, 409 }, attempts.Order().ToArray());
        using var db = database.Connections.Create();
        Assert.Single(db.Query("SELECT * FROM Users WHERE Email='concurrent@test.com'"));
    }

    private static AuthenticationService Create(ServiceDatabase database)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = AquaMonitorWebApplicationFactory.TestKey,
            ["Jwt:Issuer"] = "Tests",
            ["Jwt:Audience"] = "Tests",
            ["Jwt:ExpiresInHours"] = "8"
        }).Build();
        return new(database.Sessions, new PasswordHasher(), new TokenService(new JwtSettings(configuration), database.Clock), database.Clock);
    }
}
