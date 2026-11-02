using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Repositories;

internal sealed class SqliteReadingRepository(SqliteConnection db) : IReadingRepository
{
    public PagedResultVM<ReadingResponseVM> List(Actor actor, ReadingFilterVM filter)
    {

        var where = new List<string>();
        var p = new Dictionary<string, object?> { ["@uid"] = actor.UserId, ["@limit"] = filter.PageSize, ["@offset"] = ((long)filter.Page - 1) * filter.PageSize };

        if (!actor.IsAdmin) where.Add("s.UserId=@uid");
        if (filter.SensorId.HasValue) { where.Add("r.SensorId=@sid"); p["@sid"] = filter.SensorId.Value; }
        if (filter.From.HasValue) { where.Add("utc_ticks(r.RecordedAt)>=@from"); p["@from"] = DbValues.Ticks(filter.From.Value); }
        if (filter.To.HasValue) { where.Add("utc_ticks(r.RecordedAt)<=@to"); p["@to"] = DbValues.Ticks(filter.To.Value); }
        if (filter.AnomaliesOnly == true) where.Add("r.IsAnomaly=1");

        var wClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM WaterReadings r JOIN WaterSensors s ON s.Id=r.SensorId {wClause}", p)[0]["c"]!;

        var rows = db.Query($"""
            SELECT r.Id, r.SensorId, s.Name AS SensorName, s.Location AS SensorLocation,
                   r.LitersConsumed, r.FlowRateLitersPerHour, r.RecordedAt, r.IsAnomaly
            FROM WaterReadings r JOIN WaterSensors s ON s.Id=r.SensorId
            {wClause} ORDER BY utc_ticks(r.RecordedAt) DESC, r.Id DESC LIMIT @limit OFFSET @offset
            """, p);

        return new PagedResultVM<ReadingResponseVM>
        {
            Data = rows.Select(r => new ReadingResponseVM
            {
                Id = (int)(long)r["Id"]!,
                SensorId = (int)(long)r["SensorId"]!,
                SensorName = r["SensorName"]?.ToString() ?? "",
                SensorLocation = r["SensorLocation"]?.ToString() ?? "",
                LitersConsumed = (double)r["LitersConsumed"]!,
                FlowRateLitersPerHour = (double)r["FlowRateLitersPerHour"]!,
                RecordedAt = DbValues.Timestamp(r["RecordedAt"]),
                IsAnomaly = (long)r["IsAnomaly"]! == 1
            }).ToArray(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total
        };
    
    }
    public IEnumerable<ConsumptionSummaryVM> Summary(Actor actor, DateTime? from, DateTime? to, int? sensorId)
    {
        var where = new List<string>();
        var p = new Dictionary<string, object?> { ["@uid"] = actor.UserId };
        if (!actor.IsAdmin) where.Add("s.UserId=@uid");
        if (sensorId.HasValue) { where.Add("r.SensorId=@sid"); p["@sid"] = sensorId.Value; }
        if (from.HasValue) { where.Add("utc_ticks(r.RecordedAt)>=@from"); p["@from"] = DbValues.Ticks(from.Value); }
        if (to.HasValue) { where.Add("utc_ticks(r.RecordedAt)<=@to"); p["@to"] = DbValues.Ticks(to.Value); }
        var wClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        var rows = db.Query($"""
            SELECT r.SensorId, s.Name, s.Location,
                   SUM(r.LitersConsumed) AS TotalLiters,
                   AVG(r.FlowRateLitersPerHour) AS AvgFlow,
                   MAX(r.FlowRateLitersPerHour) AS MaxFlow,
                   SUM(CASE WHEN r.IsAnomaly=1 THEN 1 ELSE 0 END) AS AnomalyCount,
                   MIN(utc_ticks(r.RecordedAt)) AS PeriodStart, MAX(utc_ticks(r.RecordedAt)) AS PeriodEnd
            FROM WaterReadings r JOIN WaterSensors s ON s.Id=r.SensorId
            {wClause} GROUP BY r.SensorId, s.Name, s.Location ORDER BY TotalLiters DESC
            """, p);

        return rows.Select(r => new ConsumptionSummaryVM
        {
            SensorId = (int)(long)r["SensorId"]!,
            SensorName = r["Name"]?.ToString() ?? "",
            Location = r["Location"]?.ToString() ?? "",
            TotalLiters = (double)(r["TotalLiters"] ?? 0.0),
            AverageFlowRate = (double)(r["AvgFlow"] ?? 0.0),
            MaxFlowRate = (double)(r["MaxFlow"] ?? 0.0),
            AnomalyCount = (int)(long)(r["AnomalyCount"] ?? 0L),
            PeriodStart = r["PeriodStart"] is long ps ? new DateTime(ps, DateTimeKind.Utc) : null,
            PeriodEnd = r["PeriodEnd"] is long pe ? new DateTime(pe, DateTimeKind.Utc) : null
        }).ToArray();
    
    }
    public int Insert(CreateReadingVM request, DateTime recordedAt, bool anomaly)
    {
        db.Execute("INSERT INTO WaterReadings (SensorId,LitersConsumed,FlowRateLitersPerHour,RecordedAt,IsAnomaly) VALUES (@sid,@lc,@fr,@ra,@ia)",
            new Dictionary<string, object?> { ["@sid"] = request.SensorId, ["@lc"] = request.LitersConsumed, ["@fr"] = request.FlowRateLitersPerHour, ["@ra"] = DbValues.Utc(recordedAt).ToString("o"), ["@ia"] = anomaly ? 1 : 0 });
        return (int)db.LastInsertRowId();
    }
}
