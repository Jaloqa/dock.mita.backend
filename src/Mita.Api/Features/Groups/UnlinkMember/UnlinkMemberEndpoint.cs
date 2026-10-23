using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UnlinkMember;

public static class UnlinkMemberEndpoint
{
    public static RouteGroupBuilder MapUnlinkMember(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/members/{memberId:guid}/link",
                async (Guid id, Guid memberId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new UnlinkMemberCommand(id, memberId), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
