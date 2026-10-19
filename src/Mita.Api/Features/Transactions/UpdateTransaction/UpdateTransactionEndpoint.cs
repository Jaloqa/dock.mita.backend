using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transactions.UpdateTransaction;

public static class UpdateTransactionEndpoint
{
    public static RouteGroupBuilder MapUpdateTransaction(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/transactions/{id:guid}",
                async (Guid id, UpdateTransactionRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateTransactionCommand(
                        id,
                        request?.Kind,
                        request?.Date,
                        request?.Title,
                        request?.Note,
                        request?.CategoryId,
                        request?.AccountId,
                        request?.Amount,
                        request?.Currency,
                        request?.Rate);

                    return (await mediator.Send(command, ct)).ToHttp(transaction => Results.Ok(transaction));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
