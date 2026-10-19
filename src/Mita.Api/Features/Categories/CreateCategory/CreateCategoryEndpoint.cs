using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Categories.CreateCategory;

public static class CreateCategoryEndpoint
{
    public static RouteGroupBuilder MapCreateCategory(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/categories",
                async (CreateCategoryRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateCategoryCommand(request?.Name, request?.Kind, request?.Color);

                    return (await mediator.Send(command, ct))
                        .ToHttp(category => Results.Created($"/v1/categories/{category.Id}", category));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
