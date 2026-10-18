using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Mita.Api.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id =>
        TryReadId(accessor.HttpContext?.User, out var id)
            ? id
            : throw new UnauthorizedAccessException("The current request has no valid user identity.");

    internal static bool TryReadId(ClaimsPrincipal? principal, out Guid id)
    {
        id = Guid.Empty;
        return Guid.TryParse(principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out id) && id != Guid.Empty;
    }
}
