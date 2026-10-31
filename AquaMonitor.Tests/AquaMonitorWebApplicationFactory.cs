using AquaMonitor.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AquaMonitor.Tests;

public class AquaMonitorWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestKey = "Test-only-signing-key-6b95c86c-8ea1-460d-a828-6481bb104e62";
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"aquatest_{Guid.NewGuid():N}.db");
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Jwt:Key"] = TestKey,
        ["Jwt:Issuer"] = "AquaMonitor.Tests",
        ["Jwt:Audience"] = "AquaMonitor.Tests.Client",
        ["BootstrapAdmin:Enabled"] = "false",
        ["Cors:AllowedOrigins:0"] = "https://client.example.test"
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(DatabasePath));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) DeleteDatabase();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabase();
    }

    private void DeleteDatabase()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(DatabasePath + suffix);
    }
}
