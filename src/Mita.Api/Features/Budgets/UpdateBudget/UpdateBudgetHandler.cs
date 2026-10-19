using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Budgets;
using Mita.Api.Common.Time;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.UpdateBudget;

public sealed class UpdateBudgetHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<UpdateBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(UpdateBudgetCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = BudgetInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await db.LockAsync(userId, input.CategoryId, cancellationToken);
        var locked = await db.Budgets
            .FromSql($"""SELECT * FROM budgets WHERE "Id" = {command.Id} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var budget = locked.SingleOrDefault();
        if (budget is null)
        {
            return Error.NotFound();
        }

        if (context.CategoryError is { } categoryError)
        {
            return BudgetErrors.Category(categoryError);
        }

        budget.Update(input.CategoryId, input.Period, input.Limit, context.BaseCurrency);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (BudgetErrors.FromConflict(exception) is { } conflict)
        {
            return conflict;
        }

        var period = FinancialPeriod.ByKey(input.Period, context.MonthStart);
        var budgets = await db.BudgetsInAsync(userId, period, context.BaseCurrency, UtcCalendar.Today(time), cancellationToken);
        if (budgets is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        await transaction.CommitAsync(cancellationToken);

        return budgets.Single(item => item.Id == budget.Id);
    }
}
