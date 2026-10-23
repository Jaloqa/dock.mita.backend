using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.DeleteGroup;

public static class DeleteGroupEndpoint
{
    public static RouteGroupBuilder MapDeleteGroup(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteGroupCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
