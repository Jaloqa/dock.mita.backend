using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Money;
using Mita.Api.Common.Reporting;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Reports.GetReport;

public sealed class GetReportHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<GetReportQuery, Result<ReportResponse>>
{
    public async Task<Result<ReportResponse>> Handle(GetReportQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);
        var range = ReportMath.TryParseRange(query.Range, out _) ? query.Range! : ReportMath.DefaultRange;
        _ = ReportMath.TryParseRange(range, out var count);

        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        var (baseCurrency, monthStart) = await db.SettingsAsync(userId, cancellationToken);
        var periods = FinancialPeriod.Last(today, monthStart, count);
        var current = periods[^1];
        var from = periods[0].From;

        var sums = await db.CountedTransactions(userId, from, current.To)
            .GroupBy(transaction => new { transaction.Date, transaction.Kind })
            .Select(group => new { group.Key.Date, group.Key.Kind, Sum = group.Sum(transaction => transaction.BaseAmount) })
            .ToListAsync(cancellationToken);

        var rows = new List<ReportPeriodResponse>(count);
        foreach (var period in periods)
        {
            var inside = sums.Where(sum => sum.Date >= period.From && sum.Date <= period.To).ToList();
            rows.Add(new ReportPeriodResponse(
                period.Key.ToString(),
                IsoDate.ToWire(period.From),
                IsoDate.ToWire(period.To),
                inside.Where(sum => sum.Kind == CategoryKind.Income).Sum(sum => sum.Sum),
                inside.Where(sum => sum.Kind == CategoryKind.Expense).Sum(sum => sum.Sum)));
        }

        var income = rows.Sum(row => row.Income);
        var expense = rows.Sum(row => row.Expense);

        var spending = (await db.SpendingAsync(userId, current.From, current.To, cancellationToken))
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.Name, StringComparer.InvariantCulture)
            .ToList();
        var tenths = Shares.Tenths([.. spending.Select(item => item.Amount)]);
        var items = spending
            .Select((item, index) => new ReportStructureItemResponse(
                item.CategoryId,
                item.Name,
                CategoryWire.ToWire(item.Color),
                item.Amount,
                Shares.Percent(tenths[index])))
            .ToList();

        var hasTransactions = await db.Transactions.AnyAsync(transaction => transaction.UserId == userId, cancellationToken);

        return new ReportResponse(
            range,
            IsoDate.ToWire(from),
            IsoDate.ToWire(current.To),
            CurrencyWire.ToWire(baseCurrency),
            rows,
            new ReportTotalsResponse(income, expense),
            Shares.RoundedAverage(income, count),
            Shares.RoundedAverage(expense, count),
            ReportMath.SavingsRate(income, expense),
            new ReportStructureResponse(
                current.Key.ToString(),
                IsoDate.ToWire(current.From),
                IsoDate.ToWire(current.To),
                spending.Sum(item => item.Amount),
                items),
            hasTransactions);
    }
}
