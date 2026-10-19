using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Budgets;

public abstract class BudgetReferencesValidator<T> : AbstractValidator<T>
    where T : IBudgetInput
{
    protected BudgetReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(input => input).CustomAsync(async (input, context, cancellationToken) =>
        {
            if (!JsonValues.TryGetGuid(input.CategoryId, out var categoryId))
            {
                return;
            }

            var userId = currentUser.Id;
            var existingId = input.ExistingId;
            var hasPeriod = JsonValues.TryGetPeriodKey(input.Period, out var period);
            var category = await db.Categories.AsNoTracking()
                .Where(candidate => candidate.Id == categoryId && candidate.UserId == userId)
                .Select(candidate => new
                {
                    candidate.Kind,
                    candidate.System,
                    Taken = hasPeriod && db.Budgets.Any(budget =>
                        budget.UserId == userId
                        && budget.CategoryId == candidate.Id
                        && budget.Period == period
                        && budget.Id != existingId)
                })
                .SingleOrDefaultAsync(cancellationToken);

            var error = BudgetReferenceRules.CategoryError(category?.Kind, category?.System)
                ?? (category?.Taken == true ? BudgetReferenceRules.BudgetExists : null);
            if (error is not null)
            {
                context.AddFailure(new ValidationFailure(nameof(IBudgetInput.CategoryId), error) { ErrorCode = error });
            }
        });
    }
}
