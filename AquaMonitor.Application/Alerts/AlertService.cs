using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Application.Alerts;

public sealed class AlertService(IWorkSessionFactory sessions, TimeProvider time)
{
    public PagedResultVM<AlertResponseVM> List(Actor actor, AlertFilterVM filter)
    {
        RequestValidation.Validate(filter);
        using var session = sessions.Open();
        return session.Alerts.List(actor, filter);
    }
    public AlertResponseVM Get(Actor actor, int id)
    {
        using var session = sessions.Open();
        return Require(session.Alerts.Find(actor, id));
    }
    public AlertResponseVM UpdateStatus(Actor actor, int id, UpdateAlertStatusVM request)
    {
        RequestValidation.Validate(request);
        using var session = sessions.Open();
        return session.InTransaction(() =>
        {
            var current = session.Alerts.FindStatus(actor, id) ?? throw new ApplicationFailure(404, "Alerta nao encontrado.");
            var resolvedAt = AlertRules.Transition(current, request.Status, time.GetUtcNow().UtcDateTime);
            session.Alerts.UpdateStatus(id, request.Status, resolvedAt);
            return Require(session.Alerts.Find(actor, id));
        });
    }
    public AlertStatistics Statistics(Actor actor)
    {
        actor.RequireAdministrator();
        using var session = sessions.Open();
        return session.InTransaction(session.Alerts.Statistics);
    }
    private static AlertResponseVM Require(AlertResponseVM? alert)
        => alert ?? throw new ApplicationFailure(404, "Alerta nao encontrado.");
}
