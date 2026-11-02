-- Historical Phase 1 schema from 429c10c9e6dd570e5f3e3e5596113e270d2281b9.
CREATE TABLE IF NOT EXISTS Users (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Name         TEXT    NOT NULL,
    Email        TEXT    NOT NULL UNIQUE,
    PasswordHash TEXT    NOT NULL,
    Role         TEXT    NOT NULL DEFAULT 'User',
    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS WaterSensors (
    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                        TEXT    NOT NULL,
    Location                    TEXT    NOT NULL,
    AlertThresholdLitersPerHour REAL    NOT NULL DEFAULT 500,
    IsActive                    INTEGER NOT NULL DEFAULT 1,
    InstalledAt                 TEXT    NOT NULL DEFAULT (datetime('now')),
    UserId                      INTEGER NOT NULL REFERENCES Users(Id)
);

CREATE INDEX IF NOT EXISTS ix_sensors_user ON WaterSensors(UserId);

CREATE TABLE IF NOT EXISTS WaterReadings (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    SensorId             INTEGER NOT NULL REFERENCES WaterSensors(Id),
    LitersConsumed       REAL    NOT NULL DEFAULT 0,
    FlowRateLitersPerHour REAL   NOT NULL DEFAULT 0,
    RecordedAt           TEXT    NOT NULL DEFAULT (datetime('now')),
    IsAnomaly            INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_readings_sensor ON WaterReadings(SensorId);

CREATE INDEX IF NOT EXISTS ix_readings_date   ON WaterReadings(RecordedAt);

CREATE TABLE IF NOT EXISTS LeakAlerts (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    SensorId        INTEGER NOT NULL REFERENCES WaterSensors(Id),
    Message         TEXT    NOT NULL,
    Severity        INTEGER NOT NULL DEFAULT 2,
    Status          INTEGER NOT NULL DEFAULT 0,
    TriggerFlowRate REAL    NOT NULL DEFAULT 0,
    CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now')),
    ResolvedAt      TEXT
);

CREATE INDEX IF NOT EXISTS ix_alerts_status    ON LeakAlerts(Status);

CREATE INDEX IF NOT EXISTS ix_alerts_sensor    ON LeakAlerts(SensorId);

CREATE INDEX IF NOT EXISTS ix_alerts_created   ON LeakAlerts(CreatedAt);

CREATE TABLE IF NOT EXISTS WaterImpactReports (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Title               TEXT    NOT NULL,
    Year                INTEGER NOT NULL,
    Month               INTEGER NOT NULL,
    TotalConsumptionM3  REAL    NOT NULL DEFAULT 0,
    SavedWaterM3        REAL    NOT NULL DEFAULT 0,
    LeaksDetected       INTEGER NOT NULL DEFAULT 0,
    LeaksResolved       INTEGER NOT NULL DEFAULT 0,
    ReductionPercent    REAL    NOT NULL DEFAULT 0,
    MitigationPlan      TEXT    NOT NULL DEFAULT '',
    GeneratedAt         TEXT    NOT NULL DEFAULT (datetime('now')),
    GeneratedByUserId   INTEGER NOT NULL REFERENCES Users(Id)
);
