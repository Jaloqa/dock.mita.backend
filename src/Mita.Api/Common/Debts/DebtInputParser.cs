using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Wire;
using Mita.Domain.Debts;
using Mita.Domain.Money;

namespace Mita.Api.Common.Debts;

public sealed record ParsedDebt(
    DebtDirection Direction,
    string Counterparty,
    long Amount,
    Currency Currency,
    DateOnly Date,
    DateOnly? DueDate,
    Guid? AccountId,
    decimal? ManualRate,
    string? Note);

public static class DebtInputParser
{
    private const string NotValidated = "The debt input reached the handler without passing validation.";

    public static ParsedDebt Parse(IDebtInput input)
    {
        if (!DebtWire.TryParseDirection(input.Direction, out var direction)
            || !JsonValues.TryGetOptionalText(input.Counterparty, out var counterparty)
            || counterparty is null
            || counterparty.Length > Debt.CounterpartyMaxLength
            || !JsonValues.TryGetMinorAmount(input.Amount, out var amount)
            || !CurrencyWire.TryParse(input.Currency, out var currency)
            || !JsonValues.TryGetIsoDate(input.Date, out var date)
            || !JsonValues.TryGetOptionalText(input.Note, out var note))
        {
            throw new InvalidOperationException(NotValidated);
        }

        DateOnly? dueDate = null;
        if (!JsonValues.IsMissing(input.DueDate))
        {
            dueDate = JsonValues.TryGetIsoDate(input.DueDate, out var due) ? due : throw new InvalidOperationException(NotValidated);
        }

        Guid? accountId = null;
        if (!JsonValues.IsMissing(input.AccountId))
        {
            accountId = JsonValues.TryGetGuid(input.AccountId, out var id) ? id : throw new InvalidOperationException(NotValidated);
        }

        decimal? manualRate = ManualRate.TryParse(input.Rate, out var rate) ? rate : null;
        return new ParsedDebt(direction, counterparty, amount, currency, date, dueDate, accountId, manualRate, note);
    }
}
