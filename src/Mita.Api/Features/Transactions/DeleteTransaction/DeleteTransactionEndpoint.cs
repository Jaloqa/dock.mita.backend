using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transactions.DeleteTransaction;

public static class DeleteTransactionEndpoint
{
    public static RouteGroupBuilder MapDeleteTransaction(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/transactions/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteTransactionCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
