using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transactions.GetTransaction;

public static class GetTransactionEndpoint
{
    public static RouteGroupBuilder MapGetTransaction(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/transactions/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new GetTransactionQuery(id), ct)).ToHttp(transaction => Results.Ok(transaction)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
