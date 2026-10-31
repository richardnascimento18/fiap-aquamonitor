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
public class WaterImpactReportsController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;
    public WaterImpactReportsController(IDbConnectionFactory factory) => _factory = factory;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private bool IsAdmin => User.IsInRole("Admin");

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
        GeneratedAt = DateTime.Parse(r["GeneratedAt"]?.ToString()!),
        GeneratedByUser = r["GeneratedByUser"]?.ToString() ?? ""
    };

    private const string SelectReports = """
        SELECT r.Id, r.Title, r.Year, r.Month, r.TotalConsumptionM3, r.SavedWaterM3,
               r.LeaksDetected, r.LeaksResolved, r.ReductionPercent, r.MitigationPlan,
               r.GeneratedAt, u.Name AS GeneratedByUser
        FROM WaterImpactReports r JOIN Users u ON u.Id=r.GeneratedByUserId
        """;

    /// <summary>Lista relatorios com paginacao.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<ReportResponseVM>), 200)]
    public IActionResult GetAll([FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 50)] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 10;

        using var db = _factory.Create();
        var filter = IsAdmin ? "" : "WHERE r.GeneratedByUserId=@uid";
        var p = new Dictionary<string, object?> { ["@uid"] = UserId, ["@limit"] = pageSize, ["@offset"] = ((long)page - 1) * pageSize };

        var total = (int)(long)db.Query($"SELECT COUNT(*) c FROM WaterImpactReports r {filter}", p)[0]["c"]!;
        var rows = db.Query($"{SelectReports} {filter} ORDER BY r.GeneratedAt DESC LIMIT @limit OFFSET @offset", p);

        return Ok(new PagedResultVM<ReportResponseVM>
        { Data = rows.Select(MapReport), Page = page, PageSize = pageSize, TotalCount = total });
    }

    /// <summary>Retorna relatorio por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ReportResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetById(int id)
    {
        using var db = _factory.Create();
        var filter = IsAdmin ? "r.Id=@id" : "r.Id=@id AND r.GeneratedByUserId=@uid";
        var rows = db.Query($"{SelectReports} WHERE {filter} LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id, ["@uid"] = UserId });
        if (rows.Count == 0) return NotFound(new { message = "Relatorio nao encontrado." });
        return Ok(MapReport(rows[0]));
    }

    /// <summary>Gera relatorio de impacto hidrico para o mes/ano informado. (Admin only)</summary>
    [HttpPost("generate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReportResponseVM), 201)]
    [ProducesResponseType(400)]
    public IActionResult Generate([FromBody] GenerateReportVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var db = _factory.Create();
        var start = new DateTime(vm.Year, vm.Month, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var end = new DateTime(vm.Year, vm.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1).ToString("o");
        var prevStart = new DateTime(vm.Year, vm.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1).ToString("o");

        var p = new Dictionary<string, object?> { ["@s"] = start, ["@e"] = end, ["@ps"] = prevStart };

        var totalLiters = db.Query("SELECT COALESCE(SUM(LitersConsumed),0.0) v FROM WaterReadings WHERE RecordedAt>=@s AND RecordedAt<=@e", p);
        var tl = Convert.ToDouble(totalLiters[0]["v"] ?? 0.0);

        var prevLiters = db.Query("SELECT COALESCE(SUM(LitersConsumed),0.0) v FROM WaterReadings WHERE RecordedAt>=@ps AND RecordedAt<@s", p);
        var pl = Convert.ToDouble(prevLiters[0]["v"] ?? 0.0);

        var saved = pl - tl;
        var reduction = pl > 0 ? Math.Round(saved / pl * 100, 2) : 0;

        if (!double.IsFinite(tl) || !double.IsFinite(pl) || !double.IsFinite(saved) || !double.IsFinite(reduction))
            return BadRequest(new { message = "Consumo fora do intervalo suportado para calculo." });

        var leaksDetected = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE CreatedAt>=@s AND CreatedAt<=@e", p)[0]["c"]!;
        var leaksResolved = (long)db.Query("SELECT COUNT(*) c FROM LeakAlerts WHERE ResolvedAt>=@s AND ResolvedAt<=@e", p)[0]["c"]!;

        var monthName = new DateTime(vm.Year, vm.Month, 1)
            .ToString("MMMM/yyyy", new System.Globalization.CultureInfo("pt-BR"));
        var title = $"Relatorio de Impacto Hidrico — {monthName}";

        var now = DateTime.UtcNow.ToString("o");
        db.Execute("""
            INSERT INTO WaterImpactReports
              (Title,Year,Month,TotalConsumptionM3,SavedWaterM3,LeaksDetected,LeaksResolved,
               ReductionPercent,MitigationPlan,GeneratedAt,GeneratedByUserId)
            VALUES (@t,@yr,@mo,@tc,@sw,@ld,@lr,@rp,@mp,@ga,@uid)
            """,
            new Dictionary<string, object?>
            {
                ["@t"] = title,
                ["@yr"] = vm.Year,
                ["@mo"] = vm.Month,
                ["@tc"] = Math.Round(tl / 1000, 3),
                ["@sw"] = Math.Round(saved / 1000, 3),
                ["@ld"] = leaksDetected,
                ["@lr"] = leaksResolved,
                ["@rp"] = reduction,
                ["@mp"] = vm.MitigationPlan,
                ["@ga"] = now,
                ["@uid"] = UserId
            });
        var newId = (int)db.LastInsertRowId();

        var rows = db.Query($"{SelectReports} WHERE r.Id=@id LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = newId });
        return StatusCode(201, MapReport(rows[0]));
    }

    /// <summary>Remove relatorio (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public IActionResult Delete(int id)
    {
        using var db = _factory.Create();
        var exists = db.Query("SELECT 1 FROM WaterImpactReports WHERE Id=@id LIMIT 1",
            new Dictionary<string, object?> { ["@id"] = id });
        if (exists.Count == 0) return NotFound(new { message = "Relatorio nao encontrado." });
        db.Execute("DELETE FROM WaterImpactReports WHERE Id=@id", new Dictionary<string, object?> { ["@id"] = id });
        return NoContent();
    }
}
