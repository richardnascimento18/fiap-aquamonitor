using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Rules;

namespace AquaMonitor.Application.Abstractions;

public sealed record ReportTotals(double CurrentLiters, double PreviousLiters, int LeaksDetected, int LeaksResolved);
public interface IReportRepository
{
    PagedResultVM<ReportResponseVM> List(Actor actor, int page, int pageSize);
    ReportResponseVM? Find(Actor actor, int id);
    ReportTotals Totals(ReportingPeriod period);
    int Insert(Actor actor, GenerateReportVM request, string title, ImpactCalculation impact, ReportTotals totals, DateTime utcNow);
    void Delete(int id);
}
