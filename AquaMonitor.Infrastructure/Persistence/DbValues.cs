using System.Globalization;

namespace AquaMonitor.Infrastructure.Persistence;

internal static class DbValues
{
    public static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    public static long Ticks(DateTime value) => Utc(value).Ticks;

    // SQLite's historical datetime('now') strings have no zone. Their documented meaning here is UTC.
    public static DateTime Timestamp(object? value) => DateTime.Parse((string)value!, CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
