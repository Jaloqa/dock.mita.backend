using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Domain.Money;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Transactions;

public sealed class TransactionWriter(AppDbContext db, TimeProvider time)
{
    public async Task<Result<TransactionDetails>> PrepareAsync(
        Guid userId,
        ParsedTransaction input,
        Guid? existingAccountId,
        CancellationToken ct)
    {
        var storedBaseCurrency = await db.Database
            .SqlQuery<string>($"""SELECT "BaseCurrency" AS "Value" FROM user_settings WHERE "UserId" = {userId} FOR SHARE""")
            .ToListAsync(ct);
        var baseCurrency = Enum.Parse<Currency>(storedBaseCurrency.Single());

        var accounts = await db.Accounts
            .FromSql($"""SELECT * FROM accounts WHERE "Id" = {input.AccountId} AND "UserId" = {userId} FOR SHARE""")
            .AsNoTracking()
            .ToListAsync(ct);
        var account = accounts.SingleOrDefault();

        var categories = await db.Categories
            .FromSql($"""SELECT * FROM categories WHERE "Id" = {input.CategoryId} AND "UserId" = {userId} FOR SHARE""")
            .AsNoTracking()
            .ToListAsync(ct);
        var category = categories.SingleOrDefault();

        var errors = new Dictionary<string, string[]>();
        if (TransactionReferenceRules.CategoryError(category?.Kind, category?.System, input.Kind) is { } categoryError)
        {
            errors[TransactionReferenceRules.CategoryField] = [categoryError];
        }

        if (TransactionReferenceRules.AccountError(account?.Archived, account is not null && account.Id == existingAccountId) is { } accountError)
        {
            errors[TransactionReferenceRules.AccountField] = [accountError];
        }

        if (errors.Count > 0 || account is null)
        {
            return Error.Validation(errors);
        }

        var plnValues = await db.FreshPlnValuesAsync(input.Date, UtcCalendar.Today(time), ct);
        var price = TransactionPrice.Compute(input.Amount, input.Currency, account.Currency, baseCurrency, input.ManualRate, plnValues);
        if (price is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        if (price.AccountAmount <= 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.positiveAmount"] });
        }

        if (price.AccountAmount > MinorUnits.MaxAmount)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.invalidValue"] });
        }

        return new TransactionDetails(
            input.Kind,
            input.Date,
            input.Title,
            input.Note,
            input.CategoryId,
            account.Id,
            input.Amount,
            input.Currency,
            price.Rate,
            price.AccountAmount,
            price.BaseAmount);
    }
}
