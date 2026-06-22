using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AquaMonitor.API.Services;

public interface ITokenService
{
    string GenerateToken(int userId, string email, string name, string role);
    ClaimsPrincipal? ValidateToken(string token);
    DateTime GetExpiration();
}

/// <summary>
/// Hand-rolled HS256 JWT using only System.Security.Cryptography (built-in .NET runtime).
/// No JwtBearer NuGet package required.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly byte[] _keyBytes;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiresHours;

    public TokenService(IConfiguration cfg)
    {
        var key = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing.");
        _keyBytes     = Encoding.UTF8.GetBytes(key);
        _issuer       = cfg["Jwt:Issuer"]   ?? "AquaMonitor.API";
        _audience     = cfg["Jwt:Audience"] ?? "AquaMonitor.Client";
        _expiresHours = int.Parse(cfg["Jwt:ExpiresInHours"] ?? "8");
    }

    public DateTime GetExpiration() => DateTime.UtcNow.AddHours(_expiresHours);

    public string GenerateToken(int userId, string email, string name, string role)
    {
        var header  = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub   = userId.ToString(),
            email,
            name,
            role,
            iss   = _issuer,
            aud   = _audience,
            iat   = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp   = DateTimeOffset.UtcNow.AddHours(_expiresHours).ToUnixTimeSeconds(),
            jti   = Guid.NewGuid().ToString()
        }));

        var signingInput = $"{header}.{payload}";
        var sig = Sign(Encoding.UTF8.GetBytes(signingInput));
        return $"{signingInput}.{Base64Url(sig)}";
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var signingInput = $"{parts[0]}.{parts[1]}";
            var expectedSig  = Sign(Encoding.UTF8.GetBytes(signingInput));
            var actualSig    = FromBase64Url(parts[2]);

            if (!CryptographicOperations.FixedTimeEquals(expectedSig, actualSig)) return null;

            var payloadJson = Encoding.UTF8.GetString(FromBase64Url(parts[1]));
            var payload     = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
            if (payload is null) return null;

            if (payload.TryGetValue("exp", out var expEl))
            {
                var exp = DateTimeOffset.FromUnixTimeSeconds(expEl.GetInt64());
                if (exp < DateTimeOffset.UtcNow) return null;
            }

            if (payload.TryGetValue("iss", out var issEl) && issEl.GetString() != _issuer) return null;
            if (payload.TryGetValue("aud", out var audEl) && audEl.GetString() != _audience) return null;

            var claims = new List<Claim>();
            if (payload.TryGetValue("sub",   out var sub)) claims.Add(new Claim(ClaimTypes.NameIdentifier, sub.GetString()!));
            if (payload.TryGetValue("email", out var em))  claims.Add(new Claim(ClaimTypes.Email,           em.GetString()!));
            if (payload.TryGetValue("name",  out var nm))  claims.Add(new Claim(ClaimTypes.Name,            nm.GetString()!));
            if (payload.TryGetValue("role",  out var rl))  claims.Add(new Claim(ClaimTypes.Role,            rl.GetString()!));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "JWT"));
        }
        catch { return null; }
    }

    private byte[] Sign(byte[] data)
    {
        using var hmac = new HMACSHA256(_keyBytes);
        return hmac.ComputeHash(data);
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        while (s.Length % 4 != 0) s += '=';
        return Convert.FromBase64String(s);
    }
}
