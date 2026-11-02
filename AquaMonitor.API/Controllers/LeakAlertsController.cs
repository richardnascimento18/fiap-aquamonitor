using AquaMonitor.API.Authentication;
using AquaMonitor.Application.Alerts;
using AquaMonitor.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LeakAlertsController : ControllerBase
{
    private readonly AlertService _alerts;
    public LeakAlertsController(AlertService alerts) => _alerts = alerts;

    /// <summary>Lista alertas com paginacao e filtros.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<AlertResponseVM>), 200)]
    public IActionResult GetAll([FromQuery] AlertFilterVM filter)
    {
        return Ok(_alerts.List(User.Actor(), filter));
    }

    /// <summary>Retorna alerta por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AlertResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetById(int id)
    {
        return Ok(_alerts.Get(User.Actor(), id));
    }

    /// <summary>Atualiza status de um alerta (0=Open,1=Acknowledged,2=Resolved).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(AlertResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateAlertStatusVM vm)
    {
        return Ok(_alerts.UpdateStatus(User.Actor(), id, vm));
    }

    /// <summary>Estatisticas de alertas (Admin only).</summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    public IActionResult GetStats()
    {
        return Ok(_alerts.Statistics(User.Actor()));
    }

}
