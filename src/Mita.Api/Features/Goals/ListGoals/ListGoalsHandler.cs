using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Goals;
using Mita.Api.Common.Money;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.ListGoals;

public sealed class ListGoalsHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<ListGoalsQuery, Result<GoalPageResponse>>
{
    public async Task<Result<GoalPageResponse>> Handle(ListGoalsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);
        var (baseCurrency, _) = await db.SettingsAsync(userId, cancellationToken);

        var goals = await db.GoalsAsync(userId, baseCurrency, today, cancellationToken);
        var monthly = await db.MonthlySavingAsync(userId, baseCurrency, today, cancellationToken);
        if (goals is null || monthly is not { } saving)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var paging = new PageRequest(query.Page, query.PageSize);
        var items = goals.Skip(paging.Skip).Take(paging.PageSize).ToList();

        return new GoalPageResponse(
            items,
            query.Page,
            query.PageSize,
            goals.Count,
            CurrencyWire.ToWire(baseCurrency),
            GoalView.Summarize(goals, saving));
    }
}
