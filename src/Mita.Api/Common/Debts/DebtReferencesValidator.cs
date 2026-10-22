using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Debts;

public abstract class DebtReferencesValidator<T> : AbstractValidator<T>
    where T : IDebtInput
{
    protected DebtReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(input => input).CustomAsync(async (input, context, cancellationToken) =>
        {
            if (!JsonValues.TryGetGuid(input.AccountId, out var accountId))
            {
                return;
            }

            var userId = currentUser.Id;
            var existingId = input.ExistingId;
            var account = await db.Accounts.AsNoTracking()
                .Where(candidate => candidate.Id == accountId && candidate.UserId == userId)
                .Select(candidate => new
                {
                    candidate.Archived,
                    KeepsExisting = existingId != null && db.Debts.Any(debt =>
                        debt.Id == existingId && debt.UserId == userId && debt.AccountId == candidate.Id)
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (TransactionReferenceRules.AccountError(account?.Archived, account?.KeepsExisting == true) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(IDebtInput.AccountId), code) { ErrorCode = code });
            }
        });
    }
}
