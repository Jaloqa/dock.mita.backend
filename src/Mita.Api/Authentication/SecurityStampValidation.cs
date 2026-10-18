using Microsoft.AspNetCore.Authentication.JwtBearer;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Authentication;

public static class SecurityStampValidation
{
    public const string SessionExpiredCode = "auth.sessionExpired";

    public static async Task ValidateAsync(TokenValidatedContext context, Guid userId)
    {
        var tokenStamp = context.Principal?.FindFirst(TokenService.StampClaim)?.Value;
        if (string.IsNullOrEmpty(tokenStamp))
        {
            context.Fail(new SessionExpiredException());
            return;
        }

        var stamps = context.HttpContext.RequestServices.GetRequiredService<SecurityStampCache>();
        var currentStamp = await stamps.GetAsync(userId, context.HttpContext.RequestAborted);
        if (string.IsNullOrEmpty(currentStamp) || !string.Equals(tokenStamp, currentStamp, StringComparison.Ordinal))
        {
            context.Fail(new SessionExpiredException());
        }
    }
}
