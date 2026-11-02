using System.Text.RegularExpressions;
using AquaMonitor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Connection = AquaMonitor.Infrastructure.Persistence.SqliteConnection;

namespace AquaMonitor.Infrastructure.Migrations;

internal sealed record SqliteMigration(int Version, string Sql);

internal static class SqliteMigrator
{
    internal static IReadOnlyList<SqliteMigration> Migrations { get; } = new[] { new SqliteMigration(1, ReadBaseline()) };

    private static string ReadBaseline()
    {
        using var stream = typeof(SqliteMigrator).Assembly.GetManifestResourceStream("AquaMonitor.Infrastructure.Migrations.001_InitialSchema.sql")
            ?? throw new InvalidOperationException("Initial migration resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void Apply(IDbConnectionFactory connections, IReadOnlyList<SqliteMigration> migrations)
    {
        if (migrations.Count == 0 || !migrations.Select(m => m.Version).SequenceEqual(Enumerable.Range(1, migrations.Count)))
            throw new InvalidOperationException("SQLite migrations must have consecutive, ordered versions starting at one.");
        using var db = connections.Create();
        db.Execute("PRAGMA journal_mode=WAL");
        try
        {
            // An immediate transaction serializes competing startup/provisioning processes before reading the version.
            db.InTransaction(() =>
            {
                var version = (int)(long)db.Query("PRAGMA user_version")[0]["user_version"]!;
                if (version < 0 || version > migrations.Count)
                    throw new InvalidOperationException($"Unsupported SQLite schema version {version}; this application supports up to {migrations.Count}.");
                if (version == 0 && Schema(db).Count > 0)
                {
                    // Adopt only the complete historical schema, never a partial or arbitrary database.
                    ValidateSchema(db, migrations.Take(1));
                    version = 1;
                    db.Execute("PRAGMA user_version=1");
                }
                else
                    ValidateSchema(db, migrations.Take(version), allowOperationalTriggers: version > 0);

                foreach (var migration in migrations.Skip(version))
                {
                    db.Execute(migration.Sql);
                    // Version comes from the validated, compiled migration list, never external input.
                    db.Execute($"PRAGMA user_version={migration.Version}");
                }
                ValidateSchema(db, migrations, allowOperationalTriggers: true);
                if (db.Query("PRAGMA foreign_key_check").Count != 0)
                    throw new InvalidOperationException("SQLite foreign-key integrity check failed. Restore or repair the database before starting.");
                return true;
            });
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("SQLite migration failed and its transaction was rolled back. Check database permissions and integrity; restore a verified backup before retrying if needed.", exception);
        }
    }

    private static void ValidateSchema(Connection db, IEnumerable<SqliteMigration> applied, bool allowOperationalTriggers = false)
    {
        using var expected = new Connection(":memory:");
        foreach (var migration in applied) expected.Execute(migration.Sql);
        var expectedSchema = Schema(expected);
        var actualSchema = Schema(db);
        // Existing versioned databases may have operational triggers (including failure-injection tests).
        // They must survive startup. Legacy adoption remains strict; tables, indexes and constraints always match.
        var unexpected = actualSchema.Keys.Except(expectedSchema.Keys)
            .Where(key => !allowOperationalTriggers || !key.StartsWith("trigger:", StringComparison.Ordinal)).ToArray();
        var missing = expectedSchema.Keys.Except(actualSchema.Keys).ToArray();
        var changed = expectedSchema.Keys.Intersect(actualSchema.Keys).Where(key => expectedSchema[key] != actualSchema[key]).ToArray();
        if (unexpected.Length + missing.Length + changed.Length != 0)
            throw new InvalidOperationException($"Unexpected SQLite schema. Missing: {string.Join(", ", missing)}; unexpected: {string.Join(", ", unexpected)}; changed: {string.Join(", ", changed)}. No migration was committed. Restore or review the schema before retrying.");
    }

    private static Dictionary<string, string> Schema(Connection db)
        => db.Query("SELECT type,name,sql FROM sqlite_master WHERE name NOT GLOB 'sqlite_*' ORDER BY type,name")
            .ToDictionary(row => $"{row["type"]}:{row["name"]}", row => NormalizeSql((string?)row["sql"] ?? ""));

    private static string NormalizeSql(string sql)
        => Regex.Replace(sql, """'(?:''|[^'])*'|"(?:""|[^"])*"|\s+""",
            match => char.IsWhiteSpace(match.Value[0]) ? "" : match.Value);
}
