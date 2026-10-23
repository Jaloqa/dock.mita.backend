using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.GroupInvites.AcceptGroupInvite;

public static class AcceptGroupInviteEndpoint
{
    public static RouteGroupBuilder MapAcceptGroupInvite(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/group-invites/{memberId:guid}/accept",
                async (Guid memberId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new AcceptGroupInviteCommand(memberId), ct))
                        .ToHttp(summary => Results.Ok(summary)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
