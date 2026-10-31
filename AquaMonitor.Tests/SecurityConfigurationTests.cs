using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AquaMonitor.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace AquaMonitor.Tests;

public class SecurityConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("AquaMonitor_SuperSecretKey_2024_ChangeInProduction!")]
    public void InvalidSigningKey_FailsAtStartup(string? key)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        factory.Settings["Jwt:Key"] = key;
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:Key", exception.Message);
        if (!string.IsNullOrEmpty(key)) Assert.DoesNotContain(key, exception.Message);
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:ExpiresInHours", "invalid")]
    [InlineData("Jwt:ExpiresInHours", "0")]
    [InlineData("Jwt:ExpiresInHours", "25")]
    public void InvalidTokenSettings_FailAtStartup(string setting, string value)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        factory.Settings[setting] = value;
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Initialization_DoesNotSeedAccounts()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        using var db = new SqliteConnection(factory.DatabasePath);
        Assert.Empty(db.Query("SELECT * FROM Users"));
    }

    [Fact]
    public void Bootstrap_RequiresExplicitDevelopmentOptInAndCredentials()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var connections = new SqliteConnectionFactory(factory.DatabasePath);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Enabled"] = "true"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => AdministratorBootstrap.Provision(connections, config, new TestEnvironment("Production")));
        Assert.Throws<InvalidOperationException>(() => AdministratorBootstrap.Provision(connections, config, new TestEnvironment("Development")));
        using var db = connections.Create();
        Assert.Empty(db.Query("SELECT * FROM Users"));
    }

    [Fact]
    public async Task Bootstrap_IsIdempotent_AndAdministratorCanAuthenticate()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var connections = new SqliteConnectionFactory(factory.DatabasePath);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:Email"] = "bootstrap@test.com",
            ["BootstrapAdmin:Password"] = "Test-only-admin-password-917"
        }).Build();
        AdministratorBootstrap.Provision(connections, settings, new TestEnvironment("Development"));
        AdministratorBootstrap.Provision(connections, settings, new TestEnvironment("Development"));
        using (var db = connections.Create())
        {
            var user = Assert.Single(db.Query("SELECT * FROM Users"));
            Assert.Equal("Admin", user["Role"]);
            Assert.NotEqual(settings["BootstrapAdmin:Password"], user["PasswordHash"]);
        }
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = "bootstrap@test.com", password = settings["BootstrapAdmin:Password"] });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Admin", json.GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/LeakAlerts/stats")).StatusCode);
    }

    [Fact]
    public async Task Bootstrap_NeverPromotesExistingOrdinaryUser()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/register", new { name = "User", email = "existing@test.com", password = "Test-password-123" });
        response.EnsureSuccessStatusCode();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Enabled"] = "true",
            ["BootstrapAdmin:Email"] = "existing@test.com",
            ["BootstrapAdmin:Password"] = "Test-only-admin-password-917"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => AdministratorBootstrap.Provision(new SqliteConnectionFactory(factory.DatabasePath), settings, new TestEnvironment("Development")));
        using var db = new SqliteConnection(factory.DatabasePath);
        Assert.Equal("User", Assert.Single(db.Query("SELECT Role FROM Users"))["Role"]);
    }

    [Fact]
    public void Passwords_UseRandomSaltAndSupportExistingHashes()
    {
        const string password = "Test-only-password-127";
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        var legacy = $"v1$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        Assert.True(PasswordHelper.VerifyPassword(password, legacy));
        Assert.False(PasswordHelper.VerifyPassword("wrong", legacy));
        var current = PasswordHelper.HashPassword(password);
        Assert.True(PasswordHelper.VerifyPassword(password, current));
        Assert.NotEqual(current, PasswordHelper.HashPassword(password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1$999999999$AAAA$AAAA")]
    [InlineData("v1$100000$invalid$invalid")]
    [InlineData("v1$100000$AAAA$AAAA")]
    [InlineData("v2$100000$AAAA$AAAA")]
    public void MalformedPasswordHashes_AreRejected(string hash)
        => Assert.False(PasswordHelper.VerifyPassword("Test-password-123", hash));

    private sealed class TestEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "AquaMonitor.API";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
