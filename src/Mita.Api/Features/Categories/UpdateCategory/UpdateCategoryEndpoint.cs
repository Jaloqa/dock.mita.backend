using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Categories.UpdateCategory;

public static class UpdateCategoryEndpoint
{
    public static RouteGroupBuilder MapUpdateCategory(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/categories/{id:guid}",
                async (Guid id, UpdateCategoryRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateCategoryCommand(id, request?.Name, request?.Kind, request?.Color);

                    return (await mediator.Send(command, ct)).ToHttp(category => Results.Ok(category));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
