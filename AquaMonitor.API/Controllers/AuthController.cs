using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using AquaMonitor.API.Data;
using AquaMonitor.API.Services;
using AquaMonitor.API.ViewModels;

namespace AquaMonitor.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;
    private readonly ITokenService _tokens;

    public AuthController(IDbConnectionFactory factory, ITokenService tokens)
    {
        _factory = factory;
        _tokens  = tokens;
    }

    /// <summary>Registra novo usuario no sistema.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseVM), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public IActionResult Register([FromBody] RegisterVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var db = _factory.Create();
        var exists = db.Query("SELECT 1 FROM Users WHERE Email=@e LIMIT 1",
            new Dictionary<string, object?> { ["@e"] = vm.Email.ToLower() });
        if (exists.Count > 0) return Conflict(new { message = "E-mail ja cadastrado." });

        var hash = PasswordHelper.HashPassword(vm.Password);
        db.Execute("INSERT INTO Users (Name,Email,PasswordHash,Role,CreatedAt) VALUES (@n,@e,@h,'User',@c)",
            new Dictionary<string, object?> { ["@n"]=vm.Name, ["@e"]=vm.Email.ToLower(), ["@h"]=hash, ["@c"]=DateTime.UtcNow.ToString("o") });
        var id = (int)db.LastInsertRowId();

        var token = _tokens.GenerateToken(id, vm.Email, vm.Name, "User");
        return StatusCode(201, new AuthResponseVM { Token=token, Name=vm.Name, Email=vm.Email.ToLower(), Role="User", ExpiresAt=_tokens.GetExpiration() });
    }

    /// <summary>Autentica usuario e retorna JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseVM), 200)]
    [ProducesResponseType(401)]
    public IActionResult Login([FromBody] LoginVM vm)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        using var db = _factory.Create();
        var rows = db.Query("SELECT Id,Name,Email,PasswordHash,Role FROM Users WHERE Email=@e LIMIT 1",
            new Dictionary<string, object?> { ["@e"] = vm.Email.ToLower() });
        if (rows.Count == 0) return Unauthorized(new { message = "Credenciais invalidas." });

        var row = rows[0];
        var hash = row["PasswordHash"]?.ToString() ?? "";
        if (!PasswordHelper.VerifyPassword(vm.Password, hash)) return Unauthorized(new { message = "Credenciais invalidas." });

        var id    = (int)(long)row["Id"]!;
        var name  = row["Name"]?.ToString() ?? "";
        var email = row["Email"]?.ToString() ?? "";
        var role  = row["Role"]?.ToString() ?? "User";

        var token = _tokens.GenerateToken(id, email, name, role);
        return Ok(new AuthResponseVM { Token=token, Name=name, Email=email, Role=role, ExpiresAt=_tokens.GetExpiration() });
    }
}
