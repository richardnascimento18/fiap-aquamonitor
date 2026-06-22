using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using AquaMonitor.API.Data;

namespace AquaMonitor.Tests;

public class AquaMonitorWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real SQLite factory with in-memory temp file per test run
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbConnectionFactory));
            if (descriptor != null) services.Remove(descriptor);

            var tmpDb = Path.Combine(Path.GetTempPath(), $"aquatest_{Guid.NewGuid():N}.db");
            services.AddSingleton<IDbConnectionFactory>(_ =>
            {
                var factory = new SqliteConnectionFactory(tmpDb);
                DatabaseInitializer.Initialize(factory);
                return factory;
            });
        });
        builder.UseEnvironment("Development");
    }
}
