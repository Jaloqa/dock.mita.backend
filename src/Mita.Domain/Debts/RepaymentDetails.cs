using Mita.Domain.Money;

namespace Mita.Domain.Debts;

public sealed record RepaymentDetails(
    DateOnly Date,
    long Amount,
    Currency Currency,
    decimal Rate,
    long DebtAmount,
    Guid? AccountId,
    long? AccountAmount,
    string? Note);
