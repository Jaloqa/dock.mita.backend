using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Debts;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.AddRepayment;

public sealed class AddRepaymentHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<AddRepaymentCommand, Result<DebtDto>>
{
    public async Task<Result<DebtDto>> Handle(AddRepaymentCommand command, CancellationToken cancellationToken)
    {
        if (!JsonValues.TryGetIsoDate(command.Date, out var date)
            || !JsonValues.TryGetMinorAmount(command.Amount, out var amount)
            || !CurrencyWire.TryParse(command.Currency, out var currency)
            || !JsonValues.TryGetOptionalText(command.Note, out var note))
        {
            throw new InvalidOperationException("The repayment must be validated before the handler runs.");
        }

        Guid? accountId = JsonValues.TryGetGuid(command.AccountId, out var parsedAccountId) ? parsedAccountId : null;
        decimal? manualRate = ManualRate.TryParse(command.Rate, out var given) ? given : null;
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var debt = await db.LockDebtAsync(userId, command.DebtId, cancellationToken);
        if (debt is null)
        {
            return Error.NotFound();
        }

        Account? account = null;
        if (accountId is { } lockedId)
        {
            var accounts = await db.Accounts
                .FromSql($"""SELECT * FROM accounts WHERE "Id" = {lockedId} AND "UserId" = {userId} FOR SHARE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            account = accounts.SingleOrDefault();

            var accountError = TransactionReferenceRules.AccountError(account?.Archived, keepsExistingAccount: false);
            if (accountError is not null || account is null)
            {
                return Error.Validation(new Dictionary<string, string[]>
                {
                    [TransactionReferenceRules.AccountField] = [accountError ?? TransactionReferenceRules.UnknownAccount]
                });
            }
        }

        var plnValues = await db.FreshPlnValuesAsync(date, today, cancellationToken);
        var price = RepaymentPrice.Compute(amount, currency, debt.Currency, account?.Currency, manualRate, plnValues);
        if (price is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        if (price.DebtAmount <= 0 || price.AccountAmount <= 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.positiveAmount"] });
        }

        if (price.DebtAmount > MinorUnits.MaxAmount || price.AccountAmount > MinorUnits.MaxAmount)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.invalidValue"] });
        }

        if (price.DebtAmount > debt.Remaining)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["amount"] = ["validation.exceedsRemaining"] });
        }

        debt.Repay(price.DebtAmount);
        db.DebtRepayments.Add(DebtRepayment.Create(
            userId,
            debt.Id,
            new RepaymentDetails(date, amount, currency, price.Rate, price.DebtAmount, account?.Id, price.AccountAmount, note)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.Debts.AsNoTracking()
            .Where(candidate => candidate.Id == debt.Id)
            .Project(db, today)
            .SingleAsync(cancellationToken);
    }
}
