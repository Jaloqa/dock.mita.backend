using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.ContributeToGoal;

public sealed class ContributionAccountsValidator : AbstractValidator<ContributeToGoalCommand>
{
    public ContributionAccountsValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command).CustomAsync(async (command, context, cancellationToken) =>
        {
            var userId = currentUser.Id;
            var fromPresent = !JsonValues.IsMissing(command.FromAccountId);
            Guid? fromId = JsonValues.TryGetGuid(command.FromAccountId, out var from) ? from : null;
            var linkedId = await db.Goals.AsNoTracking()
                .Where(goal => goal.Id == command.Id && goal.UserId == userId)
                .Select(goal => goal.AccountId)
                .SingleOrDefaultAsync(cancellationToken);
            if (linkedId is null && !fromPresent)
            {
                return;
            }

            Guid[] ids = [.. new[] { linkedId, fromId }.OfType<Guid>().Distinct()];
            var archivedById = await db.Accounts.AsNoTracking()
                .Where(account => account.UserId == userId && ids.Contains(account.Id))
                .ToDictionaryAsync(account => account.Id, account => account.Archived, cancellationToken);
            var linkedArchived = linkedId is { } linked && archivedById.GetValueOrDefault(linked, true);

            foreach (var (field, code) in ContributionAccountRules.Errors(linkedId, linkedArchived, fromId, fromPresent, archivedById))
            {
                context.AddFailure(new ValidationFailure(field, code) { ErrorCode = code });
            }
        });
    }
}
