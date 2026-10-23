using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.GroupInvites.DeclineGroupInvite;

public static class DeclineGroupInviteEndpoint
{
    public static RouteGroupBuilder MapDeclineGroupInvite(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/group-invites/{memberId:guid}/decline",
                async (Guid memberId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeclineGroupInviteCommand(memberId), ct))
                        .ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
