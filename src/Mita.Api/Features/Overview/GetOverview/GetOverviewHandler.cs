using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Budgets;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Goals;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Reporting;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Domain.Goals;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Overview.GetOverview;

public sealed class GetOverviewHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<GetOverviewQuery, Result<OverviewResponse>>
{
    private const int SliceCount = 5;
    private const int TopBudgetCount = 5;
    private const int RecentCount = 6;
    private const int GoalPreviewCount = 3;

    private static readonly Comparer<BudgetDto> ByUsage = Comparer<BudgetDto>.Create(
        (left, right) => ((Int128)left.Spent * right.Limit).CompareTo((Int128)right.Spent * left.Limit));

    public async Task<Result<OverviewResponse>> Handle(GetOverviewQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);
        var range = OverviewRanges.TryParse(query.Period, out var parsed) ? parsed : OverviewRange.Month;

        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        var (baseCurrency, monthStart) = await db.SettingsAsync(userId, cancellationToken);
        var (current, previous) = OverviewRanges.Of(range, today, monthStart);

        var balance = await BalanceAsync(userId, baseCurrency, today, cancellationToken);
        if (balance is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var period = FinancialPeriod.For(today, monthStart);
        var budgets = await db.BudgetsInAsync(userId, period, baseCurrency, today, cancellationToken);
        if (budgets is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var goals = await db.GoalsAsync(userId, baseCurrency, today, cancellationToken);
        if (goals is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var totals = await TotalsAsync(userId, current, cancellationToken);
        var previousTotals = await TotalsAsync(userId, previous, cancellationToken);
        var (income, incomeCount) = totals.GetValueOrDefault(CategoryKind.Income);
        var (expense, expenseCount) = totals.GetValueOrDefault(CategoryKind.Expense);
        var spending = await db.SpendingAsync(userId, current.From, current.To, cancellationToken);
        var summary = BudgetView.Summarize(budgets, period.DaysLeft(today));

        return new OverviewResponse(
            OverviewRanges.ToWire(range),
            IsoDate.ToWire(current.From),
            IsoDate.ToWire(current.To),
            IsoDate.ToWire(previous.From),
            IsoDate.ToWire(previous.To),
            CurrencyWire.ToWire(baseCurrency),
            balance,
            new OverviewTotalResponse(
                income,
                incomeCount,
                OverviewMath.PercentChange(income, previousTotals.GetValueOrDefault(CategoryKind.Income).Amount)),
            new OverviewTotalResponse(
                expense,
                expenseCount,
                OverviewMath.PercentChange(expense, previousTotals.GetValueOrDefault(CategoryKind.Expense).Amount)),
            expenseCount == 0 ? null : Shares.RoundedAverage(expense, expenseCount),
            incomeCount + expenseCount,
            Slices(spending),
            new OverviewMonthResponse(
                period.Key.ToString(),
                IsoDate.ToWire(period.From),
                IsoDate.ToWire(period.To),
                summary.Planned,
                summary.Spent,
                summary.Left,
                budgets.Count),
            await TopBudgetsAsync(budgets, cancellationToken),
            await RecentAsync(userId, current, cancellationToken),
            GoalPreview(goals),
            goals.Count,
            await db.Transactions.AnyAsync(transaction => transaction.UserId == userId, cancellationToken));
    }

    private static List<SpendingSliceResponse> Slices(IReadOnlyList<CategorySpending> spending)
    {
        var named = spending
            .Where(item => item.System != SystemCategory.Other)
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.Name, StringComparer.InvariantCulture)
            .ToList();
        var top = named.Take(SliceCount).ToList();
        var bucket = named.Skip(SliceCount).Sum(item => item.Amount)
            + spending.Where(item => item.System == SystemCategory.Other).Sum(item => item.Amount);

        var amounts = top.Select(item => item.Amount).ToList();
        if (bucket > 0)
        {
            amounts.Add(bucket);
        }

        var tenths = Shares.Tenths(amounts);
        var slices = top
            .Select((item, index) => new SpendingSliceResponse(
                item.CategoryId,
                item.Name,
                item.Amount,
                Shares.Percent(tenths[index])))
            .ToList();
        if (bucket > 0)
        {
            slices.Add(new SpendingSliceResponse(null, null, bucket, Shares.Percent(tenths[^1])));
        }

        return slices;
    }

    private static List<OverviewGoalResponse> GoalPreview(IReadOnlyList<GoalDto> goals)
    {
        var reached = GoalWire.ToWire(GoalStatus.Reached);
        return goals
            .OrderBy(goal => goal.Status == reached)
            .Take(GoalPreviewCount)
            .Select(goal => new OverviewGoalResponse(
                goal.Id,
                goal.Name,
                goal.Target,
                goal.Saved,
                goal.Percent,
                goal.Status,
                goal.Deadline))
            .ToList();
    }

    private async Task<OverviewBalanceResponse?> BalanceAsync(
        Guid userId,
        Currency baseCurrency,
        DateOnly today,
        CancellationToken ct)
    {
        var accounts = (await db.ProjectAccounts(userId).ToListAsync(ct))
            .Where(account => !account.Archived || account.Balance != 0)
            .Select(account => (Currency: ParseCurrency(account.Currency), account.Balance))
            .ToList();

        IReadOnlyDictionary<Currency, decimal>? plnValues = accounts.Any(account => account.Currency != baseCurrency)
            ? await db.PlnValuesAsync(today, ct)
            : null;

        long amount = 0;
        foreach (var account in accounts)
        {
            if (account.Currency == baseCurrency)
            {
                amount += account.Balance;
                continue;
            }

            if (RateTable.Rate(plnValues!, account.Currency, baseCurrency) is not { } rate)
            {
                return null;
            }

            amount += MinorUnits.Convert(account.Balance, rate);
        }

        return new OverviewBalanceResponse(amount, accounts.Count);
    }

    private async Task<Dictionary<CategoryKind, (long Amount, int Count)>> TotalsAsync(
        Guid userId,
        DateRange range,
        CancellationToken ct)
    {
        var rows = await db.CountedTransactions(userId, range.From, range.To)
            .GroupBy(transaction => transaction.Kind)
            .Select(group => new
            {
                Kind = group.Key,
                Amount = group.Sum(transaction => transaction.BaseAmount),
                Count = group.Count()
            })
            .ToListAsync(ct);

        return rows.ToDictionary(row => row.Kind, row => (row.Amount, row.Count));
    }

    private async Task<List<OverviewBudgetResponse>> TopBudgetsAsync(IReadOnlyList<BudgetDto> budgets, CancellationToken ct)
    {
        var top = budgets
            .OrderByDescending(budget => budget, ByUsage)
            .ThenByDescending(budget => budget.Spent)
            .Take(TopBudgetCount)
            .ToList();
        var categoryIds = top.Select(budget => budget.CategoryId).ToList();
        var names = await db.Categories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, ct);

        return top
            .Select(budget => new OverviewBudgetResponse(
                budget.Id,
                budget.CategoryId,
                names[budget.CategoryId],
                budget.Limit,
                budget.Spent,
                budget.Remaining,
                budget.Status))
            .ToList();
    }

    private async Task<List<OverviewTransactionResponse>> RecentAsync(Guid userId, DateRange range, CancellationToken ct)
    {
        var rows = await (
                from transaction in db.CountedTransactions(userId, range.From, range.To)
                join category in db.Categories.AsNoTracking() on transaction.CategoryId equals category.Id
                join account in db.Accounts.AsNoTracking() on transaction.AccountId equals account.Id
                orderby transaction.Date descending, transaction.CreatedAt descending, transaction.Id descending
                select new
                {
                    transaction.Id,
                    transaction.Kind,
                    transaction.Date,
                    transaction.Title,
                    CategoryName = category.Name,
                    AccountName = account.Name,
                    transaction.BaseAmount
                })
            .Take(RecentCount)
            .ToListAsync(ct);

        return rows
            .Select(row => new OverviewTransactionResponse(
                row.Id,
                CategoryWire.ToWire(row.Kind),
                IsoDate.ToWire(row.Date),
                row.Title,
                row.CategoryName,
                row.AccountName,
                row.BaseAmount))
            .ToList();
    }

    private static Currency ParseCurrency(string wire) =>
        CurrencyWire.TryParse(wire, out var currency)
            ? currency
            : throw new InvalidOperationException($"Unknown account currency {wire}.");
}
