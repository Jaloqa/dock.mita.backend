using Mita.Api.Common.Tags;

namespace Mita.Api.Common.Transactions;

public sealed record TransactionDto(
    Guid Id,
    string Kind,
    string Date,
    string Title,
    string? Note,
    Guid CategoryId,
    Guid AccountId,
    long Amount,
    string Currency,
    long AccountAmount,
    double Rate,
    long BaseAmount,
    IReadOnlyList<TransactionTagDto> Tags);
