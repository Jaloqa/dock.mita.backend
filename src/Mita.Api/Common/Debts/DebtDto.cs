namespace Mita.Api.Common.Debts;

public sealed record DebtDto(
    Guid Id,
    string Direction,
    string Counterparty,
    long Amount,
    string Currency,
    long Repaid,
    long Remaining,
    string Status,
    bool Overdue,
    string Date,
    string? DueDate,
    Guid? AccountId,
    long? AccountAmount,
    double? Rate,
    string? Note,
    int Repayments);
