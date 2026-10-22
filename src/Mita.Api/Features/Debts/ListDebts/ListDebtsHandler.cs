using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Debts;
using Mita.Api.Common.Money;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Time;
using Mita.Domain.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.ListDebts;

public sealed class ListDebtsHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<ListDebtsQuery, Result<DebtPageResponse>>
{
    public async Task<Result<DebtPageResponse>> Handle(ListDebtsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var today = UtcCalendar.Today(time);
        var status = DebtStatus.Open;
        if (query.Status is not null && !DebtWire.TryParseStatus(query.Status, out status))
        {
            throw new InvalidOperationException("The debt status must be validated before the handler runs.");
        }

        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        var (baseCurrency, _) = await db.SettingsAsync(userId, cancellationToken);
        var summary = await db.SummarizeAsync(userId, baseCurrency, today, cancellationToken);
        if (summary is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var debts = db.Debts.AsNoTracking().Where(debt => debt.UserId == userId);
        var page = await (status == DebtStatus.Open ? OrderOpen(debts) : OrderSettled(debts))
            .Project(db, today)
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);

        return new DebtPageResponse(
            page.Items,
            page.Page,
            page.PageSize,
            page.Total,
            CurrencyWire.ToWire(baseCurrency),
            summary);
    }

    private static IQueryable<Debt> OrderOpen(IQueryable<Debt> debts) =>
        debts.Where(debt => debt.Repaid < debt.Amount)
            .OrderBy(debt => debt.DueDate == null)
            .ThenBy(debt => debt.DueDate)
            .ThenByDescending(debt => debt.Date)
            .ThenByDescending(debt => debt.CreatedAt)
            .ThenByDescending(debt => debt.Id);

    private static IQueryable<Debt> OrderSettled(IQueryable<Debt> debts) =>
        debts.Where(debt => debt.Repaid == debt.Amount)
            .OrderByDescending(debt => debt.Date)
            .ThenByDescending(debt => debt.CreatedAt)
            .ThenByDescending(debt => debt.Id);
}
