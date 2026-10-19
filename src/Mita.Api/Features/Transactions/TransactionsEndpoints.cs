using Mita.Api.Extensions;
using Mita.Api.Features.Transactions.CreateTransaction;
using Mita.Api.Features.Transactions.GetTransaction;
using Mita.Api.Features.Transactions.ListTransactions;

namespace Mita.Api.Features.Transactions;

public static class TransactionsEndpoints
{
    public static IEndpointRouteBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Transactions")
            .MapListTransactions()
            .MapGetTransaction()
            .MapCreateTransaction();

        return app;
    }
}
