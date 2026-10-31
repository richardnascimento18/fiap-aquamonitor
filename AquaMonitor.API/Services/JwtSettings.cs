using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AquaMonitor.API.Services;

public sealed class JwtSettings
{
    public SymmetricSecurityKey SigningKey { get; }
    public string Issuer { get; }
    public string Audience { get; }
    public int ExpiresInHours { get; }

    public JwtSettings(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32 || key.Distinct().Count() < 12 ||
            key.Contains("AquaMonitor_SuperSecretKey", StringComparison.Ordinal) ||
            key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Jwt:Key must be a new, securely generated secret of at least 32 UTF-8 bytes.");
        Issuer = configuration["Jwt:Issuer"] ?? "";
        Audience = configuration["Jwt:Audience"] ?? "";
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required.");
        if (!int.TryParse(configuration["Jwt:ExpiresInHours"], out var hours) || hours is < 1 or > 24)
            throw new InvalidOperationException("Jwt:ExpiresInHours must be between 1 and 24.");
        ExpiresInHours = hours;
        SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
