using Mita.Api.Extensions;
using Mita.Api.Features.ToConfirm.ConfirmItem;
using Mita.Api.Features.ToConfirm.ListToConfirm;
using Mita.Api.Features.ToConfirm.RejectItem;

namespace Mita.Api.Features.ToConfirm;

public static class ToConfirmEndpoints
{
    public static IEndpointRouteBuilder MapToConfirmEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("To confirm")
            .MapListToConfirm()
            .MapConfirmItem()
            .MapRejectItem();

        return app;
    }
}
