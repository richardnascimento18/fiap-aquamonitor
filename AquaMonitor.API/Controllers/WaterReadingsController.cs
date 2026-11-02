using AquaMonitor.API.Authentication;
using AquaMonitor.Application.Readings;
using AquaMonitor.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WaterReadingsController : ControllerBase
{
    private readonly ReadingService _readings;
    public WaterReadingsController(ReadingService readings) => _readings = readings;

    /// <summary>Lista leituras com paginacao e filtros.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<ReadingResponseVM>), 200)]
    public IActionResult GetAll([FromQuery] ReadingFilterVM filter)
    {
        return Ok(_readings.List(User.Actor(), filter));
    }

    /// <summary>Registra nova leitura. Dispara alerta automatico se vazao exceder limiar.</summary>
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public IActionResult Create([FromBody] CreateReadingVM vm)
    {
        return StatusCode(201, _readings.Create(User.Actor(), vm));
    }

    /// <summary>Resumo de consumo por sensor em um periodo.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<ConsumptionSummaryVM>), 200)]
    public IActionResult GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? sensorId)
    {
        return Ok(_readings.Summary(User.Actor(), from, to, sensorId));
    }
}
