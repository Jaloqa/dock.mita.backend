using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.RemoveMember;

public static class RemoveMemberEndpoint
{
    public static RouteGroupBuilder MapRemoveMember(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/members/{memberId:guid}",
                async (Guid id, Guid memberId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new RemoveMemberCommand(id, memberId), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
