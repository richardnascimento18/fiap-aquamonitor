using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AquaMonitor.API.Data;
using AquaMonitor.API.ViewModels;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WaterSensorsController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;
    public WaterSensorsController(IDbConnectionFactory factory) => _factory = factory;

    private int  UserId  => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private bool IsAdmin => User.IsInRole("Admin");

    private static SensorResponseVM MapSensor(Dictionary<string, object?> r) => new()
    {
        Id = (int)(long)r["Id"]!,
        Name = r["Name"]?.ToString() ?? "",
        Location = r["Location"]?.ToString() ?? "",
        AlertThresholdLitersPerHour = (double)r["AlertThresholdLitersPerHour"]!,
        IsActive = (long)r["IsActive"]! == 1,
        InstalledAt = DateTime.Parse(r["InstalledAt"]?.ToString() ?? DateTime.UtcNow.ToString("o")),
        UserId = (int)(long)r["UserId"]!,
        UserName = r["UserName"]?.ToString() ?? "",
        TotalReadings = (int)(long)(r.ContainsKey("TotalReadings") ? r["TotalReadings"]! : 0L),
        OpenAlerts = (int)(long)(r.ContainsKey("OpenAlerts") ? r["OpenAlerts"]! : 0L)
    };

    /// <summary>Lista sensores com paginacao.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<SensorResponseVM>), 200)]
    public IActionResult GetAll([FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 10;

        using var db = _factory.Create();
        var filter = IsAdmin ? "" : "WHERE s.UserId=@uid";
        var p = new Dictionary<string, object?> { ["@uid"] = UserId, ["@limit"] = pageSize, ["@offset"] = ((long)page - 1) * pageSize };

        var countSql = $"SELECT COUNT(*) c FROM WaterSensors s {filter}";
        var total = (int)(long)db.Query(countSql, p)[0]["c"]!;

        var sql = $"""
            SELECT s.Id, s.Name, s.Location, s.AlertThresholdLitersPerHour, s.IsActive,
                   s.InstalledAt, s.UserId, u.Name AS UserName,
                   (SELECT COUNT(*) FROM WaterReadings WHERE SensorId=s.Id) AS TotalReadings,
                   (SELECT COUNT(*) FROM LeakAlerts WHERE SensorId=s.Id AND Status=0) AS OpenAlerts
            FROM WaterSensors s JOIN Users u ON u.Id=s.UserId
            {filter}
            ORDER BY s.Id LIMIT @limit OFFSET @offset
            """;

        var rows = db.Query(sql, p);
        return Ok(new PagedResultVM<SensorResponseVM>
        {
            Data = rows.Select(MapSensor),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    /// <summary>Retorna sensor por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SensorResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetById(int id)
    {
        using var db = _factory.Create();
        var filter = IsAdmin ? "s.Id=@id" : "s.Id=@id AND s.UserId=@uid";
        var sql = $"""
            SELECT s.Id, s.Name, s.Location, s.AlertThresholdLitersPerHour, s.IsActive,
                   s.InstalledAt, s.UserId, u.Name AS UserName,
                   (SELECT COUNT(*) FROM WaterReadings WHERE SensorId=s.Id) AS TotalReadings,
                   (SELECT COUNT(*) FROM LeakAlerts WHERE SensorId=s.Id AND Status=0) AS OpenAlerts
            FROM WaterSensors s JOIN Users u ON u.Id=s.UserId
            WHERE {filter} LIMIT 1
            """;
        var rows = db.Query(sql, new Dictionary<string, object?> { ["@id"]=id, ["@uid"]=UserId });
        if (rows.Count == 0) return NotFound(new { message = "Sensor nao encontrado." });
        return Ok(MapSensor(rows[0]));
    }

    /// <summary>Cria novo sensor.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SensorResponseVM), 201)]
    [ProducesResponseType(400)]
    public IActionResult Create([FromBody] CreateSensorVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var db = _factory.Create();
        db.Execute(
            "INSERT INTO WaterSensors (Name,Location,AlertThresholdLitersPerHour,IsActive,InstalledAt,UserId) VALUES (@n,@l,@t,1,@ia,@uid)",
            new Dictionary<string, object?> { ["@n"]=vm.Name, ["@l"]=vm.Location, ["@t"]=vm.AlertThresholdLitersPerHour, ["@ia"]=DateTime.UtcNow.ToString("o"), ["@uid"]=UserId });
        var newId = (int)db.LastInsertRowId();
        return StatusCode(201, GetSensorById_Internal(db, newId));
    }

    /// <summary>Atualiza sensor existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SensorResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult Update(int id, [FromBody] UpdateSensorVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var db = _factory.Create();
        var filter = IsAdmin ? "Id=@id" : "Id=@id AND UserId=@uid";
        var exists = db.Query($"SELECT 1 FROM WaterSensors WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"]=id, ["@uid"]=UserId });
        if (exists.Count == 0) return NotFound(new { message = "Sensor nao encontrado." });

        var sets = new List<string>();
        var p = new Dictionary<string, object?> { ["@id"]=id, ["@uid"]=UserId };
        if (vm.Name     is not null) { sets.Add("Name=@n");     p["@n"] = vm.Name; }
        if (vm.Location is not null) { sets.Add("Location=@l"); p["@l"] = vm.Location; }
        if (vm.AlertThresholdLitersPerHour.HasValue) { sets.Add("AlertThresholdLitersPerHour=@t"); p["@t"] = vm.AlertThresholdLitersPerHour.Value; }
        if (vm.IsActive.HasValue) { sets.Add("IsActive=@a"); p["@a"] = vm.IsActive.Value ? 1 : 0; }
        if (sets.Count > 0)
            db.Execute($"UPDATE WaterSensors SET {string.Join(",", sets)} WHERE {filter}", p);

        return Ok(GetSensorById_Internal(db, id));
    }

    /// <summary>Remove sensor (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public IActionResult Delete(int id)
    {
        using var db = _factory.Create();
        var exists = db.Query("SELECT 1 FROM WaterSensors WHERE Id=@id LIMIT 1",
            new Dictionary<string, object?> { ["@id"]=id });
        if (exists.Count == 0) return NotFound(new { message = "Sensor nao encontrado." });
        db.Execute("DELETE FROM WaterSensors WHERE Id=@id", new Dictionary<string, object?> { ["@id"]=id });
        return NoContent();
    }

    private static SensorResponseVM GetSensorById_Internal(SqliteConnection db, int id)
    {
        var rows = db.Query("""
            SELECT s.Id, s.Name, s.Location, s.AlertThresholdLitersPerHour, s.IsActive,
                   s.InstalledAt, s.UserId, u.Name AS UserName,
                   (SELECT COUNT(*) FROM WaterReadings WHERE SensorId=s.Id) AS TotalReadings,
                   (SELECT COUNT(*) FROM LeakAlerts WHERE SensorId=s.Id AND Status=0) AS OpenAlerts
            FROM WaterSensors s JOIN Users u ON u.Id=s.UserId
            WHERE s.Id=@id LIMIT 1
            """,
            new Dictionary<string, object?> { ["@id"] = id });
        return MapSensor(rows[0]);
    }
}
