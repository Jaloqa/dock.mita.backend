using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Transactions.ListTransactions;

public sealed record ListTransactionsQuery(
    string? Search,
    string? CategoryId,
    string? AccountId,
    string? Kind,
    string? From,
    string? To,
    string? Sort,
    int Page,
    int PageSize) : IRequest<Result<TransactionPageResponse>>;
