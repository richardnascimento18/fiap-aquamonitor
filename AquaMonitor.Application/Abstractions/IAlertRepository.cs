using AquaMonitor.Application.Contracts;

namespace AquaMonitor.Application.Abstractions;

public interface IAlertRepository
{
    PagedResultVM<AlertResponseVM> List(Actor actor, AlertFilterVM filter);
    AlertResponseVM? Find(Actor actor, int id);
    int? FindStatus(Actor actor, int id);
    void UpdateStatus(int id, int status, DateTime? resolvedAt);
    AlertStatistics Statistics();
    int Insert(int sensorId, string message, int severity, double flow, DateTime utcNow);
}
