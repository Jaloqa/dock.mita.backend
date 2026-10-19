using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Budgets;
using Mita.Api.Common.Time;
using Mita.Domain.Budgets;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.CreateBudget;

public sealed class CreateBudgetHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<CreateBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(CreateBudgetCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = BudgetInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await db.LockAsync(userId, input.CategoryId, cancellationToken);
        if (context.CategoryError is { } categoryError)
        {
            return BudgetErrors.Category(categoryError);
        }

        var budget = Budget.Create(userId, input.CategoryId, input.Period, input.Limit, context.BaseCurrency);
        db.Budgets.Add(budget);
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
