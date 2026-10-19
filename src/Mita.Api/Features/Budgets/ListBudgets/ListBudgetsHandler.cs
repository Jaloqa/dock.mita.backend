using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Budgets;
using Mita.Api.Common.Money;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.ListBudgets;

public sealed class ListBudgetsHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<ListBudgetsQuery, Result<BudgetPageResponse>>
{
    public async Task<Result<BudgetPageResponse>> Handle(ListBudgetsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);
        var (baseCurrency, monthStart) = await db.SettingsAsync(userId, cancellationToken);
        var period = PeriodKey.TryParse(query.Period, out var key)
            ? FinancialPeriod.ByKey(key, monthStart)
            : FinancialPeriod.For(today, monthStart);

        var budgets = await db.BudgetsInAsync(userId, period, baseCurrency, today, cancellationToken);
        if (budgets is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var paging = new PageRequest(query.Page, query.PageSize);
        var items = budgets.Skip(paging.Skip).Take(paging.PageSize).ToList();

        return new BudgetPageResponse(
            items,
            query.Page,
            query.PageSize,
            budgets.Count,
            period.Key.ToString(),
            IsoDate.ToWire(period.From),
            IsoDate.ToWire(period.To),
            CurrencyWire.ToWire(baseCurrency),
            BudgetView.Summarize(budgets, period.DaysLeft(today)));
    }
}
