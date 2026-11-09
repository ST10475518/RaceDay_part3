using System.Security.Claims;

namespace RaceDay.Api.Controllers;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal p) => int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
