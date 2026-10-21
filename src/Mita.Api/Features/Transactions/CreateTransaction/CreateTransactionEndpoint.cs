using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transactions.CreateTransaction;

public static class CreateTransactionEndpoint
{
    public static RouteGroupBuilder MapCreateTransaction(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/transactions",
                async (CreateTransactionRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateTransactionCommand(
                        request?.Kind,
                        request?.Date,
                        request?.Title,
                        request?.Note,
                        request?.CategoryId,
                        request?.AccountId,
                        request?.Amount,
                        request?.Currency,
                        request?.Rate,
                        request?.Tags);

                    return (await mediator.Send(command, ct))
                        .ToHttp(transaction => Results.Created($"/v1/transactions/{transaction.Id}", transaction));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
