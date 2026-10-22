using Mita.Domain.Money;

namespace Mita.Domain.Debts;

public sealed record DebtDetails(
    DebtDirection Direction,
    string Counterparty,
    long Amount,
    Currency Currency,
    DateOnly Date,
    DateOnly? DueDate,
    Guid? AccountId,
    long? AccountAmount,
    decimal? Rate,
    string? Note);
