namespace AquaMonitor.Application.Contracts;

public sealed record ReadingCreated(int ReadingId, bool AlertGenerated, int? AlertId);
