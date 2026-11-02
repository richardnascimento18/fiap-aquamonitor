using AquaMonitor.API.Authentication;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Application.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WaterSensorsController : ControllerBase
{
    private readonly SensorService _sensors;
    public WaterSensorsController(SensorService sensors) => _sensors = sensors;

    /// <summary>Lista sensores com paginacao.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultVM<SensorResponseVM>), 200)]
    public IActionResult GetAll([FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 10)
    {
        return Ok(_sensors.List(User.Actor(), page, pageSize));
    }

    /// <summary>Retorna sensor por ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SensorResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult GetById(int id)
    {
        return Ok(_sensors.Get(User.Actor(), id));
    }

    /// <summary>Cria novo sensor.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SensorResponseVM), 201)]
    [ProducesResponseType(400)]
    public IActionResult Create([FromBody] CreateSensorVM vm)
    {
        return StatusCode(201, _sensors.Create(User.Actor(), vm));
    }

    /// <summary>Atualiza sensor existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SensorResponseVM), 200)]
    [ProducesResponseType(404)]
    public IActionResult Update(int id, [FromBody] UpdateSensorVM vm)
    {
        return Ok(_sensors.Update(User.Actor(), id, vm));
    }

    /// <summary>Remove sensor (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public IActionResult Delete(int id)
    {
        _sensors.Delete(User.Actor(), id);
        return NoContent();
    }

}
