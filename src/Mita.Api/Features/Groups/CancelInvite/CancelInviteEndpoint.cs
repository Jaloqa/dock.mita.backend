using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.CancelInvite;

public static class CancelInviteEndpoint
{
    public static RouteGroupBuilder MapCancelInvite(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/members/{memberId:guid}/invite",
                async (Guid id, Guid memberId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new CancelInviteCommand(id, memberId), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
