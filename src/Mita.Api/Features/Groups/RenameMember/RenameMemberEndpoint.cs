using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.RenameMember;

public static class RenameMemberEndpoint
{
    public static RouteGroupBuilder MapRenameMember(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/members/{memberId:guid}",
                async (Guid id, Guid memberId, RenameMemberRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new RenameMemberCommand(id, memberId, request?.Name), ct))
                        .ToHttp(member => Results.Ok(member)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
