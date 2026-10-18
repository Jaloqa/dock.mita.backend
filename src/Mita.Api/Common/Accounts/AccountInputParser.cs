using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;
using Mita.Domain.Money;

namespace Mita.Api.Common.Accounts;

public sealed record ParsedAccount(
    string Name,
    AccountType Type,
    Currency Currency,
    long OpeningBalance,
    string? CardLast4,
    bool? Archived);

public static class AccountInputParser
{
    public static ParsedAccount Parse(IAccountInput input)
    {
        if (!JsonValues.TryGetString(input.Name, out var name)
            || !AccountWire.TryParse(input.Type, out var type)
            || !CurrencyWire.TryParse(input.Currency, out var currency)
            || !JsonValues.TryGetMinorAmount(input.OpeningBalance, out var openingBalance))
        {
            throw new InvalidOperationException("The account input reached the handler without passing validation.");
        }

        var cardLast4 = JsonValues.TryGetString(input.CardLast4, out var digits) && !string.IsNullOrWhiteSpace(digits)
            ? digits
            : null;
        bool? archived = JsonValues.TryGetBoolean(input.Archived, out var flag) ? flag : null;

        return new ParsedAccount(name.Trim(), type, currency, openingBalance, cardLast4, archived);
    }
}
