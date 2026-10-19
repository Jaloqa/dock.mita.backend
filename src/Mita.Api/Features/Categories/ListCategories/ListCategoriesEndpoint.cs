using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Categories.ListCategories;

public static class ListCategoriesEndpoint
{
    public static RouteGroupBuilder MapListCategories(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/categories",
                async (IMediator mediator, CancellationToken ct, string? kind = null, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListCategoriesQuery(kind, page, pageSize), ct))
                        .ToHttp(categories => Results.Ok(categories)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
