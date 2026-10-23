using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.CreateGroup;

public static class CreateGroupEndpoint
{
    public static RouteGroupBuilder MapCreateGroup(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups",
                async (CreateGroupRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateGroupCommand(request?.Name, request?.Currency, request?.Members);

                    return (await mediator.Send(command, ct))
                        .ToHttp(created => Results.Created($"/v1/groups/{created.Id}", created));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
