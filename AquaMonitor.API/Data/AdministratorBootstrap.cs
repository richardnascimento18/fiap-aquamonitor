using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.API.Data;

public static class AdministratorBootstrap
{
    public static void Provision(IDbConnectionFactory factory, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled")) return;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Administrator bootstrap is permitted only in Development.");

        ProvisionAccount(factory, configuration, initializeSchema: false);
    }

    public static void ProvisionOffline(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Offline provisioning requires an explicit ConnectionStrings:DefaultConnection.");

        ProvisionAccount(new SqliteConnectionFactory(connectionString), configuration, initializeSchema: true);
    }

    private static void ProvisionAccount(IDbConnectionFactory factory, IConfiguration configuration, bool initializeSchema)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(password) || password.Length is < 14 or > 1024)
            throw new InvalidOperationException("BootstrapAdmin requires a valid Email and a securely supplied Password between 14 and 1024 characters.");

        if (initializeSchema) DatabaseInitializer.Initialize(factory);
        using var db = factory.Create();
        db.InTransaction(() =>
        {
            var parameters = new Dictionary<string, object?> { ["@e"] = email };
            var existing = db.Query("SELECT Role FROM Users WHERE Email=@e LIMIT 1", parameters);
            if (existing.Count > 0)
            {
                if (existing[0]["Role"]?.ToString() != "Admin")
                    throw new InvalidOperationException("BootstrapAdmin email already belongs to a non-administrator account.");
                return true;
            }
            parameters["@h"] = PasswordHelper.HashPassword(password);
            parameters["@c"] = DateTime.UtcNow.ToString("o");
            db.Execute("INSERT INTO Users (Name,Email,PasswordHash,Role,CreatedAt) VALUES ('Administrador',@e,@h,'Admin',@c)", parameters);
            return true;
        });
    }
}
