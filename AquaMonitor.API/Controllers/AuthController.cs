using AquaMonitor.Application.Authentication;
using AquaMonitor.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AuthenticationService _authentication;
    public AuthController(AuthenticationService authentication) => _authentication = authentication;

    /// <summary>Registra novo usuario no sistema.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseVM), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public IActionResult Register([FromBody] RegisterVM vm)
    {
        return StatusCode(201, _authentication.Register(vm));
    }

    /// <summary>Autentica usuario e retorna JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseVM), 200)]
    [ProducesResponseType(401)]
    public IActionResult Login([FromBody] LoginVM vm)
    {
        return Ok(_authentication.Login(vm));
    }
}
