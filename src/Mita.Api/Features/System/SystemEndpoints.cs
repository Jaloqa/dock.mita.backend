using Mita.Api.Extensions;
using Mita.Api.Features.System.Ping;

namespace Mita.Api.Features.System;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1().MapGroup("/system").MapPing();

        return app;
    }
}
