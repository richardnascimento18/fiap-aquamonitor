using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Application.Sensors;

public sealed class SensorService(IWorkSessionFactory sessions, TimeProvider time)
{
    public PagedResultVM<SensorResponseVM> List(Actor actor, int page, int pageSize)
    {
        RequestValidation.Page(page, pageSize, 100);
        using var session = sessions.Open();
        return session.Sensors.List(actor, page, pageSize);
    }
    public SensorResponseVM Get(Actor actor, int id)
    {
        using var session = sessions.Open();
        return Require(session.Sensors.Find(actor, id));
    }
    public SensorResponseVM Create(Actor actor, CreateSensorVM request)
    {
        RequestValidation.Validate(request);
        SensorRules.ValidateThreshold(request.AlertThresholdLitersPerHour);
        using var session = sessions.Open();
        return session.InTransaction(() =>
        {
            var id = session.Sensors.Insert(actor, request, time.GetUtcNow().UtcDateTime);
            return Require(session.Sensors.Find(actor, id));
        });
    }
    public SensorResponseVM Update(Actor actor, int id, UpdateSensorVM request)
    {
        RequestValidation.Validate(request);
        if (request.AlertThresholdLitersPerHour is double threshold) SensorRules.ValidateThreshold(threshold);
        using var session = sessions.Open();
        return session.InTransaction(() =>
        {
            Require(session.Sensors.Find(actor, id));
            session.Sensors.Update(actor, id, request);
            return Require(session.Sensors.Find(actor, id));
        });
    }
    public void Delete(Actor actor, int id)
    {
        actor.RequireAdministrator();
        using var session = sessions.Open();
        session.InTransaction(() =>
        {
            Require(session.Sensors.Find(actor, id));
            session.Sensors.Delete(id);
            return true;
        });
    }
    private static SensorResponseVM Require(SensorResponseVM? sensor)
        => sensor ?? throw new ApplicationFailure(404, "Sensor nao encontrado.");
}
