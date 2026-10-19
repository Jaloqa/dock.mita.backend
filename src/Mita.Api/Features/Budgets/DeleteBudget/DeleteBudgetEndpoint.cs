using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Budgets.DeleteBudget;

public static class DeleteBudgetEndpoint
{
    public static RouteGroupBuilder MapDeleteBudget(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/budgets/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteBudgetCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
