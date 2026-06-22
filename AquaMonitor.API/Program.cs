using AquaMonitor.API;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using AquaMonitor.API.Data;
using AquaMonitor.API.Filters;
using AquaMonitor.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection") ?? "aquamonitor.db";
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(dbPath));

// ── Custom JWT Auth ───────────────────────────────────────
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddAuthentication("JWT")
    .AddScheme<AuthenticationSchemeOptions, JwtAuthHandler>("JWT", null);
builder.Services.AddAuthorization();

// ── Application Services ──────────────────────────────────
builder.Services.AddScoped<IAlertService, AlertService>();

// ── Controllers ───────────────────────────────────────────
builder.Services.AddControllers(o => o.Filters.Add<GlobalExceptionFilter>())
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(k => k.Key, k => k.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { message = "Dados invalidos.", errors });
        };
    });

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ── Init DB ───────────────────────────────────────────────
DatabaseInitializer.Initialize(app.Services.GetRequiredService<IDbConnectionFactory>());

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Minimal endpoint documentation
app.MapGet("/", () => new
{
    api = "AquaMonitor API v1",
    description = "Sistema de monitoramento inteligente de agua — ESG Tema 7",
    endpoints = new[]
    {
        "POST /api/Auth/register", "POST /api/Auth/login",
        "GET|POST|PUT|DELETE /api/WaterSensors",
        "GET|POST /api/WaterReadings", "GET /api/WaterReadings/summary",
        "GET|PATCH /api/LeakAlerts", "GET /api/LeakAlerts/stats",
        "GET|POST|DELETE /api/WaterImpactReports"
    }
});

app.Run();
public partial class Program { }
