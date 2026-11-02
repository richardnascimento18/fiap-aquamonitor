using AquaMonitor.Application.Abstractions;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace AquaMonitor.Infrastructure.Security;

public sealed class TokenService(JwtSettings settings, TimeProvider time) : ITokenService
{
    public DateTime GetExpiration() => time.GetUtcNow().UtcDateTime.AddHours(settings.ExpiresInHours);

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
            notBefore: time.GetUtcNow().UtcDateTime,
            expires: GetExpiration(),
            signingCredentials: new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
