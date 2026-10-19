using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Settings;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.DeleteCategory;

public sealed class DeleteCategoryHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteCategoryCommand, Result>
{
    public async Task<Result> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (baseCurrency, monthStart) = await db.LockSettingsAsync(userId, cancellationToken);

        // Both kinds' "other" are locked with the category in one statement ordered by "Id", the order budget copies
        // lock categories in, so the two cannot deadlock, and a kind changed before the lock needs no second statement.
        var locked = await db.Categories
            .FromSql($"""
                SELECT * FROM categories
                WHERE "UserId" = {userId} AND ("Id" = {command.Id} OR "System" = {SystemCategory.Other.ToString()})
                ORDER BY "Id"
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
        var category = locked.SingleOrDefault(candidate => candidate.Id == command.Id);
        if (category is null)
        {
            return Error.NotFound();
        }

        if (category.System is not null)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["id"] = ["validation.systemCategory"] });
        }

        var otherId = locked
            .Single(candidate => candidate.Kind == category.Kind && candidate.System == SystemCategory.Other)
            .Id;

        await db.Transactions
            .Where(candidate => candidate.CategoryId == category.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.CategoryId, otherId), cancellationToken);

        if (!await BudgetReassignment.ReassignAsync(
                db, userId, category.Id, otherId, baseCurrency, monthStart, cancellationToken))
        {
            return Error.Unavailable("rates.unavailable");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
