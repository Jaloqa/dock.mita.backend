using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Transactions;

public abstract class TransactionReferencesValidator<T> : AbstractValidator<T>
    where T : ITransactionInput
{
    protected TransactionReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(input => input).CustomAsync(async (input, context, cancellationToken) =>
        {
            var userId = currentUser.Id;

            if (JsonValues.TryGetGuid(input.CategoryId, out var categoryId))
            {
                var category = await db.Categories.AsNoTracking()
                    .Where(candidate => candidate.Id == categoryId && candidate.UserId == userId)
                    .Select(candidate => new { candidate.Kind, candidate.System })
                    .SingleOrDefaultAsync(cancellationToken);
                CategoryKind? requested = CategoryWire.TryParse(input.Kind, out CategoryKind kind) ? kind : null;
                AddFailure(
                    context,
                    nameof(ITransactionInput.CategoryId),
                    TransactionReferenceRules.CategoryError(category?.Kind, category?.System, requested));
            }

            if (JsonValues.TryGetGuid(input.AccountId, out var accountId))
            {
                var existingId = input.ExistingId;
                var account = await db.Accounts.AsNoTracking()
                    .Where(candidate => candidate.Id == accountId && candidate.UserId == userId)
                    .Select(candidate => new
                    {
                        candidate.Archived,
                        KeepsExisting = existingId != null && db.Transactions.Any(transaction =>
                            transaction.Id == existingId && transaction.UserId == userId && transaction.AccountId == candidate.Id)
                    })
                    .SingleOrDefaultAsync(cancellationToken);
                AddFailure(
                    context,
                    nameof(ITransactionInput.AccountId),
                    TransactionReferenceRules.AccountError(account?.Archived, account?.KeepsExisting == true));
            }
        });
    }

    private static void AddFailure(ValidationContext<T> context, string property, string? code)
    {
        if (code is not null)
        {
            context.AddFailure(new ValidationFailure(property, code) { ErrorCode = code });
        }
    }
}
