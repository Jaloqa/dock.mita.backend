using Mita.Domain.Money;

namespace Mita.Domain.Groups;

public sealed record GroupRepaymentDetails(
    Guid FromMemberId,
    Guid ToMemberId,
    long Amount,
    Currency Currency,
    DateOnly Date,
    decimal Rate,
    bool RateManual,
    long GroupAmount,
    string? Note);
