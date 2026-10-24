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
    string? Note,
    IReadOnlyList<GroupCoverDto> Covers)
{
    // States recorded before repayments had covers carry no "covers" property.
    public IReadOnlyList<GroupCoverDto> Covers { get; init; } = Covers ?? [];

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
            repayment.Note,
            [
                .. repayment.Covers
                    .OrderBy(cover => cover.Position)
                    .Select(cover => new GroupCoverDto(cover.ExpenseId, cover.GroupAmount))
            ]);
}
