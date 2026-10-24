using System.Text.Json;
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
    string? Note,
    IReadOnlyList<Guid> Settles);

public static class GroupRepaymentInputParser
{
    public const int MaxSettles = 50;

    public static ParsedGroupRepayment Parse(IGroupRepaymentInput input)
    {
        if (!JsonValues.TryGetGuid(input.FromMemberId, out var fromMemberId)
            || !JsonValues.TryGetGuid(input.ToMemberId, out var toMemberId)
            || !JsonValues.TryGetMinorAmount(input.Amount, out var amount)
            || !CurrencyWire.TryParse(input.Currency, out var currency)
            || !JsonValues.TryGetIsoDate(input.Date, out var date)
            || !JsonValues.TryGetOptionalText(input.Note, out var note)
            || !TryParseSettles(input.Settles, out var settles))
        {
            throw new InvalidOperationException("The repayment input reached the handler without passing validation.");
        }

        decimal? manualRate = ManualRate.TryParse(input.Rate, out var rate) ? rate : null;
        return new ParsedGroupRepayment(fromMemberId, toMemberId, amount, currency, date, manualRate, note, settles);
    }

    public static bool TryParseSettles(JsonElement? element, out IReadOnlyList<Guid> settles)
    {
        settles = [];
        if (element is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return true;
        }

        if (element is not { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() > MaxSettles)
        {
            return false;
        }

        var expenseIds = new List<Guid>();
        foreach (var item in array.EnumerateArray())
        {
            if (!JsonValues.TryGetGuid(item, out var expenseId) || expenseIds.Contains(expenseId))
            {
                return false;
            }

            expenseIds.Add(expenseId);
        }

        settles = expenseIds;
        return true;
    }
}
