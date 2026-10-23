using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public sealed record GroupExpenseState(
    Guid PayerId,
    long Amount,
    string Currency,
    string Date,
    string CategoryId,
    IReadOnlyList<string> Tags,
    string? Description,
    double Rate,
    bool RateManual,
    long GroupAmount,
    string Split,
    IReadOnlyList<GroupShareDto> Shares)
{
    public static GroupExpenseState From(GroupExpense expense, IReadOnlyList<Guid> memberOrder)
    {
        var positions = memberOrder.Select((memberId, position) => (memberId, position))
            .ToDictionary(member => member.memberId, member => member.position);

        return new GroupExpenseState(
            expense.PayerId,
            expense.Amount,
            CurrencyWire.ToWire(expense.Currency),
            IsoDate.ToWire(expense.Date),
            GroupWire.ToWireCategory(expense.StandardCategory, expense.CategoryId),
            [.. expense.Tags],
            expense.Description,
            (double)expense.Rate,
            expense.RateManual,
            expense.GroupAmount,
            GroupWire.ToWire(expense.Split),
            [
                .. expense.Shares
                    .OrderBy(share => positions[share.MemberId])
                    .Select(share => new GroupShareDto(share.MemberId, share.Amount, share.GroupAmount))
            ]);
    }
}
