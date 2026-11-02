using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AquaMonitor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AquaMonitor.TestSupport;

/// <summary>One host/database per test or scenario. Authentication middleware is unchanged.</summary>
public sealed class ApiScenario : WebApplicationFactory<Program>
{
    public const string Password = "Test-only-password-123!";
    public const string AdminEmail = "admin@example.test";
    public const string SigningKey = "Test-only-signing-key-c4fdbe05889444dda02423e16c65880b";
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"aquamonitor-verification-{Guid.NewGuid():N}.db");
    public ScenarioClock Clock { get; } = new();
    private readonly List<HttpClient> _clients = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = SigningKey,
            ["Jwt:Issuer"] = "AquaMonitor.Verification",
            ["Jwt:Audience"] = "AquaMonitor.Verification.Client",
            ["Jwt:ExpiresInHours"] = "8",
            ["BootstrapAdmin:Enabled"] = "false"
        }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(DatabasePath));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            // IdentityModel uses wall time internally. Align only lifetime evaluation with
            // the injected clock; signature, issuer, audience, roles and middleware remain real.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, parameters) =>
                    expires.HasValue && (!notBefore.HasValue || notBefore <= expires) &&
                    (!notBefore.HasValue || notBefore <= Clock.GetUtcNow().UtcDateTime + parameters.ClockSkew) &&
                    expires >= Clock.GetUtcNow().UtcDateTime - parameters.ClockSkew);
        });
    }

    public HttpClient Client()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _clients.Add(client);
        return client;
    }

    public async Task<HttpClient> User(string email = "owner@example.test")
    {
        var client = Client();
        using var registration = await client.PostAsJsonAsync("/api/Auth/register", new { name = "Pessoa de teste", email, password = Password });
        if (registration.StatusCode != HttpStatusCode.Created) throw new InvalidOperationException("Test user registration failed.");
        await Login(client, email);
        return client;
    }

    public async Task<HttpClient> Admin()
    {
        var client = Client(); // Initialize the actual host and its private schema first.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = DatabasePath,
            ["BootstrapAdmin:Email"] = AdminEmail,
            ["BootstrapAdmin:Password"] = Password
        }).Build();
        AdministratorBootstrap.ProvisionOffline(configuration);
        await Login(client, AdminEmail);
        return client;
    }

    public static async Task Login(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = Password });
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Test actor login failed.");
        var body = await Json(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    public static async Task<int> Sensor(HttpClient client, double threshold = 100)
    {
        using var response = await client.PostAsJsonAsync("/api/WaterSensors", new { name = "Reservatório", location = "Campus", alertThresholdLitersPerHour = threshold });
        if (response.StatusCode != HttpStatusCode.Created) throw new InvalidOperationException("Test sensor creation failed.");
        return (await Json(response)).GetProperty("id").GetInt32();
    }

    public static Task<HttpResponseMessage> Reading(HttpClient client, int sensorId, double flow = 50, double liters = 100, string date = "2025-06-10T12:00:00Z")
        => client.PostAsJsonAsync("/api/WaterReadings", new { sensorId, litersConsumed = liters, flowRateLitersPerHour = flow, recordedAt = date });

    public static async Task<int> Alert(HttpClient client, int sensorId)
    {
        using var response = await Reading(client, sensorId, 300);
        if (response.StatusCode != HttpStatusCode.Created) throw new InvalidOperationException("Test anomalous reading creation failed.");
        return (await Json(response)).GetProperty("alertId").GetInt32();
    }

    public static Task<HttpResponseMessage> Report(HttpClient client, int month = 6)
        => client.PostAsJsonAsync("/api/WaterImpactReports/generate", new { year = 2025, month, mitigationPlan = "Inspecionar pontos de consumo" });

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var client in _clients) client.Dispose();
        base.Dispose(disposing);
        if (disposing) DeleteDatabase();
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var client in _clients) client.Dispose();
        await base.DisposeAsync();
        DeleteDatabase();
    }

    private void DeleteDatabase()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(DatabasePath + suffix);
    }
}

public sealed class ScenarioClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
