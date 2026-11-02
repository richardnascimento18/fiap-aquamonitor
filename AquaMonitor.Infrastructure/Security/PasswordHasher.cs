using AquaMonitor.Application.Abstractions;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => PasswordHelper.HashPassword(password);
    public bool Verify(string password, string stored) => PasswordHelper.VerifyPassword(password, stored);
}
