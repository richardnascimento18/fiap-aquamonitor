using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using AquaMonitor.API.Services;

namespace AquaMonitor.API;

/// <summary>
/// Custom ASP.NET Core authentication handler that validates our hand-rolled HS256 JWTs.
/// No JwtBearer NuGet package required — uses only built-in framework types.
/// </summary>
public sealed class JwtAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITokenService _tokens;

    public JwtAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenService tokens)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = authHeader["Bearer ".Length..].Trim();
        var principal = _tokens.ValidateToken(token);
        if (principal is null)
            return Task.FromResult(AuthenticateResult.Fail("Token invalido ou expirado."));

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
