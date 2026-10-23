using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public sealed record GroupRepaymentState(
    Guid FromMemberId,
    Guid ToMemberId,
    long Amount,
    string Currency,
    string Date,
    double Rate,
    bool RateManual,
    long GroupAmount,
    string? Note)
{
    public static GroupRepaymentState From(GroupRepayment repayment) =>
        new(
            repayment.FromMemberId,
            repayment.ToMemberId,
            repayment.Amount,
            CurrencyWire.ToWire(repayment.Currency),
            IsoDate.ToWire(repayment.Date),
            (double)repayment.Rate,
            repayment.RateManual,
            repayment.GroupAmount,
            repayment.Note);
}
