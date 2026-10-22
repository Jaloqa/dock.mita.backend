namespace Mita.Api.Features.Debts.ListRepayments;

public sealed record RepaymentResponse(
    Guid Id,
    string Date,
    long Amount,
    string Currency,
    double Rate,
    long DebtAmount,
    Guid? AccountId,
    long? AccountAmount,
    string? Note);
