using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace AquaMonitor.API.Services;

public interface ITokenService
{
    string GenerateToken(int userId, string email, string name, string role);
    DateTime GetExpiration();
}

public sealed class TokenService(JwtSettings settings) : ITokenService
{
    public DateTime GetExpiration() => DateTime.UtcNow.AddHours(settings.ExpiresInHours);

    public string GenerateToken(int userId, string email, string name, string role)
    {
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: new[]
            {
                new Claim("sub", userId.ToString(CultureInfo.InvariantCulture)),
                new Claim("email", email), new Claim("name", name), new Claim("role", role),
                new Claim("jti", Guid.NewGuid().ToString())
            },
            notBefore: DateTime.UtcNow,
            expires: GetExpiration(),
            signingCredentials: new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
