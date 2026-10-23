using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.AddCategory;

public static class AddCategoryEndpoint
{
    public static RouteGroupBuilder MapAddCategory(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/categories",
                async (Guid id, AddCategoryRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new AddCategoryCommand(GroupScope.Owner(id), request?.Name), ct))
                        .ToHttp(category => Results.Created((string?)null, category)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
