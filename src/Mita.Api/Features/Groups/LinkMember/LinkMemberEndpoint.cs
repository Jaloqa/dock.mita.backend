using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.LinkMember;

public static class LinkMemberEndpoint
{
    public static RouteGroupBuilder MapLinkMember(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/members/{memberId:guid}/link",
                async (Guid id, Guid memberId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new LinkMemberCommand(id, memberId), ct))
                        .ToHttp(member => Results.Ok(member)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
