using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Tags.ListTags;

public static class ListTagsEndpoint
{
    public static RouteGroupBuilder MapListTags(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/tags",
                async (IMediator mediator, CancellationToken ct, string? search = null, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListTagsQuery(search, page, pageSize), ct))
                        .ToHttp(tags => Results.Ok(tags)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
