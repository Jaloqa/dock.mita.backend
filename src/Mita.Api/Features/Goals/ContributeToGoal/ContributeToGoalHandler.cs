using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Goals;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;
using Mita.Domain.Goals;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.ContributeToGoal;

public sealed class ContributeToGoalHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<ContributeToGoalCommand, Result<GoalDto>>
{
    private static readonly Error RatesUnavailable = Error.Unavailable("rates.unavailable");

    public async Task<Result<GoalDto>> Handle(ContributeToGoalCommand command, CancellationToken cancellationToken)
    {
        if (!JsonValues.TryGetIsoDate(command.Date, out var date)
            || !JsonValues.TryGetMinorAmount(command.Amount, out var amount))
        {
            throw new InvalidOperationException("The contribution must be validated before the handler runs.");
        }

        var fromPresent = !JsonValues.IsMissing(command.FromAccountId);
        Guid? fromId = JsonValues.TryGetGuid(command.FromAccountId, out var from) ? from : null;
        decimal? manualRate = ManualRate.TryParse(command.Rate, out var given) ? given : null;
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);

        var link = await db.Goals.AsNoTracking()
            .Where(goal => goal.Id == command.Id && goal.UserId == userId)
            .Select(goal => new { goal.AccountId })
            .SingleOrDefaultAsync(cancellationToken);
        if (link is null)
        {
            return Error.NotFound();
        }

        var linkedId = link.AccountId;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (baseCurrency, _) = await db.LockSettingsAsync(userId, cancellationToken);

        Guid[] ids = [.. new[] { fromId, linkedId }.OfType<Guid>().Distinct()];
        var accounts = (await db.Accounts
                .FromSql($"""SELECT * FROM accounts WHERE "Id" = ANY({ids}) AND "UserId" = {userId} ORDER BY "Id" FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToDictionary(account => account.Id);

        var goal = await db.LockGoalAsync(userId, command.Id, cancellationToken);
        if (goal is null)
        {
            return Error.NotFound();
        }

        if (goal.AccountId != linkedId)
        {
            return Error.Conflict("goal.concurrentUpdate");
        }

        var linkedArchived = linkedId is { } linkedKey && (!accounts.TryGetValue(linkedKey, out var linkedRow) || linkedRow.Archived);
        var accountErrors = ContributionAccountRules.Errors(
            linkedId,
            linkedArchived,
            fromId,
            fromPresent,
            accounts.ToDictionary(pair => pair.Key, pair => pair.Value.Archived));
        if (accountErrors.Count > 0)
        {
            return Error.Validation(accountErrors.ToDictionary(error => error.Key, error => new[] { error.Value }));
        }

        GoalContribution contribution;
        if (linkedId is { } linkedAccountId)
        {
            var source = accounts[fromId!.Value];
            var target = accounts[linkedAccountId];

            var rate = await db.ResolveAsync(source.Currency, target.Currency, manualRate, date, cancellationToken);
            if (rate is null)
            {
                return RatesUnavailable;
            }

            var transfer = Transfer.Create(userId, date, source, target, amount, rate.Value, goal.Name);
            if (transfer.ToAmount <= 0)
            {
                return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.positiveAmount"] });
            }

            if (transfer.ToAmount > MinorUnits.MaxAmount)
            {
                return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.invalidValue"] });
            }

            var toBase = target.Currency == baseCurrency
                ? 1m
                : RateTable.Rate(await db.PlnValuesAsync(date, cancellationToken), target.Currency, baseCurrency);
            if (toBase is not { } baseRate)
            {
                return RatesUnavailable;
            }

            db.Transfers.Add(transfer);
            contribution = GoalContribution.Create(
                userId,
                goal.Id,
                date,
                amount,
                source.Currency,
                source.Id,
                transfer.Id,
                MinorUnits.Convert(transfer.ToAmount, baseRate),
                baseCurrency);
        }
        else
        {
            contribution = GoalContribution.Create(userId, goal.Id, date, amount, baseCurrency, fromId, null, amount, baseCurrency);
        }

        if (goal.Currency != baseCurrency)
        {
            var todayRate = RateTable.Rate(await db.PlnValuesAsync(today, cancellationToken), goal.Currency, baseCurrency);
            if (todayRate is not { } found)
            {
                return RatesUnavailable;
            }

            goal.Rebase(baseCurrency, found);
        }

        goal.AddSaved(contribution.BaseAmount);
        db.GoalContributions.Add(contribution);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return GoalView.From(goal, 1m, baseCurrency, today);
    }
}
