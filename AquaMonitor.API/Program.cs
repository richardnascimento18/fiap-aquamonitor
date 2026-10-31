using System.Security.Claims;
using AquaMonitor.API.Data;
using AquaMonitor.API.Filters;
using AquaMonitor.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

if (args.Contains("--provision-admin", StringComparer.Ordinal))
{
    if (args.Length != 1)
    {
        Console.Error.WriteLine("Use --provision-admin alone; supply database and credentials through private environment variables.");
        Environment.ExitCode = 1;
        return;
    }

    // Do not load application defaults or construct a web host for this offline command.
    using var configuration = new ConfigurationManager();
    configuration.AddEnvironmentVariables();
    try
    {
        AdministratorBootstrap.ProvisionOffline(configuration);
        Console.WriteLine("Administrator provisioning completed. HTTP server was not started.");
    }
    catch (InvalidOperationException exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection") ?? "aquamonitor.db";
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(dbPath));

// Validate signing configuration before serving any requests.
builder.Services.AddSingleton<JwtSettings>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSettings>((options, jwt) =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwt.SigningKey,
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            RequireAudience = true,
            ValidAudience = jwt.Audience,
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal!;
                var subjects = principal.FindAll("sub").ToArray();
                var roles = principal.FindAll("role").ToArray();
                if (subjects.Length != 1 || !int.TryParse(subjects[0].Value, out var id) || id <= 0 ||
                    roles.Length != 1 || roles[0].Value is not ("Admin" or "User"))
                    context.Fail("Token identity or role is invalid.");
                else
                {
                    // Controllers use only this trusted, validated identity.
                    var identity = (ClaimsIdentity)principal.Identity!;
                    foreach (var claim in identity.FindAll(ClaimTypes.NameIdentifier).ToArray()) identity.RemoveClaim(claim);
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }
                return Task.CompletedTask;
            }
        };
    });
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

builder.Services.AddProblemDetails();
builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.GetLeftPart(UriPartial.Authority) != origin))
            throw new InvalidOperationException("Cors:AllowedOrigins must contain exact HTTP or HTTPS origins without paths or wildcards.");
        options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0)
                policy.WithOrigins(origins).WithHeaders("Authorization", "Content-Type")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE");
            else
                policy.SetIsOriginAllowed(_ => false);
        });
    });

var app = builder.Build();

app.Services.GetRequiredService<JwtSettings>();
_ = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>().Value;

// ── Init DB ───────────────────────────────────────────────
DatabaseInitializer.Initialize(app.Services.GetRequiredService<IDbConnectionFactory>());
AdministratorBootstrap.Provision(app.Services.GetRequiredService<IDbConnectionFactory>(), app.Configuration, app.Environment);

app.UseExceptionHandler();
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
