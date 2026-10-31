using System.Security.Cryptography;

namespace AquaMonitor.API.Data;

/// <summary>
/// PBKDF2-SHA256 password hashing — zero external dependencies.
/// Format: "v1$iterations$base64salt$base64hash"
/// </summary>
public static class PasswordHelper
{
    private const int Iterations = 600_000;
    private const int SaltBytes   = 16;
    private const int HashBytes    = 32;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Pbkdf2(password, salt);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        try
        {
            if (string.IsNullOrEmpty(stored) || stored.Length > 256) return false;
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "v1") return false;
            if (!int.TryParse(parts[1], out var iterations) || iterations < 10_000 || iterations > 2_000_000) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltBytes || expected.Length != HashBytes) return false;
            var actual = Pbkdf2(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations = Iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
}
