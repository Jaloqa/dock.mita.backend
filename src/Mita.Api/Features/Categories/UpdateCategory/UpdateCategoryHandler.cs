using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Categories.UpdateCategory;

public sealed class UpdateCategoryHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private const string NameIndex = "IX_categories_user_kind_name";

    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = CategoryInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var locked = await db.Categories
            .FromSql($"""SELECT * FROM categories WHERE "Id" = {command.Id} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var category = locked.SingleOrDefault();
        if (category is null)
        {
            return Error.NotFound();
        }

        if (category.Kind != input.Kind
            && (category.System is not null
                || await db.Transactions.AnyAsync(candidate => candidate.CategoryId == category.Id, cancellationToken)))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["kind"] = ["validation.categoryKindLocked"] });
        }

        category.Update(input.Name, input.Kind, input.Color);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: NameIndex
        })
        {
            return Error.Validation(new Dictionary<string, string[]> { ["name"] = ["validation.categoryNameTaken"] });
        }

        var response = await db.ProjectCategory(userId, category.Id).SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return response;
    }
}
