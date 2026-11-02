using System.Security.Claims;
using AquaMonitor.Application.Abstractions;

namespace AquaMonitor.API.Authentication;

internal static class ActorExtensions
{
    public static Actor Actor(this ClaimsPrincipal principal)
        => principal.Identity?.IsAuthenticated == true && int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? new Actor(id, principal.IsInRole("Admin")) : throw new ApplicationFailure(401);
}
