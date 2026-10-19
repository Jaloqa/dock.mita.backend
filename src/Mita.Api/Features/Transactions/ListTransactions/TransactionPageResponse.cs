using Mita.Api.Common.Transactions;

namespace Mita.Api.Features.Transactions.ListTransactions;

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionDto> Items,
    int Page,
    int PageSize,
    int Total,
    long Sum,
    string Currency);
