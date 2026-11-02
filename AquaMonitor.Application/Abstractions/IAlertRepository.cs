using AquaMonitor.Application.Contracts;

namespace AquaMonitor.Application.Abstractions;

public interface IAlertRepository
{
    int Insert(int sensorId, string message, int severity, double flow, DateTime utcNow);
}
