using System.Globalization;

namespace AquaMonitor.Infrastructure.Persistence;

internal static class DbValues
{
    // SQLite's historical datetime('now') strings have no zone. Their documented meaning here is UTC.
    public static DateTime Timestamp(object? value) => DateTime.Parse((string)value!, CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
