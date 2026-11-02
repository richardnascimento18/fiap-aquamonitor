namespace AquaMonitor.Application.Abstractions;

public interface ITokenService
{
    string GenerateToken(int userId, string email, string name, string role);
    DateTime GetExpiration();
}

