using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.UpdateDebt;

public static class UpdateDebtEndpoint
{
    public static RouteGroupBuilder MapUpdateDebt(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/debts/{id:guid}",
                async (Guid id, UpdateDebtRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateDebtCommand(
                        id,
                        request?.Direction,
                        request?.Counterparty,
                        request?.Amount,
                        request?.Currency,
                        request?.Date,
                        request?.DueDate,
                        request?.AccountId,
                        request?.Rate,
                        request?.Note);

                    return (await mediator.Send(command, ct)).ToHttp(debt => Results.Ok(debt));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
