using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Wire;
using Mita.Domain.Money;

namespace Mita.Api.Common.Groups;

public sealed record ParsedGroupRepayment(
    Guid FromMemberId,
    Guid ToMemberId,
    long Amount,
    Currency Currency,
    DateOnly Date,
    decimal? ManualRate,
    string? Note);

public static class GroupRepaymentInputParser
{
    public static ParsedGroupRepayment Parse(IGroupRepaymentInput input)
    {
        if (!JsonValues.TryGetGuid(input.FromMemberId, out var fromMemberId)
            || !JsonValues.TryGetGuid(input.ToMemberId, out var toMemberId)
            || !JsonValues.TryGetMinorAmount(input.Amount, out var amount)
            || !CurrencyWire.TryParse(input.Currency, out var currency)
            || !JsonValues.TryGetIsoDate(input.Date, out var date)
            || !JsonValues.TryGetOptionalText(input.Note, out var note))
        {
            throw new InvalidOperationException("The repayment input reached the handler without passing validation.");
        }

        decimal? manualRate = ManualRate.TryParse(input.Rate, out var rate) ? rate : null;
        return new ParsedGroupRepayment(fromMemberId, toMemberId, amount, currency, date, manualRate, note);
    }
}
