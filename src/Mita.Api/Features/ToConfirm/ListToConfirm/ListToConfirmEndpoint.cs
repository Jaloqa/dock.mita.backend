using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.ToConfirm.ListToConfirm;

public static class ListToConfirmEndpoint
{
    public static RouteGroupBuilder MapListToConfirm(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/to-confirm",
                async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListToConfirmQuery(page, pageSize), ct))
                        .ToHttp(items => Results.Ok(items)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
