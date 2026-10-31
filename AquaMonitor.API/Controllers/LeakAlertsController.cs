using System.Security.Claims;
using AquaMonitor.API.Data;
using AquaMonitor.API.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LeakAlertsController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;
    public LeakAlertsController(IDbConnectionFactory factory) => _factory = factory;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private bool IsAdmin => User.IsInRole("Admin");

    private static AlertResponseVM MapAlert(Dictionary<string, object?> r) => new()
    {
        Id = (int)(long)r["Id"]!,
        Message = r["Message"]?.ToString() ?? "",
        Severity = r["SeverityLabel"]?.ToString() ?? "",
        Status = r["StatusLabel"]?.ToString() ?? "",
        TriggerFlowRate = (double)r["TriggerFlowRate"]!,
        CreatedAt = DateTime.Parse(r["CreatedAt"]?.ToString()!),
        ResolvedAt = r["ResolvedAt"] is string ra ? DateTime.Parse(ra) : null,
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

    /// <summary>Lista alertas com paginacao e filtros.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<AlertResponseVM>), 200)]
    public IActionResult GetAll([FromQuery] AlertFilterVM filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize is < 1 or > 100) filter.PageSize = 20;

        using var db = _factory.Create();
        var where = new List<string>();
        var p = new Dictionary<string, object?> { ["@uid"] = UserId, ["@limit"] = filter.PageSize, ["@offset"] = ((long)filter.Page - 1) * filter.PageSize };

        if (!IsAdmin) where.Add("s.UserId=@uid");
        if (filter.SensorId.HasValue) { where.Add("a.SensorId=@sid"); p["@sid"] = filter.SensorId.Value; }
        if (filter.Status.HasValue) { where.Add("a.Status=@st"); p["@st"] = (int)filter.Status.Value; }
        if (filter.Severity.HasValue) { where.Add("a.Severity=@sev"); p["@sev"] = (int)filter.Severity.Value; }

        var wClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM LeakAlerts a JOIN WaterSensors s ON s.Id=a.SensorId {wClause}", p)[0]["c"]!;
        var rows = db.Query($"{SelectAlerts} {wClause} ORDER BY a.CreatedAt DESC LIMIT @limit OFFSET @offset", p);

        return Ok(new PagedResultVM<AlertResponseVM>
        { Data = rows.Select(MapAlert), Page = filter.Page, PageSize = filter.PageSize, TotalCount = total });
    }

    /// <summary>Retorna alerta por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AlertResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetById(int id)
    {
        using var db = _factory.Create();
        var filter = IsAdmin ? "a.Id=@id" : "a.Id=@id AND s.UserId=@uid";
        var rows = db.Query($"{SelectAlerts} WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = UserId });
        if (rows.Count == 0) return NotFound(new { message = "Alerta nao encontrado." });
        return Ok(MapAlert(rows[0]));
    }

    /// <summary>Atualiza status de um alerta (0=Open,1=Acknowledged,2=Resolved).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(AlertResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateAlertStatusVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        using var db = _factory.Create();
        var filter = IsAdmin ? "a.Id=@id" : "a.Id=@id AND s.UserId=@uid";
        var chk = db.Query($"SELECT a.Id FROM LeakAlerts a JOIN WaterSensors s ON s.Id=a.SensorId WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = UserId });
        if (chk.Count == 0) return NotFound(new { message = "Alerta nao encontrado." });

        var resolvedAt = vm.Status == 2 ? DateTime.UtcNow.ToString("o") : null;
        db.Execute("UPDATE LeakAlerts SET Status=@st, ResolvedAt=@ra WHERE Id=@id",
            new Dictionary<string, object?> { ["@st"] = vm.Status, ["@ra"] = resolvedAt, ["@id"] = id });

        var rows = db.Query($"{SelectAlerts} WHERE a.Id=@id LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = UserId });
        return Ok(MapAlert(rows[0]));
    }

    /// <summary>Estatisticas de alertas (Admin only).</summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    public IActionResult GetStats()
    {
        using var db = _factory.Create();
        var total = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts")[0]["c"]!;
        var open = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE Status=0")[0]["c"]!;
        var ack = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE Status=1")[0]["c"]!;
        var resolved = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE Status=2")[0]["c"]!;
        var bySeverity = db.Query("SELECT Severity, COUNT(*) cnt FROM LeakAlerts GROUP BY Severity")
            .Select(r => new { severity = SevLabel((int)(long)r["Severity"]!), count = (long)r["cnt"]! });
        return Ok(new { totalAlerts = total, openAlerts = open, acknowledgedAlerts = ack, resolvedAlerts = resolved, bySeverity });
    }

    private static string SevLabel(int s) => s switch { 4 => "Critical", 3 => "High", 2 => "Medium", _ => "Low" };
}
