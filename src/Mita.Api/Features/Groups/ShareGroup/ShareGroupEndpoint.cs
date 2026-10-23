using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.ShareGroup;

public static class ShareGroupEndpoint
{
    public static RouteGroupBuilder MapShareGroup(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/share",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new ShareGroupCommand(id), ct)).ToHttp(shared => Results.Ok(shared)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
