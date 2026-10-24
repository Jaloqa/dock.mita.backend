using Mita.Api.Extensions;
using Mita.Api.Features.ToConfirm.ListToConfirm;

namespace Mita.Api.Features.ToConfirm;

public static class ToConfirmEndpoints
{
    public static IEndpointRouteBuilder MapToConfirmEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("To confirm")
            .MapListToConfirm();

        return app;
    }
}
