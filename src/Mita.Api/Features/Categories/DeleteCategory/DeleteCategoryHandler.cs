using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
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

        await db.Database
            .SqlQuery<string>($"""SELECT "BaseCurrency" AS "Value" FROM user_settings WHERE "UserId" = {userId} FOR SHARE""")
            .ToListAsync(cancellationToken);

        var locked = await db.Categories
            .FromSql($"""SELECT * FROM categories WHERE "Id" = {command.Id} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var category = locked.SingleOrDefault();
        if (category is null)
        {
            return Error.NotFound();
        }

        if (category.System is not null)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["id"] = ["validation.systemCategory"] });
        }

        var otherId = await db.Categories
            .Where(candidate => candidate.UserId == userId
                && candidate.Kind == category.Kind
                && candidate.System == SystemCategory.Other)
            .Select(candidate => candidate.Id)
            .SingleAsync(cancellationToken);

        await db.Transactions
            .Where(candidate => candidate.CategoryId == category.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.CategoryId, otherId), cancellationToken);

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
