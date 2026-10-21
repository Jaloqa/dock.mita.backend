using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Tags.RenameTag;

public static class RenameTagEndpoint
{
    public static RouteGroupBuilder MapRenameTag(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/tags/{id:guid}",
                async (Guid id, RenameTagRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new RenameTagCommand(id, request?.Name), ct)).ToHttp(tag => Results.Ok(tag)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
