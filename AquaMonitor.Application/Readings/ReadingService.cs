using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Application.Readings;

public sealed class ReadingService(IWorkSessionFactory sessions, TimeProvider time)
{
    public PagedResultVM<ReadingResponseVM> List(Actor actor, ReadingFilterVM filter)
    {
        RequestValidation.Validate(filter);
        if (filter.From > filter.To) throw new ApplicationFailure(400, "Periodo invalido.");
        using var session = sessions.Open();
        return session.Readings.List(actor, filter);
    }
    public IEnumerable<ConsumptionSummaryVM> Summary(Actor actor, DateTime? from, DateTime? to, int? sensorId)
    {
        if (from > to || sensorId <= 0) throw new ApplicationFailure(400, "Periodo ou sensor invalido.");
        using var session = sessions.Open();
        return session.Readings.Summary(actor, from, to, sensorId);
    }
    public ReadingCreated Create(Actor actor, CreateReadingVM request)
    {
        RequestValidation.Validate(request);
        using var session = sessions.Open();
        return session.InTransaction(() =>
        {
            var sensor = session.Sensors.FindState(request.SensorId) ?? throw new ApplicationFailure(404, "Sensor nao encontrado.");
            actor.RequireOwnership(sensor.UserId);
            var anomaly = sensor.ClassifyReading(request.LitersConsumed, request.FlowRateLitersPerHour);
            var now = time.GetUtcNow().UtcDateTime;
            var recordedAt = request.RecordedAt ?? now;
            var id = session.Readings.Insert(request, recordedAt, anomaly);
            int? alertId = null;
            if (anomaly)
            {
                var severity = AlertRules.Severity(request.FlowRateLitersPerHour, sensor.AlertThresholdLitersPerHour);
                var message = AlertRules.Message(request.FlowRateLitersPerHour, sensor.AlertThresholdLitersPerHour, sensor.Name, sensor.Location, now);
                alertId = session.Alerts.Insert(sensor.Id, message, (int)severity, request.FlowRateLitersPerHour, now);
            }
            return new ReadingCreated(id, alertId.HasValue, alertId);
        });
    }
}
