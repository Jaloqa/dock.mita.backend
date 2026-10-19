using Mita.Api.Common.Categories;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Domain.Money;

namespace Mita.Api.Common.Transactions;

public sealed record ParsedTransaction(
    CategoryKind Kind,
    DateOnly Date,
    string Title,
    string? Note,
    Guid CategoryId,
    Guid AccountId,
    long Amount,
    Currency Currency,
    decimal? ManualRate);

public static class TransactionInputParser
{
    public static ParsedTransaction Parse(ITransactionInput input)
    {
        if (!CategoryWire.TryParse(input.Kind, out CategoryKind kind)
            || !JsonValues.TryGetIsoDate(input.Date, out var date)
            || !JsonValues.TryGetOptionalText(input.Title, out var title)
            || !JsonValues.TryGetOptionalText(input.Note, out var note)
            || !JsonValues.TryGetGuid(input.CategoryId, out var categoryId)
            || !JsonValues.TryGetGuid(input.AccountId, out var accountId)
            || !JsonValues.TryGetMinorAmount(input.Amount, out var amount)
            || !CurrencyWire.TryParse(input.Currency, out var currency))
        {
            throw new InvalidOperationException("The transaction input reached the handler without passing validation.");
        }

        decimal? manualRate = ManualRate.TryParse(input.Rate, out var rate) ? rate : null;
        return new ParsedTransaction(kind, date, title ?? string.Empty, note, categoryId, accountId, amount, currency, manualRate);
    }
}
