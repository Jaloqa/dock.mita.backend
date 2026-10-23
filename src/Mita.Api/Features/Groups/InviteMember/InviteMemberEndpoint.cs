using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.InviteMember;

public static class InviteMemberEndpoint
{
    public static RouteGroupBuilder MapInviteMember(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/members/{memberId:guid}/invite",
                async (Guid id, Guid memberId, InviteMemberRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new InviteMemberCommand(id, memberId, request?.Email), ct))
                        .ToHttp(member => Results.Ok(member)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
