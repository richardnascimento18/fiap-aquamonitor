using AquaMonitor.Domain.Entities;

namespace AquaMonitor.Application.Abstractions;

public interface IUserRepository
{
    User? FindByEmail(string normalizedEmail);
    int Insert(string name, string normalizedEmail, string passwordHash, DateTime utcNow);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string stored);
}
