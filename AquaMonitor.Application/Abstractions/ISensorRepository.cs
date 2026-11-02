using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Entities;

namespace AquaMonitor.Application.Abstractions;

public interface ISensorRepository
{
    PagedResultVM<SensorResponseVM> List(Actor actor, int page, int pageSize);
    SensorResponseVM? Find(Actor actor, int id);
    WaterSensor? FindState(int id);
    int Insert(Actor actor, CreateSensorVM request, DateTime utcNow);
    void Update(Actor actor, int id, UpdateSensorVM request);
    void Delete(int id);
}
