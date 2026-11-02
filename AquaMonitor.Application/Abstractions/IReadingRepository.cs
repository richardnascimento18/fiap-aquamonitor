using AquaMonitor.Application.Contracts;

namespace AquaMonitor.Application.Abstractions;

public interface IReadingRepository
{
    PagedResultVM<ReadingResponseVM> List(Actor actor, ReadingFilterVM filter);
    IEnumerable<ConsumptionSummaryVM> Summary(Actor actor, DateTime? from, DateTime? to, int? sensorId);
    int Insert(CreateReadingVM request, DateTime recordedAt, bool anomaly);
}
