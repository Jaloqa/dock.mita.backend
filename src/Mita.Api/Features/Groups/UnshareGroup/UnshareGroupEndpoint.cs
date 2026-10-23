using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UnshareGroup;

public static class UnshareGroupEndpoint
{
    public static RouteGroupBuilder MapUnshareGroup(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/share",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new UnshareGroupCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
