using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.ToConfirm.RejectItem;

public static class RejectItemEndpoint
{
    public static RouteGroupBuilder MapRejectItem(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/to-confirm/{kind}/{entryId:guid}/reject",
                async (string kind, Guid entryId, RejectItemRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new RejectItemCommand(kind, entryId, request?.Version), ct))
                        .ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
