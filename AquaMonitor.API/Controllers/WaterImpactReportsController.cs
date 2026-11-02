using AquaMonitor.API.Authentication;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WaterImpactReportsController : ControllerBase
{
    private readonly ReportService _reports;
    public WaterImpactReportsController(ReportService reports) => _reports = reports;

    /// <summary>Lista relatorios com paginacao.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<ReportResponseVM>), 200)]
    public IActionResult GetAll([FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 50)] int pageSize = 10)
    {
        return Ok(_reports.List(User.Actor(), page, pageSize));
    }

    /// <summary>Retorna relatorio por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ReportResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetById(int id)
    {
        return Ok(_reports.Get(User.Actor(), id));
    }

    /// <summary>Gera relatorio de impacto hidrico para o mes/ano informado. (Admin only)</summary>
    [HttpPost("generate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReportResponseVM), 201)]
    [ProducesResponseType(400)]
    public IActionResult Generate([FromBody] GenerateReportVM vm)
    {
        return StatusCode(201, _reports.Generate(User.Actor(), vm));
    }

    /// <summary>Remove relatorio (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public IActionResult Delete(int id)
    {
        _reports.Delete(User.Actor(), id);
        return NoContent();
    }
}
