using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Rules;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Repositories;

internal sealed class SqliteReportRepository(SqliteConnection db) : IReportRepository
{
    private static ReportResponseVM MapReport(Dictionary<string, object?> r) => new()
    {
        Id = (int)(long)r["Id"]!,
        Title = r["Title"]?.ToString() ?? "",
        Year = (int)(long)r["Year"]!,
        Month = (int)(long)r["Month"]!,
        TotalConsumptionM3 = (double)r["TotalConsumptionM3"]!,
        SavedWaterM3 = (double)r["SavedWaterM3"]!,
        LeaksDetected = (int)(long)r["LeaksDetected"]!,
        LeaksResolved = (int)(long)r["LeaksResolved"]!,
        ReductionPercent = (double)r["ReductionPercent"]!,
        MitigationPlan = r["MitigationPlan"]?.ToString() ?? "",
        GeneratedAt = DbValues.Timestamp(r["GeneratedAt"]),
        GeneratedByUser = r["GeneratedByUser"]?.ToString() ?? ""
    };

    private const string SelectReports = """
        SELECT r.Id, r.Title, r.Year, r.Month, r.TotalConsumptionM3, r.SavedWaterM3,
               r.LeaksDetected, r.LeaksResolved, r.ReductionPercent, r.MitigationPlan,
               r.GeneratedAt, u.Name AS GeneratedByUser
        FROM WaterImpactReports r JOIN Users u ON u.Id=r.GeneratedByUserId
        """;


    public PagedResultVM<ReportResponseVM> List(Actor actor, int page, int pageSize)
    {

        var filter = actor.IsAdmin ? "" : "WHERE r.GeneratedByUserId=@uid";
        var p = new Dictionary<string, object?> { ["@uid"] = actor.UserId, ["@limit"] = pageSize, ["@offset"] = ((long)page - 1) * pageSize };

        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM WaterImpactReports r {filter}", p)[0]["c"]!;
        var rows = db.Query($"{SelectReports} {filter} ORDER BY utc_ticks(r.GeneratedAt) DESC, r.Id DESC LIMIT @limit OFFSET @offset", p);

        return new PagedResultVM<ReportResponseVM>
        { Data = rows.Select(MapReport).ToArray(), Page = page, PageSize = pageSize, TotalCount = total };
    
    }
    public ReportResponseVM? Find(Actor actor, int id)
    {
        var filter = actor.IsAdmin ? "r.Id=@id" : "r.Id=@id AND r.GeneratedByUserId=@uid";
        var rows = db.Query($"{SelectReports} WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = actor.UserId });
        if (rows.Count == 0) return null;
        return MapReport(rows[0]);
    
    }
    public ReportTotals Totals(ReportingPeriod period)
    {
        var p = new Dictionary<string, object?> { ["@s"] = period.Start.Ticks, ["@e"] = period.EndExclusive.Ticks, ["@ps"] = period.PreviousStart.Ticks };
        var current = Convert.ToDouble(db.Query("SELECT COALESCE(SUM(LitersConsumed),0.0) v FROM WaterReadings WHERE utc_ticks(RecordedAt)>=@s AND utc_ticks(RecordedAt)<@e", p)[0]["v"]);
        var previous = Convert.ToDouble(db.Query("SELECT COALESCE(SUM(LitersConsumed),0.0) v FROM WaterReadings WHERE utc_ticks(RecordedAt)>=@ps AND utc_ticks(RecordedAt)<@s", p)[0]["v"]);
        var detected = (int)(long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE utc_ticks(CreatedAt)>=@s AND utc_ticks(CreatedAt)<@e", p)[0]["c"]!;
        var resolved = (int)(long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE utc_ticks(ResolvedAt)>=@s AND utc_ticks(ResolvedAt)<@e", p)[0]["c"]!;
        return new(current, previous, detected, resolved);
    }
    public int Insert(Actor actor, GenerateReportVM request, string title, ImpactCalculation impact, ReportTotals totals, DateTime utcNow)
    {
        db.Execute("""
            INSERT INTO WaterImpactReports
              (Title,Year,Month,TotalConsumptionM3,SavedWaterM3,LeaksDetected,LeaksResolved,
               ReductionPercent,MitigationPlan,GeneratedAt,GeneratedByUserId)
            VALUES (@t,@yr,@mo,@tc,@sw,@ld,@lr,@rp,@mp,@ga,@uid)
            """, new Dictionary<string, object?>
            {
                ["@t"] = title, ["@yr"] = request.Year, ["@mo"] = request.Month,
                ["@tc"] = impact.TotalConsumptionM3, ["@sw"] = impact.SavedWaterM3,
                ["@ld"] = totals.LeaksDetected, ["@lr"] = totals.LeaksResolved,
                ["@rp"] = impact.ReductionPercent, ["@mp"] = request.MitigationPlan,
                ["@ga"] = utcNow.ToString("o"), ["@uid"] = actor.UserId
            });
        return (int)db.LastInsertRowId();
    }
    public void Delete(int id) => db.Execute("DELETE FROM WaterImpactReports WHERE Id=@id", new Dictionary<string, object?> { ["@id"] = id });
}
