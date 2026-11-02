using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Entities;
using AquaMonitor.Infrastructure.Persistence;

namespace AquaMonitor.Infrastructure.Repositories;

internal sealed class SqliteSensorRepository(SqliteConnection db) : ISensorRepository
{
    private const string SelectSensors = """
        SELECT s.Id, s.Name, s.Location, s.AlertThresholdLitersPerHour, s.IsActive,
               s.InstalledAt, s.UserId, u.Name AS UserName,
               (SELECT COUNT(*) FROM WaterReadings WHERE SensorId=s.Id) AS TotalReadings,
               (SELECT COUNT(*) FROM LeakAlerts WHERE SensorId=s.Id AND Status=0) AS OpenAlerts
        FROM WaterSensors s JOIN Users u ON u.Id=s.UserId
        """;
    private static SensorResponseVM MapSensor(Dictionary<string, object?> r) => new()
    {
        Id = (int)(long)r["Id"]!,
        Name = r["Name"]?.ToString() ?? "",
        Location = r["Location"]?.ToString() ?? "",
        AlertThresholdLitersPerHour = (double)r["AlertThresholdLitersPerHour"]!,
        IsActive = (long)r["IsActive"]! == 1,
        InstalledAt = DbValues.Timestamp(r["InstalledAt"]),
        UserId = (int)(long)r["UserId"]!,
        UserName = r["UserName"]?.ToString() ?? "",
        TotalReadings = (int)(long)(r.ContainsKey("TotalReadings") ? r["TotalReadings"]! : 0L),
        OpenAlerts = (int)(long)(r.ContainsKey("OpenAlerts") ? r["OpenAlerts"]! : 0L)
    };


    public PagedResultVM<SensorResponseVM> List(Actor actor, int page, int pageSize)
    {
        var filter = actor.IsAdmin ? "" : "WHERE s.UserId=@uid";
        var p = new Dictionary<string, object?> { ["@uid"] = actor.UserId, ["@limit"] = pageSize, ["@offset"] = ((long)page - 1) * pageSize };
        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM WaterSensors s {filter}", p)[0]["c"]!;
        var rows = db.Query($"{SelectSensors} {filter} ORDER BY s.Id LIMIT @limit OFFSET @offset", p);
        return new() { Data = rows.Select(MapSensor).ToArray(), Page = page, PageSize = pageSize, TotalCount = total };
    }
    public SensorResponseVM? Find(Actor actor, int id)
    {
        var filter = actor.IsAdmin ? "s.Id=@id" : "s.Id=@id AND s.UserId=@uid";
        var rows = db.Query($"{SelectSensors} WHERE {filter} LIMIT 1", new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = actor.UserId });
        return rows.Count == 0 ? null : MapSensor(rows[0]);
    }
    public WaterSensor? FindState(int id)
    {
        var rows = db.Query("SELECT * FROM WaterSensors WHERE Id=@id", new Dictionary<string, object?> { ["@id"] = id });
        if (rows.Count == 0) return null;
        var r = rows[0];
        return new WaterSensor(id, (string)r["Name"]!, (string)r["Location"]!, (double)r["AlertThresholdLitersPerHour"]!,
            (long)r["IsActive"]! == 1, DbValues.Timestamp(r["InstalledAt"]), (int)(long)r["UserId"]!);
    }
    public int Insert(Actor actor, CreateSensorVM request, DateTime utcNow)
    {
        db.Execute("INSERT INTO WaterSensors (Name,Location,AlertThresholdLitersPerHour,IsActive,InstalledAt,UserId) VALUES (@n,@l,@t,1,@ia,@uid)",
            new Dictionary<string, object?> { ["@n"] = request.Name, ["@l"] = request.Location, ["@t"] = request.AlertThresholdLitersPerHour, ["@ia"] = utcNow.ToString("o"), ["@uid"] = actor.UserId });
        return (int)db.LastInsertRowId();
    }
    public void Update(Actor actor, int id, UpdateSensorVM request)
    {
        var sets = new List<string>();
        var p = new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = actor.UserId };
        if (request.Name is not null) { sets.Add("Name=@n"); p["@n"] = request.Name; }
        if (request.Location is not null) { sets.Add("Location=@l"); p["@l"] = request.Location; }
        if (request.AlertThresholdLitersPerHour.HasValue) { sets.Add("AlertThresholdLitersPerHour=@t"); p["@t"] = request.AlertThresholdLitersPerHour.Value; }
        if (request.IsActive.HasValue) { sets.Add("IsActive=@a"); p["@a"] = request.IsActive.Value ? 1 : 0; }
        var filter = actor.IsAdmin ? "Id=@id" : "Id=@id AND UserId=@uid";
        if (sets.Count > 0) db.Execute($"UPDATE WaterSensors SET {string.Join(",", sets)} WHERE {filter}", p);
    }
    public void Delete(int id) => db.Execute("DELETE FROM WaterSensors WHERE Id=@id", new Dictionary<string, object?> { ["@id"] = id });
}
