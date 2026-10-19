using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Goals;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Domain.Accounts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.UpdateGoal;

public sealed class UpdateGoalHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<UpdateGoalCommand, Result<GoalDto>>
{
    public async Task<Result<GoalDto>> Handle(UpdateGoalCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = GoalInputParser.Parse(command);
        var today = UtcCalendar.Today(time);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (baseCurrency, _) = await db.LockSettingsAsync(userId, cancellationToken);
        Account? account = null;
        if (input.AccountId is { } accountId)
        {
            account = await db.LockAccountAsync(userId, accountId, cancellationToken);
        }

        var goal = await db.LockGoalAsync(userId, command.Id, cancellationToken);
        if (goal is null)
        {
            return Error.NotFound();
        }

        if (input.AccountId is not null
            && GoalReferenceRules.AccountError(account?.Type, account?.Archived, goal.AccountId == input.AccountId) is { } accountError)
        {
            return GoalReferenceRules.Account(accountError);
        }

        if (goal.Currency != baseCurrency)
        {
            var rate = RateTable.Rate(await db.PlnValuesAsync(today, cancellationToken), goal.Currency, baseCurrency);
            if (rate is not { } found)
            {
                return Error.Unavailable("rates.unavailable");
            }

            goal.Rebase(baseCurrency, found);
        }

        goal.Update(input.Name, input.Target, input.Deadline, input.AccountId);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (GoalWriteLocks.FromConflict(exception) is { } conflict)
        {
            return conflict;
        }

        await transaction.CommitAsync(cancellationToken);

        return GoalView.From(goal, 1m, baseCurrency, today);
    }
}
