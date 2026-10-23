using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.LeaveGroup;

public static class LeaveGroupEndpoint
{
    public static RouteGroupBuilder MapLeaveGroup(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/membership",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new LeaveGroupCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
