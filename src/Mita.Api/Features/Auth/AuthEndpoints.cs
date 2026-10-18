using Mita.Api.Extensions;
using Mita.Api.Features.Auth.Login;
using Mita.Api.Features.Auth.Register;

namespace Mita.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .MapGroup("/auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .MapRegister()
            .MapLogin();

        return app;
    }
}
