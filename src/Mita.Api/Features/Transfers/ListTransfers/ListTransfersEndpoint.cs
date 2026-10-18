using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transfers.ListTransfers;

public static class ListTransfersEndpoint
{
    public static RouteGroupBuilder MapListTransfers(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/transfers",
                async (IMediator mediator, CancellationToken ct, string? accountId = null, int page = 1, int pageSize = 20) =>
                    (await mediator.Send(new ListTransfersQuery(accountId, page, pageSize), ct))
                        .ToHttp(transfers => Results.Ok(transfers)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
