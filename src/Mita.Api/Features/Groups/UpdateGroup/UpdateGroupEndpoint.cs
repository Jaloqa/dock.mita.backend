using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UpdateGroup;

public static class UpdateGroupEndpoint
{
    public static RouteGroupBuilder MapUpdateGroup(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}",
                async (Guid id, UpdateGroupRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateGroupCommand(id, request?.Name, request?.Currency);

                    return (await mediator.Send(command, ct)).ToHttp(updated => Results.Ok(updated));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
