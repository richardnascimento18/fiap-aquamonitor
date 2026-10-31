using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AquaMonitor.API.Data;
using Microsoft.Extensions.Configuration;

namespace AquaMonitor.Tests;

public class AdministratorProvisioningTests
{
    [Fact]
    public async Task OfflineProvisioning_CreatesWorkingAdmin_AndRepeatedExecutionPreservesPassword()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        var configuration = Settings(factory.DatabasePath);
        AdministratorBootstrap.ProvisionOffline(configuration);
        string originalHash;
        using (var db = new SqliteConnection(factory.DatabasePath))
        {
            var user = Assert.Single(db.Query("SELECT * FROM Users"));
            Assert.Equal("Admin", user["Role"]);
            originalHash = Assert.IsType<string>(user["PasswordHash"]);
            Assert.True(PasswordHelper.VerifyPassword(configuration["BootstrapAdmin:Password"]!, originalHash));
        }

        configuration["BootstrapAdmin:Password"] = "Different-test-only-password-139";
        AdministratorBootstrap.ProvisionOffline(configuration);
        using (var db = new SqliteConnection(factory.DatabasePath))
            Assert.Equal(originalHash, Assert.Single(db.Query("SELECT PasswordHash FROM Users"))["PasswordHash"]);

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = "operator@example.test", password = "Test-only-operator-password-138" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void OfflineProvisioning_RequiresExplicitDatabaseConfiguration(string? connectionString)
    {
        var configuration = Settings(connectionString);
        var exception = Assert.Throws<InvalidOperationException>(() => AdministratorBootstrap.ProvisionOffline(configuration));
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message);
    }

    [Theory]
    [InlineData(null, "Test-only-operator-password-138")]
    [InlineData("invalid", "Test-only-operator-password-138")]
    [InlineData("operator@example.test", null)]
    [InlineData("operator@example.test", "short")]
    public void OfflineProvisioning_RejectsInvalidCredentialsBeforeCreatingDatabase(string? email, string? password)
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        var configuration = Settings(factory.DatabasePath);
        configuration["BootstrapAdmin:Email"] = email;
        configuration["BootstrapAdmin:Password"] = password;
        Assert.Throws<InvalidOperationException>(() => AdministratorBootstrap.ProvisionOffline(configuration));
        Assert.False(File.Exists(factory.DatabasePath));
    }

    [Fact]
    public async Task OfflineProvisioning_RefusesToPromoteExistingOrdinaryAccount()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/Auth/register", new { name = "User", email = "operator@example.test", password = "Test-only-user-password-140" })).EnsureSuccessStatusCode();
        Assert.Throws<InvalidOperationException>(() => AdministratorBootstrap.ProvisionOffline(Settings(factory.DatabasePath)));
        using var db = new SqliteConnection(factory.DatabasePath);
        Assert.Equal("User", Assert.Single(db.Query("SELECT Role FROM Users"))["Role"]);
    }

    [Fact]
    public async Task ConcurrentOfflineProvisioning_IsIdempotent()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        var configuration = Settings(factory.DatabasePath);
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => AdministratorBootstrap.ProvisionOffline(configuration))));
        using var db = new SqliteConnection(factory.DatabasePath);
        Assert.Equal("Admin", Assert.Single(db.Query("SELECT Role FROM Users"))["Role"]);
    }

    [Fact]
    public void FailedProvisioning_LeavesNoAccount_AndCanBeRetried()
    {
        using var factory = new AquaMonitorWebApplicationFactory();
        using var client = factory.CreateClient();
        using var db = new SqliteConnection(factory.DatabasePath);
        db.Execute("CREATE TRIGGER fail_admin BEFORE INSERT ON Users BEGIN SELECT RAISE(ABORT, 'test-only failure'); END");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => AdministratorBootstrap.ProvisionOffline(Settings(factory.DatabasePath)));
        Assert.Empty(db.Query("SELECT * FROM Users"));
        db.Execute("DROP TRIGGER fail_admin");
        AdministratorBootstrap.ProvisionOffline(Settings(factory.DatabasePath));
        Assert.Equal("Admin", Assert.Single(db.Query("SELECT Role FROM Users"))["Role"]);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task Command_ExitsWithoutStartingHttp_AndNeverUsesDefaultDatabase(bool configureDatabase, int expectedExitCode)
    {
        var directory = Directory.CreateTempSubdirectory("aqua-provision-");
        try
        {
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            var start = new ProcessStartInfo(host)
            {
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            start.ArgumentList.Add("--provision-admin");
            foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("BootstrapAdmin__", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("BootstrapAdmin:", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(key);
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["BootstrapAdmin__Email"] = "operator@example.test";
            start.Environment["BootstrapAdmin__Password"] = "Test-only-operator-password-138";
            var databasePath = Path.Combine(directory.FullName, "explicit.db");
            if (configureDatabase) start.Environment["ConnectionStrings__DefaultConnection"] = databasePath;

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var stdout = await output;
            var stderr = await errors;
            Assert.True(process.ExitCode == expectedExitCode, $"Unexpected exit {process.ExitCode}: {stderr}");
            Assert.DoesNotContain("Now listening", stdout + stderr);
            Assert.DoesNotContain(start.Environment["BootstrapAdmin__Password"]!, stdout + stderr);
            Assert.False(File.Exists(Path.Combine(directory.FullName, "aquamonitor.db")));
            if (configureDatabase)
            {
                Assert.Contains("HTTP server was not started", stdout);
                using var db = new SqliteConnection(databasePath);
                Assert.Equal("Admin", Assert.Single(db.Query("SELECT Role FROM Users"))["Role"]);
            }
            else
            {
                Assert.Contains("ConnectionStrings:DefaultConnection", stderr);
                Assert.Empty(directory.EnumerateFiles());
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private static IConfigurationRoot Settings(string? connectionString)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["BootstrapAdmin:Email"] = "operator@example.test",
            ["BootstrapAdmin:Password"] = "Test-only-operator-password-138"
        }).Build();
}
