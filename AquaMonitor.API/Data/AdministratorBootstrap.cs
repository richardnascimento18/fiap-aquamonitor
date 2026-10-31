using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.API.Data;

public static class AdministratorBootstrap
{
    public static void Provision(IDbConnectionFactory factory, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled")) return;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Administrator bootstrap is permitted only in Development.");

        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(password) || password.Length < 14)
            throw new InvalidOperationException("BootstrapAdmin requires a valid Email and a securely supplied Password of at least 14 characters.");

        using var db = factory.Create();
        var parameters = new Dictionary<string, object?> { ["@e"] = email };
        var existing = db.Query("SELECT Role FROM Users WHERE Email=@e LIMIT 1", parameters);
        if (existing.Count > 0)
        {
            if (existing[0]["Role"]?.ToString() != "Admin")
                throw new InvalidOperationException("BootstrapAdmin email already belongs to a non-administrator account.");
            return;
        }
        parameters["@h"] = PasswordHelper.HashPassword(password);
        parameters["@c"] = DateTime.UtcNow.ToString("o");
        db.Execute("INSERT INTO Users (Name,Email,PasswordHash,Role,CreatedAt) VALUES ('Administrador',@e,@h,'Admin',@c)", parameters);
    }
}
