using AquaMonitor.Infrastructure.Migrations;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Tests;

public class MigrationTests
{
    [Fact]
    public void FreshDatabase_IsVersionedIdempotentAndEnforcesForeignKeys()
    {
        using var database = new MigrationDatabase();
        DatabaseInitializer.Initialize(database.Connections);
        DatabaseInitializer.Initialize(database.Connections);
        using var db = database.Connections.Create();
        Assert.Equal(1L, db.Query("PRAGMA user_version")[0]["user_version"]);
        Assert.Equal(5, db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").Count);
        Assert.Equal(6, db.Query("SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'").Count);
        Assert.Equal(1L, db.Query("PRAGMA foreign_keys")[0]["foreign_keys"]);
        Assert.Empty(db.Query("PRAGMA foreign_key_check"));
        Assert.ThrowsAny<Exception>(() => db.Execute("INSERT INTO WaterReadings (SensorId) VALUES (999)"));
    }

    [Fact]
    public void HistoricalSchema_IsAdoptedWithoutChangingAnyOperationalData()
    {
        using var database = new MigrationDatabase();
        database.CreateLegacy();
        var salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var passwordHash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2("Legacy-password-123", salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var storedHash = $"v1$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(passwordHash)}";
        Dictionary<string, List<Dictionary<string, object?>>> before;
        using (var db = database.Connections.Create())
        {
            db.Execute("INSERT INTO Users (Name,Email,PasswordHash,Role) VALUES ('Legacy','legacy@test.com',@hash,'Admin')", new Dictionary<string, object?> { ["@hash"] = storedHash });
            db.Execute("INSERT INTO WaterSensors (Name,Location,UserId) VALUES ('Legacy sensor','Room',1)");
            db.Execute("INSERT INTO WaterReadings (SensorId,LitersConsumed,FlowRateLitersPerHour,IsAnomaly) VALUES (1,125.5,1600,1)");
            db.Execute("INSERT INTO LeakAlerts (SensorId,Message,Severity,Status,TriggerFlowRate,ResolvedAt) VALUES (1,'Legacy alert',4,2,1600,'2024-02-29 12:00:00')");
            db.Execute("INSERT INTO WaterImpactReports (Title,Year,Month,TotalConsumptionM3,SavedWaterM3,LeaksDetected,LeaksResolved,ReductionPercent,MitigationPlan,GeneratedByUserId) VALUES ('Legacy report',2024,2,0.126,-0.126,1,1,-100,'Inspect',1)");
            Assert.Equal(0L, db.Query("PRAGMA user_version")[0]["user_version"]);
            before = Snapshot(db);
        }
        DatabaseInitializer.Initialize(database.Connections);
        DatabaseInitializer.Initialize(database.Connections);
        using var verify = database.Connections.Create();
        foreach (var (table, rows) in before)
        {
            var after = verify.Query($"SELECT * FROM {table} ORDER BY Id");
            Assert.Equal(rows.Count, after.Count);
            for (var i = 0; i < rows.Count; i++) Assert.Equal(rows[i].OrderBy(p => p.Key), after[i].OrderBy(p => p.Key));
        }
        Assert.True(PasswordHelper.VerifyPassword("Legacy-password-123", (string)verify.Query("SELECT PasswordHash FROM Users")[0]["PasswordHash"]!));
        Assert.Equal(1L, verify.Query("PRAGMA user_version")[0]["user_version"]);
        Assert.Empty(verify.Query("PRAGMA foreign_key_check"));
        verify.Execute("INSERT INTO Users (Name,Email,PasswordHash) VALUES ('Next','next@test.com','hash')");
        Assert.Equal(2, verify.LastInsertRowId());
    }

    [Theory]
    [InlineData("DROP INDEX ix_readings_date")]
    [InlineData("ALTER TABLE Users ADD COLUMN Unexpected TEXT")]
    [InlineData("DROP TABLE WaterImpactReports")]
    [InlineData("CREATE TABLE Unexpected (Id INTEGER)")]
    [InlineData("DROP TABLE WaterReadings; CREATE TABLE WaterReadings (Id INTEGER PRIMARY KEY, SensorId INTEGER, LitersConsumed REAL, FlowRateLitersPerHour REAL, RecordedAt TEXT, IsAnomaly INTEGER); CREATE INDEX ix_readings_sensor ON WaterReadings(SensorId); CREATE INDEX ix_readings_date ON WaterReadings(RecordedAt)")]
    public void UnexpectedHistoricalSchema_IsRejectedWithoutStampingVersion(string mutation)
    {
        using var database = new MigrationDatabase();
        database.CreateLegacy();
        using (var db = database.Connections.Create()) db.Execute(mutation);
        var failure = Assert.Throws<InvalidOperationException>(() => DatabaseInitializer.Initialize(database.Connections));
        Assert.Contains("Unexpected SQLite schema", failure.Message);
        using var verify = database.Connections.Create();
        Assert.Equal(0L, verify.Query("PRAGMA user_version")[0]["user_version"]);
    }

    [Theory]
    [InlineData(-1)] [InlineData(2)] [InlineData(999)]
    public void UnsupportedVersion_IsRejected(int version)
    {
        using var database = new MigrationDatabase();
        using (var db = database.Connections.Create()) db.Execute($"PRAGMA user_version={version}");
        Assert.Contains("Unsupported SQLite schema version", Assert.Throws<InvalidOperationException>(() => DatabaseInitializer.Initialize(database.Connections)).Message);
        using var verify = database.Connections.Create();
        Assert.Equal((long)version, verify.Query("PRAGMA user_version")[0]["user_version"]);
    }

    [Fact]
    public void FailedMigration_RollsBackDdlDataAndVersion_AndCanBeRetried()
    {
        using var database = new MigrationDatabase();
        database.CreateLegacy();
        using (var db = database.Connections.Create()) db.Execute("INSERT INTO Users (Name,Email,PasswordHash) VALUES ('Preserved','preserved@test.com','hash')");
        var migrations = SqliteMigrator.Migrations.Concat(new[] { new SqliteMigration(2, "CREATE TABLE Partial (Id INTEGER); UPDATE Users SET Name='Changed'; INSERT INTO DoesNotExist VALUES (1);") }).ToArray();
        Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Apply(database.Connections, migrations));
        using (var verify = database.Connections.Create())
        {
            Assert.Equal(0L, verify.Query("PRAGMA user_version")[0]["user_version"]);
            Assert.Equal("Preserved", Assert.Single(verify.Query("SELECT Name FROM Users"))["Name"]);
            Assert.Empty(verify.Query("SELECT name FROM sqlite_master WHERE name='Partial'"));
        }
        DatabaseInitializer.Initialize(database.Connections);
    }

    [Fact]
    public void FailedFreshMigration_LeavesNoPartialSchema()
    {
        using var database = new MigrationDatabase();
        Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Apply(database.Connections, new[] { new SqliteMigration(1, "CREATE TABLE Partial (Id INTEGER); INVALID SQL;") }));
        using var db = database.Connections.Create();
        Assert.Equal(0L, db.Query("PRAGMA user_version")[0]["user_version"]);
        Assert.Empty(db.Query("SELECT name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'"));
    }

