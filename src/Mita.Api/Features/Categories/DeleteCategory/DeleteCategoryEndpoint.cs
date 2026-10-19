using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Categories.DeleteCategory;

public static class DeleteCategoryEndpoint
{
    public static RouteGroupBuilder MapDeleteCategory(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/categories/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteCategoryCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
