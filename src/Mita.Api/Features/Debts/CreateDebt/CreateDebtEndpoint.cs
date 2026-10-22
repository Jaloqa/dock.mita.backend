using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.CreateDebt;

public static class CreateDebtEndpoint
{
    public static RouteGroupBuilder MapCreateDebt(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/debts",
                async (CreateDebtRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateDebtCommand(
                        request?.Direction,
                        request?.Counterparty,
                        request?.Amount,
                        request?.Currency,
                        request?.Date,
                        request?.DueDate,
                        request?.AccountId,
                        request?.Rate,
                        request?.Note);

                    return (await mediator.Send(command, ct))
                        .ToHttp(debt => Results.Created($"/v1/debts/{debt.Id}", debt));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
