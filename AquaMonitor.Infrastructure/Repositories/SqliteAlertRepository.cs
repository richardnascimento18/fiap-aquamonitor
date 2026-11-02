using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Repositories;

internal sealed class SqliteAlertRepository(SqliteConnection db) : IAlertRepository
{
    private static AlertResponseVM MapAlert(Dictionary<string, object?> r) => new()
    {
        Id = (int)(long)r["Id"]!,
        Message = r["Message"]?.ToString() ?? "",
        Severity = r["SeverityLabel"]?.ToString() ?? "",
        Status = r["StatusLabel"]?.ToString() ?? "",
        TriggerFlowRate = (double)r["TriggerFlowRate"]!,
        CreatedAt = DbValues.Timestamp(r["CreatedAt"]),
        ResolvedAt = r["ResolvedAt"] is string ra ? DbValues.Timestamp(ra) : null,
        SensorId = (int)(long)r["SensorId"]!,
        SensorName = r["SensorName"]?.ToString() ?? "",
        SensorLocation = r["SensorLocation"]?.ToString() ?? ""
    };

    private const string SelectAlerts = """
        SELECT a.Id, a.Message,
               CASE a.Severity WHEN 4 THEN 'Critical' WHEN 3 THEN 'High' WHEN 2 THEN 'Medium' ELSE 'Low' END AS SeverityLabel,
               CASE a.Status   WHEN 2 THEN 'Resolved' WHEN 1 THEN 'Acknowledged' ELSE 'Open' END AS StatusLabel,
               a.TriggerFlowRate, a.CreatedAt, a.ResolvedAt, a.SensorId,
               s.Name AS SensorName, s.Location AS SensorLocation
        FROM LeakAlerts a JOIN WaterSensors s ON s.Id=a.SensorId
        """;


    public PagedResultVM<AlertResponseVM> List(Actor actor, AlertFilterVM filter)
    {

        var where = new List<string>();
        var p = new Dictionary<string, object?> { ["@uid"] = actor.UserId, ["@limit"] = filter.PageSize, ["@offset"] = ((long)filter.Page - 1) * filter.PageSize };

        if (!actor.IsAdmin) where.Add("s.UserId=@uid");
        if (filter.SensorId.HasValue) { where.Add("a.SensorId=@sid"); p["@sid"] = filter.SensorId.Value; }
        if (filter.Status.HasValue) { where.Add("a.Status=@st"); p["@st"] = (int)filter.Status.Value; }
        if (filter.Severity.HasValue) { where.Add("a.Severity=@sev"); p["@sev"] = (int)filter.Severity.Value; }

        var wClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM LeakAlerts a JOIN WaterSensors s ON s.Id=a.SensorId {wClause}", p)[0]["c"]!;
        var rows = db.Query($"{SelectAlerts} {wClause} ORDER BY utc_ticks(a.CreatedAt) DESC, a.Id DESC LIMIT @limit OFFSET @offset", p);

        return new PagedResultVM<AlertResponseVM>
        { Data = rows.Select(MapAlert).ToArray(), Page = filter.Page, PageSize = filter.PageSize, TotalCount = total };
    
    }
    public AlertResponseVM? Find(Actor actor, int id)
    {
        var filter = actor.IsAdmin ? "a.Id=@id" : "a.Id=@id AND s.UserId=@uid";
        var rows = db.Query($"{SelectAlerts} WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = actor.UserId });
        if (rows.Count == 0) return null;
        return MapAlert(rows[0]);
    
    }
    public int? FindStatus(Actor actor, int id)
    {
        var filter = actor.IsAdmin ? "a.Id=@id" : "a.Id=@id AND s.UserId=@uid";
        var rows = db.Query($"SELECT a.Status FROM LeakAlerts a JOIN WaterSensors s ON s.Id=a.SensorId WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = actor.UserId });
        return rows.Count == 0 ? null : (int)(long)rows[0]["Status"]!;
    }
    public void UpdateStatus(int id, int status, DateTime? resolvedAt)
        => db.Execute("UPDATE LeakAlerts SET Status=@st, ResolvedAt=@ra WHERE Id=@id",
            new Dictionary<string, object?> { ["@id"] = id, ["@st"] = status, ["@ra"] = resolvedAt?.ToString("o") });
    public AlertStatistics Statistics()
    {
        var total = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts")[0]["c"]!;
        var open = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE Status=0")[0]["c"]!;
        var ack = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE Status=1")[0]["c"]!;
        var resolved = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE Status=2")[0]["c"]!;
        var bySeverity = db.Query("SELECT Severity, COUNT(*) cnt FROM LeakAlerts GROUP BY Severity")
            .Select(r => new SeverityCount(SevLabel((int)(long)r["Severity"]!), (long)r["cnt"]!));
        return new AlertStatistics(total, open, ack, resolved, bySeverity.ToArray());
    
    }
    private static string SevLabel(int s) => s switch { 4 => "Critical", 3 => "High", 2 => "Medium", _ => "Low" };
    public int Insert(int sensorId, string message, int severity, double flow, DateTime utcNow)
    {
        db.Execute("INSERT INTO LeakAlerts (SensorId, Message, Severity, Status, TriggerFlowRate, CreatedAt) VALUES (@sid,@msg,@sev,0,@tfr,@ca)",
            new Dictionary<string, object?> { ["@sid"] = sensorId, ["@msg"] = message, ["@sev"] = severity, ["@tfr"] = flow, ["@ca"] = utcNow.ToString("o") });
        return (int)db.LastInsertRowId();
    }
}
