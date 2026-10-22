using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Transactions;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Debts;

public sealed class DebtWriter(AppDbContext db, TimeProvider time)
{
    public async Task<Result<DebtDetails>> PrepareAsync(
        Guid userId,
        ParsedDebt input,
        Guid? existingAccountId,
        CancellationToken ct)
    {
        if (input.AccountId is not { } accountId)
        {
            return Details(input, null, null, null);
        }

        var accounts = await db.Accounts
            .FromSql($"""SELECT * FROM accounts WHERE "Id" = {accountId} AND "UserId" = {userId} FOR SHARE""")
            .AsNoTracking()
            .ToListAsync(ct);
        var account = accounts.SingleOrDefault();

        var accountError = TransactionReferenceRules.AccountError(account?.Archived, account is not null && account.Id == existingAccountId);
        if (accountError is not null || account is null)
        {
            return Error.Validation(new Dictionary<string, string[]>
            {
                [TransactionReferenceRules.AccountField] = [accountError ?? TransactionReferenceRules.UnknownAccount]
            });
        }

        var plnValues = await db.FreshPlnValuesAsync(input.Date, UtcCalendar.Today(time), ct);
        var price = DebtPrice.Compute(input.Amount, input.Currency, account.Currency, input.ManualRate, plnValues);
        if (price is not { Rate: { } rate, AccountAmount: { } accountAmount })
        {
            return Error.Unavailable("rates.unavailable");
        }

        if (accountAmount <= 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.positiveAmount"] });
        }

        if (accountAmount > MinorUnits.MaxAmount)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.invalidValue"] });
        }

        return Details(input, account.Id, accountAmount, rate);
    }

    private static DebtDetails Details(ParsedDebt input, Guid? accountId, long? accountAmount, decimal? rate) =>
        new(
            input.Direction,
            input.Counterparty,
            input.Amount,
            input.Currency,
            input.Date,
            input.DueDate,
            accountId,
            accountAmount,
            rate,
            input.Note);
}
