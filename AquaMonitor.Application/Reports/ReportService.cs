using System.Globalization;
using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Application.Reports;

public sealed class ReportService(IWorkSessionFactory sessions, TimeProvider time)
{
    public PagedResultVM<ReportResponseVM> List(Actor actor, int page, int pageSize)
    {
        RequestValidation.Page(page, pageSize, 50);
        using var session = sessions.Open();
        return session.Reports.List(actor, page, pageSize);
    }
    public ReportResponseVM Get(Actor actor, int id)
    {
        using var session = sessions.Open();
        return Require(session.Reports.Find(actor, id));
    }
    public ReportResponseVM Generate(Actor actor, GenerateReportVM request)
    {
        actor.RequireAdministrator();
        RequestValidation.Validate(request);
        var period = new ReportingPeriod(request.Year, request.Month);
        using var session = sessions.Open();
        return session.InTransaction(() =>
        {
            var totals = session.Reports.Totals(period);
            var impact = ImpactCalculation.Calculate(totals.CurrentLiters, totals.PreviousLiters);
            var monthName = period.Start.ToString("MMMM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
            var id = session.Reports.Insert(actor, request, $"Relatorio de Impacto Hidrico — {monthName}", impact, totals, time.GetUtcNow().UtcDateTime);
            return Require(session.Reports.Find(actor, id));
        });
    }
    public void Delete(Actor actor, int id)
    {
        actor.RequireAdministrator();
        using var session = sessions.Open();
        session.InTransaction(() =>
        {
            Require(session.Reports.Find(actor, id));
            session.Reports.Delete(id);
            return true;
        });
    }
    private static ReportResponseVM Require(ReportResponseVM? report)
        => report ?? throw new ApplicationFailure(404, "Relatorio nao encontrado.");
}
