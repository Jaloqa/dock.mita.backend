using Mita.Domain.Categories;
using Mita.Domain.Money;

namespace Mita.Domain.Transactions;

public sealed record TransactionDetails(
    CategoryKind Kind,
    DateOnly Date,
    string Title,
    string? Note,
    Guid CategoryId,
    Guid AccountId,
    long Amount,
    Currency Currency,
    decimal Rate,
    long AccountAmount,
    long BaseAmount);
