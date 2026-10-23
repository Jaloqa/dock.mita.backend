using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.DeleteDebt;

public static class DeleteDebtEndpoint
{
    public static RouteGroupBuilder MapDeleteDebt(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/debts/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteDebtCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
