using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.AddRepayment;

public sealed class AddRepaymentReferencesValidator : AbstractValidator<AddRepaymentCommand>
{
    public AddRepaymentReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command).CustomAsync(async (command, context, cancellationToken) =>
        {
            if (!JsonValues.TryGetGuid(command.AccountId, out var accountId))
            {
                return;
            }

            var userId = currentUser.Id;
            var archived = await db.Accounts.AsNoTracking()
                .Where(candidate => candidate.Id == accountId && candidate.UserId == userId)
                .Select(candidate => (bool?)candidate.Archived)
                .SingleOrDefaultAsync(cancellationToken);

            if (TransactionReferenceRules.AccountError(archived, keepsExistingAccount: false) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(AddRepaymentCommand.AccountId), code) { ErrorCode = code });
            }
        });
    }
}