    [Fact]
    public void HistoricalForeignKeyCorruption_IsRejectedWithoutAdoption()
    {
        using var database = new MigrationDatabase();
        database.CreateLegacy();
        using (var db = database.Connections.Create())
        {
            db.Execute("PRAGMA foreign_keys=OFF");
            db.Execute("INSERT INTO WaterReadings (SensorId) VALUES (999)");
        }
        Assert.Contains("foreign-key integrity", Assert.Throws<InvalidOperationException>(() => DatabaseInitializer.Initialize(database.Connections)).Message);
        using var verify = database.Connections.Create();
        Assert.Equal(0L, verify.Query("PRAGMA user_version")[0]["user_version"]);
        Assert.Single(verify.Query("SELECT * FROM WaterReadings"));
    }

    [Fact]
    public async Task ConcurrentStartup_SerializesVersionAdoption()
    {
        using var database = new MigrationDatabase();
        database.CreateLegacy();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => DatabaseInitializer.Initialize(database.Connections))));
        using var verify = database.Connections.Create();
        Assert.Equal(1L, verify.Query("PRAGMA user_version")[0]["user_version"]);
        Assert.Empty(verify.Query("PRAGMA foreign_key_check"));
    }

    [Fact]
    public void AlreadyVersionedSchemaDrift_IsRejected()
    {
        using var database = new MigrationDatabase();
        DatabaseInitializer.Initialize(database.Connections);
        using (var db = database.Connections.Create()) db.Execute("DROP INDEX ix_alerts_created");
        Assert.Throws<InvalidOperationException>(() => DatabaseInitializer.Initialize(database.Connections));
    }

    [Fact]
    public void OrderedMigrations_AreAppliedExactlyOnce()
    {
        using var database = new MigrationDatabase();
        var migrations = SqliteMigrator.Migrations.Concat(new[] { new SqliteMigration(2, "CREATE TABLE MigrationProbe (Id INTEGER PRIMARY KEY); INSERT INTO MigrationProbe VALUES (1);") }).ToArray();
        SqliteMigrator.Apply(database.Connections, migrations);
        SqliteMigrator.Apply(database.Connections, migrations);
        using var verify = database.Connections.Create();
        Assert.Equal(2L, verify.Query("PRAGMA user_version")[0]["user_version"]);
        Assert.Single(verify.Query("SELECT * FROM MigrationProbe"));
    }

    [Fact]
    public void LegacyTriggers_RequireReview_ButVersionedOperationalTriggersArePreserved()
    {
        using var database = new MigrationDatabase();
        database.CreateLegacy();
        using (var db = database.Connections.Create()) db.Execute("CREATE TRIGGER guard_users BEFORE DELETE ON Users BEGIN SELECT RAISE(ABORT,'guard'); END");
        Assert.Throws<InvalidOperationException>(() => DatabaseInitializer.Initialize(database.Connections));
        using (var db = database.Connections.Create()) db.Execute("DROP TRIGGER guard_users");
        DatabaseInitializer.Initialize(database.Connections);
        using (var db = database.Connections.Create()) db.Execute("CREATE TRIGGER guard_users BEFORE DELETE ON Users BEGIN SELECT RAISE(ABORT,'guard'); END");
        DatabaseInitializer.Initialize(database.Connections);
        using var verify = database.Connections.Create();
        Assert.Single(verify.Query("SELECT name FROM sqlite_master WHERE name='guard_users'"));
    }

    private static Dictionary<string, List<Dictionary<string, object?>>> Snapshot(SqliteConnection db)
        => new[] { "Users", "WaterSensors", "WaterReadings", "LeakAlerts", "WaterImpactReports" }
            .ToDictionary(table => table, table => db.Query($"SELECT * FROM {table} ORDER BY Id"));
}

internal sealed class MigrationDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aquamigration_{Guid.NewGuid():N}.db");
    public IDbConnectionFactory Connections { get; }
    public MigrationDatabase() => Connections = new SqliteConnectionFactory(_path);
    public void CreateLegacy()
    {
        using var stream = typeof(MigrationTests).Assembly.GetManifestResourceStream("AquaMonitor.Tests.Fixtures.Phase1Schema.sql")!;
        using var reader = new StreamReader(stream);
        using var db = Connections.Create();
        db.Execute(reader.ReadToEnd());
    }
    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_path + suffix);
    }
}
