using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Tags.DeleteTag;

public static class DeleteTagEndpoint
{
    public static RouteGroupBuilder MapDeleteTag(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/tags/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteTagCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
