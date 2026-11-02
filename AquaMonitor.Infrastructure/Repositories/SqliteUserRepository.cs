using AquaMonitor.Application.Abstractions;
using AquaMonitor.Domain.Entities;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Repositories;

internal sealed class SqliteUserRepository(SqliteConnection db) : IUserRepository
{
    public User? FindByEmail(string normalizedEmail)
    {
        var rows = db.Query("SELECT * FROM Users WHERE Email=@e LIMIT 1", new Dictionary<string, object?> { ["@e"] = normalizedEmail });
        if (rows.Count == 0) return null;
        var r = rows[0];
        return new User { Id = (int)(long)r["Id"]!, Name = (string)r["Name"]!, Email = (string)r["Email"]!, PasswordHash = (string)r["PasswordHash"]!, Role = (string)r["Role"]!, CreatedAt = DbValues.Timestamp(r["CreatedAt"]) };
    }
    public int Insert(string name, string normalizedEmail, string passwordHash, DateTime utcNow)
    {
        db.Execute("INSERT INTO Users (Name,Email,PasswordHash,Role,CreatedAt) VALUES (@n,@e,@h,'User',@c)",
            new Dictionary<string, object?> { ["@n"] = name, ["@e"] = normalizedEmail, ["@h"] = passwordHash, ["@c"] = utcNow.ToString("o") });
        return (int)db.LastInsertRowId();
    }
}
