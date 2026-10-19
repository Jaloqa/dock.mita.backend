using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Goals;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Domain.Goals;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.CreateGoal;

public sealed class CreateGoalHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<CreateGoalCommand, Result<GoalDto>>
{
    public async Task<Result<GoalDto>> Handle(CreateGoalCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = GoalInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (baseCurrency, _) = await db.LockSettingsAsync(userId, cancellationToken);
        if (input.AccountId is { } accountId)
        {
            var account = await db.LockAccountAsync(userId, accountId, cancellationToken);
            if (GoalReferenceRules.AccountError(account?.Type, account?.Archived, false) is { } accountError)
            {
                return GoalReferenceRules.Account(accountError);
            }
        }

        var goal = Goal.Create(userId, input.Name, input.Target, input.Deadline, input.AccountId, baseCurrency);
        db.Goals.Add(goal);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (GoalWriteLocks.FromConflict(exception) is { } conflict)
        {
            return conflict;
        }

        await transaction.CommitAsync(cancellationToken);

        return GoalView.From(goal, 1m, baseCurrency, UtcCalendar.Today(time));
    }
}
