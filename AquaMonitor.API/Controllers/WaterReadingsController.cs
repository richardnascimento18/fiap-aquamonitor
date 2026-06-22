using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AquaMonitor.API.Data;
using AquaMonitor.API.Services;
using AquaMonitor.API.ViewModels;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WaterReadingsController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;
    private readonly IAlertService _alerts;
    public WaterReadingsController(IDbConnectionFactory factory, IAlertService alerts)
    { _factory = factory; _alerts = alerts; }

    private int  UserId  => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Lista leituras com paginacao e filtros.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<ReadingResponseVM>), 200)]
    public IActionResult GetAll([FromQuery] ReadingFilterVM filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize is < 1 or > 100) filter.PageSize = 20;

        using var db = _factory.Create();
        var where = new List<string>();
        var p = new Dictionary<string, object?> { ["@uid"]=UserId, ["@limit"]=filter.PageSize, ["@offset"]=(filter.Page-1)*filter.PageSize };

        if (!IsAdmin) where.Add("s.UserId=@uid");
        if (filter.SensorId.HasValue) { where.Add("r.SensorId=@sid"); p["@sid"]=filter.SensorId.Value; }
        if (filter.From.HasValue) { where.Add("r.RecordedAt>=@from"); p["@from"]=filter.From.Value.ToString("o"); }
        if (filter.To.HasValue)   { where.Add("r.RecordedAt<=@to");   p["@to"]=filter.To.Value.ToString("o"); }
        if (filter.AnomaliesOnly==true) where.Add("r.IsAnomaly=1");

        var wClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM WaterReadings r JOIN WaterSensors s ON s.Id=r.SensorId {wClause}", p)[0]["c"]!;

        var rows = db.Query($"""
            SELECT r.Id, r.SensorId, s.Name AS SensorName, s.Location AS SensorLocation,
                   r.LitersConsumed, r.FlowRateLitersPerHour, r.RecordedAt, r.IsAnomaly
            FROM WaterReadings r JOIN WaterSensors s ON s.Id=r.SensorId
            {wClause} ORDER BY r.RecordedAt DESC LIMIT @limit OFFSET @offset
            """, p);

        return Ok(new PagedResultVM<ReadingResponseVM>
        {
            Data = rows.Select(r => new ReadingResponseVM
            {
                Id = (int)(long)r["Id"]!,
                SensorId = (int)(long)r["SensorId"]!,
                SensorName = r["SensorName"]?.ToString() ?? "",
                SensorLocation = r["SensorLocation"]?.ToString() ?? "",
                LitersConsumed = (double)r["LitersConsumed"]!,
                FlowRateLitersPerHour = (double)r["FlowRateLitersPerHour"]!,
                RecordedAt = DateTime.Parse(r["RecordedAt"]?.ToString()!),
                IsAnomaly = (long)r["IsAnomaly"]! == 1
            }),
            Page=filter.Page, PageSize=filter.PageSize, TotalCount=total
        });
    }

    /// <summary>Registra nova leitura. Dispara alerta automatico se vazao exceder limiar.</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateReadingVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var db = _factory.Create();
        var sensor = db.Query("SELECT Id,Name,Location,AlertThresholdLitersPerHour,IsActive,UserId FROM WaterSensors WHERE Id=@id LIMIT 1",
            new Dictionary<string, object?> { ["@id"]=vm.SensorId });
        if (sensor.Count == 0) return NotFound(new { message = "Sensor nao encontrado." });

        var s = sensor[0];
        if (!IsAdmin && (int)(long)s["UserId"]! != UserId) return Forbid();
        if ((long)s["IsActive"]! == 0) return BadRequest(new { message = "Sensor inativo." });

        var threshold = (double)s["AlertThresholdLitersPerHour"]!;
        var isAnomaly = vm.FlowRateLitersPerHour > threshold;
        var recordedAt = (vm.RecordedAt ?? DateTime.UtcNow).ToString("o");

        db.Execute("INSERT INTO WaterReadings (SensorId,LitersConsumed,FlowRateLitersPerHour,RecordedAt,IsAnomaly) VALUES (@sid,@lc,@fr,@ra,@ia)",
            new Dictionary<string, object?> { ["@sid"]=vm.SensorId, ["@lc"]=vm.LitersConsumed, ["@fr"]=vm.FlowRateLitersPerHour, ["@ra"]=recordedAt, ["@ia"]=isAnomaly?1:0 });
        var newId = (int)db.LastInsertRowId();

        int? alertId = null;
        if (isAnomaly)
            alertId = await _alerts.EvaluateReadingAsync(vm.SensorId, vm.FlowRateLitersPerHour, threshold,
                s["Name"]?.ToString()!, s["Location"]?.ToString()!);

        return StatusCode(201, new { readingId=newId, alertGenerated=alertId.HasValue, alertId });
    }

    /// <summary>Resumo de consumo por sensor em um periodo.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<ConsumptionSummaryVM>), 200)]
    public IActionResult GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? sensorId)
    {
        using var db = _factory.Create();
        var where = new List<string>();
        var p = new Dictionary<string, object?> { ["@uid"]=UserId };
        if (!IsAdmin) where.Add("s.UserId=@uid");
        if (sensorId.HasValue) { where.Add("r.SensorId=@sid"); p["@sid"]=sensorId.Value; }
        if (from.HasValue) { where.Add("r.RecordedAt>=@from"); p["@from"]=from.Value.ToString("o"); }
        if (to.HasValue)   { where.Add("r.RecordedAt<=@to");   p["@to"]=to.Value.ToString("o"); }
        var wClause = where.Count>0 ? "WHERE "+string.Join(" AND ", where) : "";

        var rows = db.Query($"""
            SELECT r.SensorId, s.Name, s.Location,
                   SUM(r.LitersConsumed) AS TotalLiters,
                   AVG(r.FlowRateLitersPerHour) AS AvgFlow,
                   MAX(r.FlowRateLitersPerHour) AS MaxFlow,
                   SUM(CASE WHEN r.IsAnomaly=1 THEN 1 ELSE 0 END) AS AnomalyCount,
                   MIN(r.RecordedAt) AS PeriodStart, MAX(r.RecordedAt) AS PeriodEnd
            FROM WaterReadings r JOIN WaterSensors s ON s.Id=r.SensorId
            {wClause} GROUP BY r.SensorId, s.Name, s.Location ORDER BY TotalLiters DESC
            """, p);

        return Ok(rows.Select(r => new ConsumptionSummaryVM
        {
            SensorId = (int)(long)r["SensorId"]!,
            SensorName = r["Name"]?.ToString() ?? "",
            Location = r["Location"]?.ToString() ?? "",
            TotalLiters = (double)(r["TotalLiters"] ?? 0.0),
            AverageFlowRate = (double)(r["AvgFlow"] ?? 0.0),
            MaxFlowRate = (double)(r["MaxFlow"] ?? 0.0),
            AnomalyCount = (int)(long)(r["AnomalyCount"] ?? 0L),
            PeriodStart = r["PeriodStart"] is string ps ? DateTime.Parse(ps) : null,
            PeriodEnd   = r["PeriodEnd"]   is string pe ? DateTime.Parse(pe)   : null
        }));
    }
}
