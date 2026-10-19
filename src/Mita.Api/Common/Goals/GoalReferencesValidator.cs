using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Goals;

public abstract class GoalReferencesValidator<T> : AbstractValidator<T>
    where T : IGoalInput
{
    protected GoalReferencesValidator(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    {
        RuleFor(input => input).CustomAsync(async (input, context, cancellationToken) =>
        {
            var hasDeadline = JsonValues.TryGetPeriodKey(input.Deadline, out var deadline);
            var hasAccount = JsonValues.TryGetGuid(input.AccountId, out var accountId);
            if (!hasDeadline && !hasAccount)
            {
                return;
            }

            var userId = currentUser.Id;
            var existingId = input.ExistingId;
            var stored = existingId is null
                ? null
                : await db.Goals.AsNoTracking()
                    .Where(goal => goal.Id == existingId && goal.UserId == userId)
                    .Select(goal => new StoredGoal(goal.Deadline, goal.AccountId))
                    .SingleOrDefaultAsync(cancellationToken);

            if (hasDeadline)
            {
                AddFailure(
                    context,
                    nameof(IGoalInput.Deadline),
                    GoalReferenceRules.DeadlineError(deadline, stored?.Deadline, UtcCalendar.Today(time)));
            }

            if (hasAccount)
            {
                var account = await db.Accounts.AsNoTracking()
                    .Where(candidate => candidate.Id == accountId && candidate.UserId == userId)
                    .Select(candidate => new { candidate.Type, candidate.Archived })
                    .SingleOrDefaultAsync(cancellationToken);
                AddFailure(
                    context,
                    nameof(IGoalInput.AccountId),
                    GoalReferenceRules.AccountError(account?.Type, account?.Archived, stored?.AccountId == accountId));
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

    private sealed record StoredGoal(PeriodKey? Deadline, Guid? AccountId);
}
