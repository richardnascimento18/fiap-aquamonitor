# SQLite backup and recovery

Before replacing the application image, stop all AquaMonitor instances and offline provisioning processes that use the database. With no writers running, use SQLite's backup command to create a consistent snapshot:

```bash
sqlite3 /path/to/aquamonitor.db ".backup '/secure/backup/aquamonitor-before-upgrade.db'"
sqlite3 /secure/backup/aquamonitor-before-upgrade.db 'PRAGMA integrity_check; PRAGMA foreign_key_check;'
```

Expect `ok` and no foreign-key violations. Keep the backup outside the container and restrict its permissions: it contains accounts and operational data. Test the new application against a separate writable copy first, supplying the normal private JWT configuration. Never point a development/test process at the production database.

Startup applies ordered, embedded SQL migrations within an immediate SQLite transaction and records the schema version in `PRAGMA user_version`. Version zero is adopted only when the complete Phase 1 schema matches: tables, column definitions, defaults, unique/foreign-key constraints, and indexes. Unknown versions, schema drift, or foreign-key violations stop startup. Completed migrations are not replayed. Additional operational triggers are retained on already versioned databases; legacy adoption rejects them for explicit review.

If startup fails, keep the service stopped and inspect the server diagnostic. Do not delete tables, manually advance `user_version`, or repeatedly change the schema to force startup. Failed migration transactions roll back their schema/data/version changes. Correct a permissions/configuration issue and retry, or recover from the verified backup.

For recovery, stop all instances, preserve the failed database **and its `-wal`/`-shm` sidecars together** in a separate location for diagnosis, and restore the verified snapshot to a clean destination. Set ownership/permissions for the runtime UID (1654 in Docker), run the same integrity checks, and start the application version matching that backup. Never combine an old database with newer WAL sidecars. There are no automatic destructive/down migrations.

Historical SQLite `datetime('now')` values are interpreted as UTC. Queries compare parsed UTC ticks to support historical space-separated timestamps, ISO formats, offsets, and sub-millisecond boundaries without rewriting stored data. Monthly reporting uses `[month start, next month start)`; `SavedWaterM3` remains the prior-minus-current consumption difference, not evidence of savings caused by AquaMonitor.
