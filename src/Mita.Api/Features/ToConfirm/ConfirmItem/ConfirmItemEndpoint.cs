using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.ToConfirm.ConfirmItem;

public static class ConfirmItemEndpoint
{
    public static RouteGroupBuilder MapConfirmItem(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/to-confirm/{kind}/{entryId:guid}/confirm",
                async (string kind, Guid entryId, ConfirmItemRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new ConfirmItemCommand(
                        kind,
                        entryId,
                        request?.Version,
                        request?.AccountId,
                        request?.Rate,
                        request?.Lines);

                    return (await mediator.Send(command, ct)).ToHttp(() => Results.NoContent());
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
