using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transactions.ListTransactions;

public static class ListTransactionsEndpoint
{
    public static RouteGroupBuilder MapListTransactions(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/transactions",
                async (
                    IMediator mediator,
                    CancellationToken ct,
                    string? search = null,
                    string? categoryId = null,
                    string? accountId = null,
                    string? kind = null,
                    string? from = null,
                    string? to = null,
                    string? sort = null,
                    int page = 1,
                    int pageSize = 20) =>
                    (await mediator.Send(
                        new ListTransactionsQuery(search, categoryId, accountId, kind, from, to, sort, page, pageSize),
                        ct))
                        .ToHttp(transactions => Results.Ok(transactions)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
