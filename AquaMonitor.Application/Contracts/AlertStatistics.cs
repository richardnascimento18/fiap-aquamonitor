namespace AquaMonitor.Application.Contracts;

public sealed record SeverityCount(string Severity, long Count);
public sealed record AlertStatistics(long TotalAlerts, long OpenAlerts, long AcknowledgedAlerts, long ResolvedAlerts, IEnumerable<SeverityCount> BySeverity);
