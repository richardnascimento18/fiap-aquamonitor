using AquaMonitor.Infrastructure.Migrations;

namespace AquaMonitor.Infrastructure.Persistence;

/// <summary>Applies versioned, transactional migrations or validates and adopts the historical schema.</summary>
public static class DatabaseInitializer
{
    public static void Initialize(IDbConnectionFactory factory) => SqliteMigrator.Apply(factory, SqliteMigrator.Migrations);
}
