using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.GetGroup;

public static class GetGroupEndpoint
{
    public static RouteGroupBuilder MapGetGroup(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new GetGroupQuery(new GroupScope(id)), ct)).ToHttp(found => Results.Ok(found)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
